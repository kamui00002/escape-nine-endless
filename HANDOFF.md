# Meta Ads キャンペーン作成 - 引継ぎ ☁️

> Discord Bot セッションから引き継いだ作業。続きはここから。

## 現状（2026-05-01 時点）

- ✅ meta-ads CLI インストール済み（uv tool）
- ✅ `.env` に Meta 認証情報 4件設定済み（ACCESS_TOKEN / AD_ACCOUNT_ID / PAGE_ID / API_VERSION=v25.0）
- ✅ `ads/campaign-template.yaml` 作成済み（予算 $10/日、PAUSED、JP iOS、音ゲーファン 15-35）
- ✅ `ads/brand-profile.json` / `ads/campaign-brief.md` 作成済み
- ✅ `ad-assets/meta/feed-1080x1080-v1.png` プレースホルダー画像あり
- ✅ dry-run 通過済み（act_153606124398210/... が正しく出力）
- ⏳ App Store URL 更新が必要：`https://apps.apple.com/jp/app/escape-nine-endless/id6760906738`
- ⏳ **本物の広告画像を生成して差し替え**（最優先タスク）
- ⏳ 本番実行（PAUSED で作成）

## 残タスク（順番に実行）

### A. App Store URL 更新

```bash
sed -i.bak2 's|escape-nine-endless"|escape-nine-endless/id6760906738"|' ads/campaign-template.yaml
grep link ads/campaign-template.yaml
```

### B. 本物の広告画像を生成（最優先）

`ads/campaign-brief.md` に 3 コンセプト分のビジュアル指示があるはず。

画像生成方法の選択肢：

1. banana-claude / banana MCP — 手元にあれば一発
2. visual-designer サブエージェント — Agent ツールで起動可能
3. 手動: Figma / Canva で 1080×1080 を 3 種作って `ad-assets/meta/` に配置

完成した画像を `feed-1080x1080-v1.png` を上書きするか、YAML の `image:` を新ファイル名に変更。

### C. 環境変数を読み込んで dry-run 再確認

```bash
cd ~/Documents/GitHub/escape-nine-endless
set -a && source .env && set +a
echo "TOKEN: ${#META_ACCESS_TOKEN}, ACCOUNT: ${#META_AD_ACCOUNT_ID}, PAGE: ${#META_PAGE_ID}"
meta-ads create --config ads/campaign-template.yaml --dry-run
```

### D. 本番実行（PAUSED で作成）

```bash
meta-ads create --config ads/campaign-template.yaml --yes
```

`status: PAUSED` のまま作成 → Ads Manager で確認 → 手動 ACTIVE 化。

## 参考情報

- `AD_ACCOUNT_ID` には `act_` を付けない（CLI が自動で付ける）
- `daily_budget` は cents 単位（$10/日 = 1000、$30/日 = 3000）
- API バージョンは v25.0 必須（v21.0 は deprecated）
- meta-ads CLI ソース: `/Users/yoshidometoru/.local/share/uv/tools/meta-ads-cli/lib/python3.11/site-packages/meta_ads/`

## ParkPedia は別途同じ手順を踏む必要あり

`~/Documents/GitHub/ParkPedia/` 配下に同等構成あり。`.env` 設定からやり直し。
