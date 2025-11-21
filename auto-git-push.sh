#!/bin/bash

# サブエージェント更新の自動Git監視スクリプト
# 使い方: ./auto-git-push.sh

cd /Users/yoshidometoru/Documents/GitHub/escape-nine-endless/EscapeNine-endless-

echo "🤖 サブエージェント更新監視を開始します..."
echo "変更を検知したら自動でコミット&プッシュします"
echo "Ctrl+C で停止"
echo ""

while true; do
    # 変更があるかチェック
    if git status --porcelain | grep -q '^'; then
        echo "📝 変更を検知しました"

        # 変更内容を表示
        git status --short
        echo ""

        # すべての変更をステージング
        git add -A

        # コミット
        git commit -m "$(cat <<'EOF'
chore: サブエージェントによる自動更新

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"

        # プッシュ
        echo "📤 プッシュ中..."
        git push origin main

        echo "✅ コミット&プッシュ完了"
        echo "次の変更を監視します..."
        echo ""
    fi

    # 30秒待機
    sleep 30
done
