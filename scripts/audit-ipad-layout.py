#!/usr/bin/env python3
# audit-ipad-layout.py ⭐️
#
# Escape Nine: Endless の iPad レイアウトルール監査ワークフロー（read-only）。
# CLAUDE.md「iPad レイアウトルール（厳守）」に対し、Views/ 配下の SwiftUI View を走査して
# 以下 3 種の違反候補を検出し、ファイル別にグループ化した HTML レポートを出力する。
#
#   検出器A: View 内での直接 `ResponsiveLayout.isIPad()` 分岐
#            （ルール「View 内で直接 isIPad() 分岐しない／ResponsiveLayout 経由」違反）
#   検出器B: 固定 pt の .frame()（hardcode された width/height/maxWidth/maxHeight 数値リテラル）
#            （ルール「固定 pt サイズ禁止」違反）
#   検出器C: View を宣言しているのに `#Preview("iPad")` が無いファイル
#            （ルール「主要 View には iPhone/iPad 両方の #Preview」「新規 View は両対応 Preview 必須」）
#
# 設計メモ（正直性のための limitation 明記）:
#   - frame 検出は「行単位」。複数行にまたがる .frame( は B では拾わないが、本コードベースでは
#     それらは全て isIPad() を含むため検出器A が捕捉する（取りこぼしゼロ）。brace-aware 化は今回スコープ外。
#   - 検出器B は「純数値リテラル（123 / 1.5 / CGFloat(12) / 三項の両枝が数値）」のみ flag。
#     .infinity・英字識別子（cellSize / geometry.* / ResponsiveLayout.* 等）を含む値は responsive とみなしスキップ。
#   - isIPad() を含む行は検出器A 専管とし、B では二重計上しない。
#   - #Preview { ... } ブロック内の .frame() は「プレビュー用の意図的サイズ」なので B から除外する。
#   - 正本である ResponsiveLayout.swift（Utilities/）は Views/ 外なので走査対象外＝その内部 isIPad() は flag されない。
#
# コードは一切変更しない。出力は reports/audit-ipad-layout-YYYYMMDD-HHMM.html と stdout サマリのみ。

import os
import re
import sys
import html
from datetime import datetime

# ---- 設定 ----------------------------------------------------------------

# CLAUDE.md が「主要 View」として名指しするファイル（iPhone/iPad 両 Preview 必須）
MAIN_VIEWS = {
    "GameView", "HomeView", "ShopView", "RankingView", "CharacterSelectionView",
}

FRAME_DIM_LABELS = {"width", "height", "maxWidth", "maxHeight", "idealWidth", "idealHeight"}
TOUCH_TARGET_LABELS = {"minWidth", "minHeight"}  # アクセシビリティ最小タッチ領域＝推奨。flag しない

# ---- 正規表現 ------------------------------------------------------------

STRUCT_NAME_RE = re.compile(r"\bstruct\s+(\w+)")
GENERIC_CLAUSE_RE = re.compile(r"<[^>]*>")  # ジェネリクス <Content: View> を除去するため
CONFORMANCE_RE = re.compile(r":\s*([^{\n]+)")
PREVIEW_LABEL_RE = re.compile(r'#Preview\s*\(\s*"([^"]*)"')
NUM_RE = re.compile(r"^-?\d+(\.\d+)?$")
CGFLOAT_RE = re.compile(r"^CGFloat\(\s*-?\d+(\.\d+)?\s*\)$")
ISIPAD_TOKEN = "ResponsiveLayout.isIPad()"


# ---- 解析ヘルパ ----------------------------------------------------------

def is_pure_numeric(expr):
    e = expr.strip()
    return bool(NUM_RE.match(e) or CGFLOAT_RE.match(e))


def split_top_level_commas(s):
    """括弧の深さを尊重してトップレベルのカンマで分割する。"""
    parts, depth, cur = [], 0, ""
    for ch in s:
        if ch in "([{":
            depth += 1
            cur += ch
        elif ch in ")]}":
            depth -= 1
            cur += ch
        elif ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    return parts


