# Meta Ads パフォーマンス分析レポート（中断）

**実行日**: 2026-05-04（自動スケジュール起動）
**対象**: Campaign ID `120243998737650344` — EscapeNine - App Install [2026-05]
**ステータス**: 中断 — Meta アクセストークン期限切れ

## 状況

```
API Error: Error validating access token:
Session has expired on Thursday, 30-Apr-26 11:00:00 PDT.
```

Graph API Explorer で発行した短命トークン（1〜2時間有効）が失効。
全ての insights 取得が不可能な状態です。

## 必要なアクション（ユーザー作業）

1. https://developers.facebook.com/tools/explorer/ を開く
2. アプリ「Escape Nine」を選択
3. 以下の権限で **Generate Access Token**:
   - `ads_management`
   - `ads_read`
   - `pages_manage_ads`
   - `pages_show_list`
   - `pages_read_engagement`
   - `business_management`
4. 新しいトークンで `ads/.env` の `META_ACCESS_TOKEN` を更新
5. 再分析を実行

## 再開コマンド

トークン更新後、Claude に「分析を再開して」と伝えてください。以下が実行されます：

```bash
# 1. キャンペーン全体ステータス
meta-ads status 120243998737650344

# 2. 各広告の insights 取得
# - Ad 120243998745800344 (Concept1 やめられない)
# - Ad 120243998753610344 (Concept2 ビートに挑戦)
# - Ad 120243998757030344 (Concept3 逃げろ)
# 指標: impressions, clicks, ctr, cpc, spend, actions

# 3. 結果分析 → 勝ちコンセプト特定 → バリエーション3案を campaign-brief.md に追記
```

## 補足

- 5/1〜5/4 の3日間で最大 ¥3,000 相当の配信実績が蓄積されているはず
- ただし審査が長期化していた場合、実配信日数は1〜2日のみの可能性あり
- 永続トークン（System User Token）への切り替えを検討すると、次回以降この問題は回避可能
