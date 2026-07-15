#!/usr/bin/env python3
"""PostToolUse hook: 分析イベント名の iOS↔Unity パリティ監視 ☀️

対象は AnalyticsEvents.swift / AnalyticsService.cs の 2 ファイルのみ (他は no-op)。
両ファイルから eg_* イベント名を抽出して突合し、unity/PARITY_GAPS.md (allowlist) に
載っていない片側イベントを警告する。常に exit 0 (ブロックしない)。
警告は stdout の hookSpecificOutput.additionalContext JSON で Claude に届ける
(exit 0 の stderr は Claude に届かないため。/review-full 2026-07-15 C1)。
根拠: .claude/rules/analytics-parity.md
"""
from __future__ import annotations

import json
import os
import re
import sys

IOS_RELPATH = "EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsEvents.swift"
UNITY_RELPATH = "unity/EscapeNine/Assets/Scripts/Runtime/Analytics/AnalyticsService.cs"
ALLOWLIST_RELPATH = "unity/PARITY_GAPS.md"
TARGET_BASENAMES = {"AnalyticsEvents.swift", "AnalyticsService.cs"}
EVENT_RE = re.compile(r'"(eg_[a-z0-9_]+)"')
# allowlist は Markdown 中の裸トークン/バッククォート表記も拾えるよう境界一致で抽出
ALLOW_EVENT_RE = re.compile(r"\beg_[a-z0-9_]+\b")


def find_repo_root(path: str) -> str | None:
    cur = os.path.dirname(os.path.abspath(path))
    for _ in range(12):
        if os.path.isfile(os.path.join(cur, IOS_RELPATH)) and os.path.isfile(os.path.join(cur, UNITY_RELPATH)):
            return cur
        parent = os.path.dirname(cur)
        if parent == cur:
            return None
        cur = parent
    return None


def events_of(path: str) -> set[str] | None:
    """読み込み失敗のみ None。正常に読めた空集合は「イベント全削除」として比較対象にする
    (/review-full 2026-07-15 C6: 空集合を失敗と同一視すると最大のパリティ崩壊が無警告になる)"""
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return set(EVENT_RE.findall(f.read()))
    except OSError:
        return None


def main() -> int:
    try:
        data = json.load(sys.stdin)
    except Exception:
        return 0
    fp = (data.get("tool_input") or {}).get("file_path") or ""
    if os.path.basename(fp) not in TARGET_BASENAMES:
        return 0  # 非対象は no-op

    root = os.environ.get("CLAUDE_PROJECT_DIR") or find_repo_root(fp)
    if not root:
        return 0
    ios = events_of(os.path.join(root, IOS_RELPATH))
    unity = events_of(os.path.join(root, UNITY_RELPATH))
    if ios is None or unity is None:
        return 0  # ファイルが読めない時のみスキップ

    # allowlist はイベント名集合として抽出し完全一致で照合
    # (/review-full 2026-07-15 C7: 全文部分一致だと eg_game が eg_game_started に素通りする)
    allow_set: set[str] = set()
    try:
        with open(os.path.join(root, ALLOWLIST_RELPATH), encoding="utf-8") as f:
            allow_set = set(ALLOW_EVENT_RE.findall(f.read()))
    except OSError:
        pass

    warnings: list[str] = []
    for only, side in ((ios - unity, "iOS"), (unity - ios, "Unity")):
        missing = sorted(e for e in only if e not in allow_set)
        if missing:
            warnings.append(
                f"{side} のみに存在するイベント: {', '.join(missing)} → "
                "ペア追加するか unity/PARITY_GAPS.md に理由を記載 (.claude/rules/analytics-parity.md)"
            )

    if warnings:
        text = "\n".join(f"[check_analytics_parity] WARN: {w}" for w in warnings)
        print(json.dumps(
            {"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": text}},
            ensure_ascii=False,
        ))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        sys.exit(0)  # hook 自体の不具合で作業を止めない