def extract_frame_args(line):
    """行内の最初の .frame( ... ) の内側文字列を返す。行内で閉じない場合は None。"""
    idx = line.find(".frame(")
    if idx == -1:
        return None
    i = idx + len(".frame(")
    depth, inner = 1, ""
    while i < len(line):
        ch = line[i]
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return inner
        inner += ch
        i += 1
    return None  # 複数行 frame（A で捕捉される）


def classify_frame_value(value):
    """frame 引数値が固定 pt かどうか判定。
    戻り値: 'literal'（純数値）/ 'ternary'（三項の両枝が数値）/ None（responsive=flag しない）"""
    v = value.strip()
    if v == ".infinity" or v.endswith(".infinity"):
        return None
    if "?" in v and ":" in v:
        rest = v[v.find("?") + 1:]
        ci = rest.find(":")
        if ci != -1:
            a, b = rest[:ci], rest[ci + 1:]
            if is_pure_numeric(a) and is_pure_numeric(b):
                return "ternary"
        return None
    if is_pure_numeric(v):
        return "literal"
    return None  # 英字識別子・式を含む => responsive


def preview_line_set(lines):
    """#Preview ブロックに属する行番号（1-based）の集合を返す。"""
    in_preview = set()
    n = len(lines)
    i = 0
    while i < n:
        if "#Preview" in lines[i]:
            depth, started, j = 0, False, i
            while j < n:
                for ch in lines[j]:
                    if ch == "{":
                        depth += 1
                        started = True
                    elif ch == "}":
                        depth -= 1
                in_preview.add(j + 1)
                if started and depth == 0:
                    break
                j += 1
            i = j + 1
        else:
            i += 1
    return in_preview


def file_view_structs(lines):
    """ファイルが宣言する SwiftUI View 構造体名のリスト（: View に厳密一致）。
    ジェネリクス（例: struct GameCard<Content: View>: View）の <...> 内コロンに惑わされないよう除去してから判定。"""
    names = []
    for ln in lines:
        m = STRUCT_NAME_RE.search(ln)
        if not m:
            continue
        rest = GENERIC_CLAUSE_RE.sub("", ln[m.end():])  # <Content: View> を消す
        cm = CONFORMANCE_RE.search(rest)
        if not cm:
            continue
        conforms = [c.split("where")[0].strip() for c in cm.group(1).split(",")]
        if "View" in conforms:
            names.append(m.group(1))
    return names


def preview_state(lines):
    """(#Preview が存在するか, iPad ラベルの #Preview が存在するか)"""
    has_any, has_ipad = False, False
    for ln in lines:
        if "#Preview" in ln:
            has_any = True
            m = PREVIEW_LABEL_RE.search(ln)
            if m and "ipad" in m.group(1).lower():
                has_ipad = True
    return has_any, has_ipad


def b_notes(line, value):
    """検出器B の補足注記（装飾/スペーサー/微小値の可能性）。"""
    notes = []
    if "Spacer" in line or "Color.clear" in line or "Divider" in line:
        notes.append("スペーサー/装飾")
    if is_pure_numeric(value):
        try:
            if abs(float(value.strip().replace("CGFloat(", "").replace(")", ""))) <= 4:
                notes.append("微小値（ヘアライン等、意図的の可能性）")
        except ValueError:
            pass
    return notes


# ---- ファイル監査 --------------------------------------------------------

