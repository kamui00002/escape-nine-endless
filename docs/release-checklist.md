# リリース済み機能チェックリスト ⭐️

Escape Nine: Endless の実装・外部設定の進捗サマリ。

---

## 完了（コード実装済み）

- [x] 基本ゲームロジック（移動・当たり判定・同時移動）
- [x] 3x3グリッドUI + レスポンシブ対応
- [x] キャラクター選択画面 + 4キャラスキル
- [x] AI（Easy/Normal/Hard）+ 階層スケーリング
- [x] BPMべき乗曲線（70→200）
- [x] 階層システム + 特殊ルール（霧/消失/複合）
- [x] ターンカウントダウンシステム（3→2→1 → 移動）
- [x] ゲーム開始カウントダウン（3→2→1→GO!）
- [x] 敗因表示（捕まった/時間切れ）
- [x] メトロノームビートシステム（AVAudioEngine）
- [x] 効果音8種（.wav）
- [x] 設定画面（BGM/効果音音量分離）
- [x] ランキング（ローカル永続化）
- [x] 実績システム（9種）
- [x] チュートリアル6ページ（図解付き）
- [x] ショップ画面
- [x] StoreKit 2（ローカルテスト設定済み）
- [x] Game Center連携（コード実装済み）
- [x] Firebase/AdMob（モック実装・本番切替準備済み）
- [x] デバッグ管理システム（開始階層/BPM/AI/カウントダウン設定）
- [x] 定数一元管理（Constants.swift）

---

## 外部設定が必要（コードだけでは完了不可） — 完了済み

- [x] Firebase: GoogleService-Info.plist配置済み + Firestore Rules デプロイ済み
- [x] AdMob: 本番広告ID設定済み（バナー+インタースティシャル）
- [x] App Store Connect: StoreKit 4商品登録済み（wizard/elf/knight/removeads）
- [x] Game Center: リーダーボードID登録済み（highestfloor）
- [x] BGM音楽ファイル（6曲生成済み: menu/early/mid/late/clear/gameover）
- [x] Sign in with Apple: entitlements + Firebase Auth 有効化済み

---

## App Store 提出 — 完了済み

- [x] アプリアイコン（1024x1024）
- [x] スクリーンショット（6.7", 6.5", 5.5"）
- [x] App内課金の審査用スクリーンショット（4商品分）
- [x] アプリ説明文、キーワード
- [x] プライバシーポリシーURL
- [x] 年齢レーティング設定

> アプリストア提出用メタデータは `appstore-metadata.md` を参照。
