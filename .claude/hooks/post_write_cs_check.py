#!/usr/bin/env python3
"""PostToolUse hook: .cs 編集の回帰チェック (Escape Nine Unity) ☀️

方針: .cs のみ対象 / 非対象ファイルは no-op / 原則 exit 0 (警告のみ、ブロックしない)。
警告は stdout の hookSpecificOutput.additionalContext JSON で Claude に届ける
(PostToolUse で exit 0 の stderr は Claude に届かないため。/review-full 2026-07-15 C1)。
唯一の例外: Core での UnityEngine.Random / using UnityEngine は exit 2 (ブロック級、stderr)。
根拠: .claude/rules/daily-seed-spec.md (シード再現性) / unity-conventions.md / save-compat-ledger.md
他 Unity 案件へ流用する時は下の設定ブロックだけ調整する (汎用版: ~/.claude/templates/unity/)。
"""
from __future__ import annotations

import json
import os
import re
import sys

# ---- プロジェクト固有設定 --------------------------------------------------
# Core (純 .NET・乱数は IRandomSource 経由のみ) と判定するパス断片
CORE_DIR_MARKERS = ["/Assets/Scripts/Core/"]
# UnityEngine.Random を演出用途として許可するディレクトリ
FX_ALLOWED_MARKERS = ["/Assets/Scripts/Runtime/Stage/", "/Assets/Scripts/Runtime/UI/Fx/"]
# PlayerPrefs キー台帳 (リポジトリルートからの相対)
LEDGER_RELPATH = ".claude/rules/save-compat-ledger.md"
# ----------------------------------------------------------------------------

RANDOM_RE = re.compile(
    r"\bUnityEngine\.Random\b"
    r"|\bRandom\.(Range|value|insideUnitCircle|insideUnitSphere|onUnitSphere|rotation\b|rotationUniform|InitState)"
)
USING_UNITY_RE = re.compile(r"^\s*using\s+UnityEngine", re.M)
# DebugStartFloor 等のデバッグ識別子 (Debug.Log は「Debug.」なのでマッチしない)
DEBUG_IDENT_RE = re.compile(r"\bDebug[A-Z]\w*")
DEBUG_GUARD = "UNITY_EDITOR || DEVELOPMENT_BUILD"
# アクセス修飾子で始まるメンバー宣言行 (ガード漏れチェック②の対象外)
MEMBER_DECL_RE = re.compile(r"\s*(public|private|protected|internal)\b")
# Set 系書き込み呼び出し (PlayerPrefs.SetInt / SetBool 等。ガード漏れチェック②の対象外)
WRITE_CALL_RE = re.compile(r"\bSet\w*\s*\(")
CSHARP10_RE = re.compile(
    r"^\s*namespace\s+[\w.]+\s*;"      # file-scoped namespace (C#10)
    r"|^\s*global\s+using\s"            # global using (C#10)
    r"|\brecord\s+struct\b"             # record struct (C#10)
    r"|\brequired\s+(?:public|private|internal|protected|\w+\s+\w+\s*[;{=])",  # required (C#11)
    re.M,
)
PLAYERPREFS_KEY_RE = re.compile(
    r'PlayerPrefs\.(?:Set|Get|HasKey|DeleteKey)\w*\(\s*"([^"\n]+)"'
    r'|const\s+string\s+\w*Key\s*=\s*"([^"\n]+)"'
)