def audit_file(path, rel):
    with open(path, encoding="utf-8") as f:
        lines = f.readlines()

    view_names = file_view_structs(lines)
    if not view_names:
        return None  # SwiftUI View を宣言しないファイルは対象外（Representable 等）

    previews = preview_line_set(lines)
    findings = []

    for idx, raw in enumerate(lines, start=1):
        line = raw.rstrip("\n")

        # 検出器A: 直接 isIPad() 分岐（Preview ブロック内は本来稀なので注記のみ）
        if ISIPAD_TOKEN in line:
            findings.append({
                "detector": "A",
                "severity": "medium",
                "line": idx,
                "code": line.strip(),
                "msg": "View 内で直接 isIPad() 分岐（ResponsiveLayout のメソッド経由にすべき）",
                "notes": ["#Preview 内"] if idx in previews else [],
            })
            continue  # A 専管。同一行を B で二重計上しない

        # 検出器B: 固定 pt frame（Preview ブロック内は除外）
        if ".frame(" in line and idx not in previews:
            inner = extract_frame_args(line)
            if inner is not None:
                for arg in split_top_level_commas(inner):
                    if ":" not in arg:
                        continue
                    label, _, value = arg.partition(":")
                    label = label.strip()
                    if label in TOUCH_TARGET_LABELS:
                        continue  # タッチ領域最小値は推奨
                    if label not in FRAME_DIM_LABELS:
                        continue
                    kind = classify_frame_value(value)
                    if kind is None:
                        continue
                    notes = b_notes(line, value)
                    sev = "low" if notes else "medium"
                    findings.append({
                        "detector": "B",
                        "severity": sev,
                        "line": idx,
                        "code": line.strip(),
                        "msg": f"固定 pt frame（{label}: {value.strip()}）— 比率/ResponsiveLayout 経由にすべき",
                        "notes": notes,
                    })

    # 検出器C: iPad Preview 欠落（ファイル単位）
    has_any, has_ipad = preview_state(lines)
    if not has_ipad:
        is_main = any(n in MAIN_VIEWS for n in view_names) or \
            os.path.basename(path).replace(".swift", "") in MAIN_VIEWS
        if is_main:
            sev = "high"
        elif "/Components/" in rel:
            sev = "low"
        else:
            sev = "medium"
        msg = ('#Preview が一つも無い（iPhone/iPad とも欠落）'
               if not has_any else
               '#Preview はあるが "iPad" ラベルの Preview が無い')
        findings.append({
            "detector": "C",
            "severity": sev,
            "line": None,
            "code": "",
            "msg": msg,
            "notes": [],
        })

    return {
        "rel": rel,
        "views": view_names,
        "is_main": any(n in MAIN_VIEWS for n in view_names),
        "ipad_preview": has_ipad,
        "findings": findings,
    }


# ---- レポート出力 --------------------------------------------------------

SEV_ORDER = {"high": 0, "medium": 1, "low": 2}
SEV_LABEL = {"high": "HIGH", "medium": "MEDIUM", "low": "LOW"}
DET_LABEL = {
    "A": "A · 直接 isIPad() 分岐",
    "B": "B · 固定 pt frame",
    "C": "C · iPad Preview 欠落",
}


