# Security guidance（iOS / Firebase / Swift）

security-guidance プラグインのモデルレビューが参照する脅威モデル・チェックリスト。
このリポジトリのコード変更をレビューする際、以下に照らして findings を出すこと。

## Firestore / Firebase 認可
- Firestore の読み書きは必ず認証ユーザーの `uid` で絞る。他ユーザーの uid のドキュメントを read/write できる経路（IDOR）を作らない。
- Security Rules 任せにせず、クライアント側でも uid 照合を行う（多層防御）。
- `signInAnonymously` 等の認証失敗を握りつぶさない（サイレントフェイル禁止）。失敗時はリトライ + 機能制限 + ユーザー通知のいずれかで誠実に扱う。

## PII / ログ
- email / 実名 / 電話番号 / トークン / uid を `os.Logger` / `print` / `Analytics.logEvent` に出さない。Swift ログは `privacy: .private` を使い、Analytics は内部 ID のみ。

## 同意 / トラッキング（ATT / GDPR）
- `Analytics.setConsent` をユーザーが ATT に答える前に `.granted` にしない（事前同意原則）。起動時は `.denied`、ATT 許可後に `.granted` へ切替。

## Secrets
- API キー・トークンをソースにハードコードしない（`AIza...` / `sk-...` / `GOCSPX-...` / `AKIA...`）。Keychain / secret 管理経由で読む。

## Swift 安全性
- 強制アンラップ `!` / `as!` / `try!` を避ける（クラッシュ要因）。`guard let` / `as?` / `do-catch` を使う。
- `catch {}` で握りつぶさない（具体的なエラー処理を）。

## 一般
- injection / SSRF / 安全でないデシリアライズ / DOM injection（WebView の `innerHTML` 等）に注意。