def strip_comments(src: str) -> str:
    """コメントのみ除去し、文字列リテラルは保持する簡易字句スキャナ。

    正規表現置換だと文字列内の "https://..." 以降をコメント扱いで
    誤削除する (/review-full 2026-07-15 G2)。通常文字列 (\\ エスケープ)・
    verbatim 文字列 @"..." ("" エスケープ)・char リテラルを状態として区別する。
    """
    out: list[str] = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        nxt = src[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":  # 行コメント → 行末まで捨てる
            while i < n and src[i] != "\n":
                i += 1
        elif c == "/" and nxt == "*":  # ブロックコメント → */ まで捨てる
            i += 2
            while i + 1 < n and not (src[i] == "*" and src[i + 1] == "/"):
                i += 1
            i = min(i + 2, n)
        elif c == "@" and nxt == '"':  # verbatim 文字列: "" が唯一のエスケープ
            out.append('@"')
            i += 2
            while i < n:
                out.append(src[i])
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        out.append('"')
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
        elif c == '"' or c == "'":  # 通常文字列 / char: \ エスケープ
            quote = c
            out.append(c)
            i += 1
            while i < n:
                ch = src[i]
                out.append(ch)
                if ch == "\\" and i + 1 < n:
                    out.append(src[i + 1])
                    i += 2
                    continue
                i += 1
                if ch == quote or ch == "\n":  # 行跨ぎは不正リテラルなので打ち切り
                    break
        else:
            out.append(c)
            i += 1
    return "".join(out)


def find_unguarded_debug_lines(src: str) -> list[int]:
    """`#if UNITY_EDITOR || DEVELOPMENT_BUILD` の外にある Debug* 識別子の行番号 (1-based)。

    ファイル単位の存在チェックだと「ガード済み Debug* がある大ファイルに
    無ガードの新規 Debug* を追加」を見逃す (/review-full 2026-07-15 G5)。
    行ベースで #if スタックを追う。ガード #if の #else 側は「リリース時のデフォルト値
    リセット」という正しい慣行 (PlayerState.cs:230-236) なので対象外のまま。
    メンバー宣言行 (アクセス修飾子で始まる行 = const キー名・プロパティ宣言等) は対象外:
    宣言自体は挙動を持たず、危険なのは未ガードの読み書き・永続化 (830e1a6 の実パターン)。
    PlayerState.cs の Debug* プロパティ宣言は「デバッグ UI からのみ触る想定」と文書化された既存慣行。
    """
    hits: list[int] = []
    stack: list[bool] = []  # 各 #if がガード条件かどうか
    for lineno, line in enumerate(src.splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith("#if"):
            stack.append(DEBUG_GUARD in stripped)
        elif stripped.startswith("#endif"):
            if stack:
                stack.pop()
        elif stripped.startswith("#else") or stripped.startswith("#elif"):
            pass  # ガード #if の #else 側 = リリース時のデフォルト値リセット (PlayerState.cs:230-236 の正しい慣行) なので対象外のまま
        elif MEMBER_DECL_RE.match(line) or WRITE_CALL_RE.search(line):
            continue  # 宣言行と Set 系書き込みは対象外 (830e1a6 の穴は「リリースでの読み込み・分岐」。
            #           リリースで改変不能な in-memory 値の永続化 (PlayerState.Save) は穴にならない)
        elif DEBUG_IDENT_RE.search(line) and not any(stack):
            hits.append(lineno)
    return hits


def find_repo_root(path: str) -> str | None:
    cur = os.path.dirname(os.path.abspath(path))
    for _ in range(12):
        if os.path.isfile(os.path.join(cur, LEDGER_RELPATH)):
            return cur
        parent = os.path.dirname(cur)
        if parent == cur:
            return None
        cur = parent
    return None


def emit_warnings(warnings: list[str], name: str) -> None:
    """警告を additionalContext として stdout に出す (exit 0 でも Claude に届く公式経路)"""
    if not warnings:
        return
    text = "\n".join(f"[post_write_cs_check] {name}: {w}" for w in warnings)
    print(json.dumps(
        {"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": text}},
        ensure_ascii=False,
    ))


def main() -> int:
    try:
        data = json.load(sys.stdin)
    except Exception:
        return 0
    fp = (data.get("tool_input") or {}).get("file_path") or ""
    if not fp.endswith(".cs"):
        return 0  # 非対象は no-op
    try:
        with open(fp, encoding="utf-8", errors="replace") as f:
            raw = f.read()
    except OSError:
        return 0

    src = strip_comments(raw)
    norm = fp.replace("\\", "/")
    in_core = any(m in norm for m in CORE_DIR_MARKERS)
    in_fx = any(m in norm for m in FX_ALLOWED_MARKERS)
    name = os.path.basename(fp)
    warnings: list[str] = []

    # ① UnityEngine.Random — 唯一の強めチェック (Core は exit 2 でブロック級)
    has_random = bool(RANDOM_RE.search(src))
    if in_core and (has_random or USING_UNITY_RE.search(src)):
        print(
            f"[post_write_cs_check] BLOCK: {name} (Core) に UnityEngine.Random / using UnityEngine を検出。"
            "Core は noEngineReferences の純 .NET。乱数は IRandomSource 経由にすること"
            "(デイリーチャレンジのシード再現性が壊れる。.claude/rules/daily-seed-spec.md 参照)",
            file=sys.stderr,
        )
        return 2
    if has_random and not in_fx:
        warnings.append(
            "UnityEngine.Random をゲームロジック層で検出。演出 (Runtime/Stage/, Runtime/UI/Fx/) 以外は "
            "IRandomSource 経由にする (daily-seed-spec.md)"
        )

    # ② デバッグ識別子のビルドガード漏れ (830e1a6: 有料キャラ全開放の穴)
    #    Tests/ は asmdef (UNITY_INCLUDE_TESTS) で Editor 限定のため対象外
    in_tests = "/Assets/Tests/" in norm or name.endswith("Tests.cs")
    if not in_tests:
        unguarded = find_unguarded_debug_lines(src)
        if unguarded:
            lines = ", ".join(f"L{n}" for n in unguarded[:5])
            warnings.append(
                f"`#if UNITY_EDITOR || DEVELOPMENT_BUILD` ガード外に Debug* 系識別子 ({lines})。"
                "リリースビルドへのデバッグ機能漏れに注意 (unity-conventions.md)"
            )

    # ③ UnityWebRequest.timeout 未設定 (69e974d: 認証デッドロック)
    if "UnityWebRequest" in src and ".timeout" not in src:
        warnings.append(
            "UnityWebRequest 使用ファイルに .timeout 設定が見当たらない。"
            "通信スタック時にデッドロックする (unity-conventions.md)"
        )

    # ④ PlayerPrefs 新キー検出 → セーブ互換台帳の更新リマインド
    keys = {a or b for a, b in PLAYERPREFS_KEY_RE.findall(src)}
    if keys:
        root = os.environ.get("CLAUDE_PROJECT_DIR") or find_repo_root(fp)
        ledger_file = os.path.join(root, LEDGER_RELPATH) if root else None
        if ledger_file and os.path.isfile(ledger_file):
            try:
                with open(ledger_file, encoding="utf-8") as f:
                    ledger = f.read()
                unknown = sorted(k for k in keys if k and f"`{k}`" not in ledger and k not in ledger)
                if unknown:
                    warnings.append(
                        f"台帳に無い PlayerPrefs キーを検出: {', '.join(unknown)} → "
                        ".claude/rules/save-compat-ledger.md に追記すること (アップデート差し替え互換)"
                    )
            except OSError:
                pass

    # ⑤ C# 9 超の構文 (Unity 2022/6 互換制約、Core.Tests.csproj で LangVersion 9.0 固定)
    if CSHARP10_RE.search(src):
        warnings.append(
            "C# 10+ の構文らしき記述を検出 (file-scoped namespace / global using / record struct / required)。"
            "本プロジェクトは C# 9.0 固定 (unity-conventions.md)"
        )

    emit_warnings(warnings, name)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        sys.exit(0)  # hook 自体の不具合で作業を止めない