def build_html(results, compliant, generated_at, views_dir):
    total = {"A": 0, "B": 0, "C": 0}
    sev_total = {"high": 0, "medium": 0, "low": 0}
    for r in results:
        for f in r["findings"]:
            total[f["detector"]] += 1
            sev_total[f["severity"]] += 1
    file_count = len(results) + len(compliant)
    flagged_files = len(results)

    out = []
    out.append('<!doctype html>')
    out.append('<html lang="ja"><head><meta charset="utf-8">')
    out.append('<meta name="viewport" content="width=device-width, initial-scale=1">')
    out.append('<title>iPad レイアウト監査 — Escape Nine: Endless</title>')
    out.append('''<style>
  :root { color-scheme: light dark; }
  body { font-family: -apple-system, BlinkMacSystemFont, "Helvetica Neue", sans-serif;
         max-width: 1000px; margin: 0 auto; padding: 24px; line-height: 1.6; }
  h1 { border-bottom: 3px solid #daa520; padding-bottom: 8px; }
  h2 { margin-top: 36px; border-bottom: 1px solid #ccc; padding-bottom: 4px; }
  .meta { color: #666; font-size: 0.9em; }
  table { border-collapse: collapse; width: 100%; margin: 12px 0; }
  th, td { border: 1px solid #ccc; padding: 8px; text-align: left; vertical-align: top; }
  th { background: #f0e6d2; }
  .badge { display: inline-block; padding: 1px 8px; border-radius: 10px; font-size: 0.8em; font-weight: 700; }
  .b-high { background: #d33; color: #fff; }
  .b-medium { background: #e8a23d; color: #fff; }
  .b-low { background: #5887c4; color: #fff; }
  .finding { padding: 8px 12px; margin: 6px 0; border-radius: 4px; }
  .sev-high { background: #ffe5e5; border-left: 4px solid #d33; }
  .sev-medium { background: #fff5e0; border-left: 4px solid #e8a23d; }
  .sev-low { background: #eef3fb; border-left: 4px solid #5887c4; }
  code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
  pre { background: #f4f4f4; padding: 8px 10px; overflow-x: auto; border-radius: 4px; margin: 4px 0; font-size: 0.85em; }
  .ln { color: #888; }
  .note { color: #777; font-size: 0.85em; }
  .ok { color: #2e7d32; font-weight: 700; }
  a.jump { text-decoration: none; }
  .filehead code { font-size: 1.05em; font-weight: 700; }
  @media (prefers-color-scheme: dark) {
    body { background: #1a1a1a; color: #e8e8e8; }
    th { background: #3a3220; } th, td { border-color: #444; }
    pre { background: #2a2a2a; } .ln { color: #999; } .note { color: #aaa; }
    .sev-high { background: #3a1a1a; } .sev-medium { background: #3a2e1a; } .sev-low { background: #1a2436; }
    h2 { border-color: #444; } .filehead code { color: #ffd27f; }
    .meta { color: #aaa; } .ok { color: #90ee90; }
  }
  @media print { body { max-width: none; } a { color: inherit; } }
</style></head><body>''')

    out.append('<h1>iPad レイアウト監査レポート</h1>')
    out.append(f'<p class="meta"><strong>対象:</strong> Escape Nine: Endless — <code>{html.escape(views_dir)}</code><br>'
               f'<strong>生成日時:</strong> {generated_at}<br>'
               f'<strong>ルート典拠:</strong> CLAUDE.md「iPad レイアウトルール（厳守）」 / read-only（コード変更なし）</p>')

    # サマリ
    out.append('<h2>サマリ</h2>')
    out.append(f'<p>走査した View ファイル <strong>{file_count}</strong> 件中、'
               f'指摘あり <strong>{flagged_files}</strong> 件 / 準拠 <strong>{len(compliant)}</strong> 件。</p>')
    out.append('<table><tr><th>検出器</th><th>内容</th><th>件数</th></tr>')
    out.append(f'<tr><td>A</td><td>View 内で直接 <code>isIPad()</code> 分岐</td><td>{total["A"]}</td></tr>')
    out.append(f'<tr><td>B</td><td>固定 pt の <code>.frame()</code></td><td>{total["B"]}</td></tr>')
    out.append(f'<tr><td>C</td><td><code>#Preview("iPad")</code> 欠落（ファイル単位）</td><td>{total["C"]}</td></tr>')
    out.append('</table>')
    out.append('<table><tr><th>Severity</th><th>件数</th></tr>')
    out.append(f'<tr><td><span class="badge b-high">HIGH</span></td><td>{sev_total["high"]}</td></tr>')
    out.append(f'<tr><td><span class="badge b-medium">MEDIUM</span></td><td>{sev_total["medium"]}</td></tr>')
    out.append(f'<tr><td><span class="badge b-low">LOW</span></td><td>{sev_total["low"]}</td></tr>')
    out.append('</table>')

    # 検出基準と範囲（limitation）
    out.append('<h2>検出基準と範囲（limitation）</h2>')
    out.append('<ul>'
               '<li><strong>検出器A</strong> は <code>ResponsiveLayout.isIPad()</code> トークンに一致。'
               'Views/ 内に <code>userInterfaceIdiom</code> / <code>horizontalSizeClass</code> 等の'
               '別形態の直接端末分岐は存在しないことを確認済み（A は全数）。'
               '正本 <code>ResponsiveLayout.swift</code> は Views/ 外のため非対象（その内部 isIPad() は正当）。</li>'
               '<li><strong>検出器B</strong> は <code>.frame()</code> の width/height/maxWidth/maxHeight に'
               '純数値リテラル（<code>123</code> / <code>CGFloat(12)</code> / 三項の両枝が数値）が来る場合のみ flag。'
               '<code>.infinity</code>・比率（geometry.*）・<code>ResponsiveLayout.*</code>・変数はスキップ。'
               '<code>minWidth/minHeight</code>（タッチ領域最小値）は推奨のため除外。'
               '<code>#Preview</code> ブロック内の frame は意図的サイズとして除外。'
               '判定は<strong>行単位</strong>（複数行 frame は isIPad を含むため A が捕捉）。</li>'
               '<li><strong>検出器C</strong> はラベルに "iPad"（大小無視）を含む <code>#Preview</code> の有無で判定（例: '
               '<code>"iPad — danger cell"</code> は準拠扱い）。本コードベースに traits のみで iPad を表す Preview は無い。</li>'
               '</ul>')

    # 主要 View の準拠状況
    out.append('<h2>主要 View の iPad Preview 準拠状況</h2>')
    out.append('<table><tr><th>主要 View</th><th>iPad Preview</th></tr>')
    main_seen = {}
    for r in results + compliant:
        for n in r["views"]:
            if n in MAIN_VIEWS:
                main_seen[n] = r["ipad_preview"]
    for n in sorted(MAIN_VIEWS):
        st = main_seen.get(n)
        if st is True:
            cell = '<span class="ok">✅ あり</span>'
        elif st is False:
            cell = '<span class="badge b-high">欠落</span>'
        else:
            cell = '—（未検出）'
        out.append(f'<tr><td><code>{n}</code></td><td>{cell}</td></tr>')
    out.append('</table>')

    # ファイル別インデックス
    out.append('<h2>ファイル別インデックス</h2>')
    out.append('<table><tr><th>ファイル</th><th>A</th><th>B</th><th>C</th><th>最大 severity</th></tr>')
    for i, r in enumerate(results):
        c = {"A": 0, "B": 0, "C": 0}
        worst = "low"
        for f in r["findings"]:
            c[f["detector"]] += 1
            if SEV_ORDER[f["severity"]] < SEV_ORDER[worst]:
                worst = f["severity"]
        out.append(f'<tr><td><a class="jump" href="#f{i}"><code>{html.escape(r["rel"])}</code></a></td>'
                   f'<td>{c["A"] or ""}</td><td>{c["B"] or ""}</td><td>{c["C"] or ""}</td>'
                   f'<td><span class="badge b-{worst}">{SEV_LABEL[worst]}</span></td></tr>')
    out.append('</table>')

    # ファイル別詳細
    out.append('<h2>ファイル別詳細</h2>')
    for i, r in enumerate(results):
        main_tag = ' <span class="badge b-high">主要 View</span>' if r["is_main"] else ""
        out.append(f'<div class="filehead" id="f{i}"><h3><code>{html.escape(r["rel"])}</code>{main_tag}</h3>')
        out.append(f'<p class="note">View: {", ".join(html.escape(v) for v in r["views"])}</p>')
        ordered = sorted(r["findings"], key=lambda f: (SEV_ORDER[f["severity"]], f["detector"], f["line"] or 0))
        for f in ordered:
            sev = f["severity"]
            loc = f'<span class="ln">L{f["line"]}</span> ' if f["line"] else ""
            note = ""
            if f["notes"]:
                note = f'<div class="note">注: {html.escape(" / ".join(f["notes"]))}</div>'
            code = f'<pre>{html.escape(f["code"])}</pre>' if f["code"] else ""
            out.append(f'<div class="finding sev-{sev}">'
                       f'<span class="badge b-{sev}">{SEV_LABEL[sev]}</span> '
                       f'<strong>[{DET_LABEL[f["detector"]]}]</strong> {loc}<br>'
                       f'{html.escape(f["msg"])}{code}{note}</div>')
        out.append('</div>')

    # 準拠ファイル
    if compliant:
        out.append('<h2>✅ 指摘なしのファイル</h2><ul>')
        for r in sorted(compliant, key=lambda x: x["rel"]):
            tag = " （主要 View）" if r["is_main"] else ""
            out.append(f'<li><code>{html.escape(r["rel"])}</code>{tag}</li>')
        out.append('</ul>')

    out.append('</body></html>')
    return "\n".join(out)


def print_summary(results, compliant):
    total = {"A": 0, "B": 0, "C": 0}
    sev = {"high": 0, "medium": 0, "low": 0}
    for r in results:
        for f in r["findings"]:
            total[f["detector"]] += 1
            sev[f["severity"]] += 1
    print("=" * 64)
    print("iPad レイアウト監査サマリ")
    print("=" * 64)
    print(f"対象 View ファイル: {len(results) + len(compliant)}  / 指摘あり: {len(results)}  / 準拠: {len(compliant)}")
    print(f"  A 直接 isIPad() 分岐 : {total['A']}")
    print(f"  B 固定 pt frame      : {total['B']}")
    print(f"  C iPad Preview 欠落  : {total['C']}")
    print(f"  severity  HIGH:{sev['high']}  MEDIUM:{sev['medium']}  LOW:{sev['low']}")
    print("-" * 64)
    for r in sorted(results, key=lambda x: x["rel"]):
        c = {"A": 0, "B": 0, "C": 0}
        for f in r["findings"]:
            c[f["detector"]] += 1
        print(f"  {r['rel']:<46} A:{c['A']} B:{c['B']} C:{c['C']}")
    print("=" * 64)


def main():
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    default_views = os.path.join(
        repo_root, "EscapeNine-endless-", "EscapeNine-endless-", "Views")
    views_dir = sys.argv[1] if len(sys.argv) > 1 else default_views

    if not os.path.isdir(views_dir):
        print(f"ERROR: Views ディレクトリが見つかりません: {views_dir}", file=sys.stderr)
        sys.exit(1)

    swift_files = []
    for root, _, files in os.walk(views_dir):
        for fn in files:
            if fn.endswith(".swift"):
                swift_files.append(os.path.join(root, fn))
    swift_files.sort()

    results, compliant = [], []
    for path in swift_files:
        rel = os.path.relpath(path, repo_root)
        r = audit_file(path, rel)
        if r is None:
            continue
        (results if r["findings"] else compliant).append(r)

    # ファイル別グループ: 最大 severity が高い順 → ファイル名
    results.sort(key=lambda r: (min((SEV_ORDER[f["severity"]] for f in r["findings"]), default=9), r["rel"]))

    now = datetime.now()
    generated_at = now.strftime("%Y-%m-%d %H:%M")
    reports_dir = os.path.join(repo_root, "reports")
    os.makedirs(reports_dir, exist_ok=True)
    out_path = os.path.join(reports_dir, f"audit-ipad-layout-{now.strftime('%Y%m%d-%H%M')}.html")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(build_html(results, compliant, generated_at, os.path.relpath(views_dir, repo_root)))

    print_summary(results, compliant)
    print(f"HTML レポート: {os.path.relpath(out_path, repo_root)}")


if __name__ == "__main__":
    main()
