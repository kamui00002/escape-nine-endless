#!/bin/zsh
# unity/setup/ios-device-deploy.sh ☀️
# Unity 版 Escape Nine を iPhone 実機へ CLI batchmode で「ビルド→署名→install→launch」する。
#
# フロー: ミラー (~/EscapeNineUnity) を対象に
#   1) Unity CLI batchmode (-executeMethod BuildScripts.BuildIOS) で Xcode プロジェクト再生成
#   2) xcodebuild で実機ビルド (自動プロビジョニング署名)
#   3) devicectl で install + launch
# 各段の成否は結果マーカー ($OUT = ios-redeploy3-result.txt) に追記する
# (呼び出し側の MCP/セッションがタイムアウトしても、ファイルを見れば成否と失敗ステージが分かる)。
#
# 前提:
#   - Unity Editor を閉じておくこと (同一プロジェクトを batchmode と Editor が同時に開くとロック競合)。
#     MCP (対話 Editor) が落ちていても本スクリプトは動く (Editor 常駐は不要)。
#   - 実機が USB/近接で devicectl から見えていること。
#
# 元は前セッションの scratchpad に置かれ揮発リスクがあったため、2026-07-10 に repo へ恒久化。
# 環境依存値は環境変数で上書き可能 (デフォルトは現行オーナー環境の実値)。

set -o pipefail

MIRROR="${ESCAPENINE_UNITY_MIRROR:-/Users/yoshidometoru/EscapeNineUnity}"
DEV="${ESCAPENINE_IOS_UDID:-00008150-001A11EA3441401C}"
BID="${ESCAPENINE_IOS_BUNDLE:-com.yoshidometoru.escapenine.unity}"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity}"
IOSDIR="$MIRROR/Builds/ios"
OUT="$MIRROR/ios-redeploy3-result.txt"

: > "$OUT"

# 1) Unity iOS ビルド (Xcode プロジェクト再生成)
"$UNITY" -batchmode -quit -projectPath "$MIRROR" \
  -buildTarget iOS -executeMethod EscapeNine.EditorTools.BuildScripts.BuildIOS \
  -logFile "$MIRROR/build-ios-cli3.log"
if ! grep -q "result=Succeeded" "$MIRROR/build-ios-result.txt" 2>/dev/null; then
  echo "result=FAIL stage=unity_build" >> "$OUT"
  cat "$MIRROR/build-ios-result.txt" 2>/dev/null >> "$OUT"
  exit 0
fi

# 2) 実機ビルド (署名は自動プロビジョニング)
cd "$IOSDIR" || { echo "result=FAIL stage=cd_iosdir" >> "$OUT"; exit 0; }
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -destination "id=$DEV" -derivedDataPath ./DerivedData -allowProvisioningUpdates build \
  > "$MIRROR/xcodebuild-device3.log" 2>&1
if ! grep -q "BUILD SUCCEEDED" "$MIRROR/xcodebuild-device3.log"; then
  echo "result=FAIL stage=xcodebuild" >> "$OUT"
  grep -iE "error:|Code Sign|BUILD FAILED" "$MIRROR/xcodebuild-device3.log" | tail -12 >> "$OUT"
  exit 0
fi

APP=$(ls -d "$IOSDIR"/DerivedData/Build/Products/Release-iphoneos/*.app 2>/dev/null | head -1)

# 3) install + launch
xcrun devicectl device install app --device "$DEV" "$APP" \
  > "$MIRROR/ios-install3.log" 2>&1 \
  || { echo "result=FAIL stage=install" >> "$OUT"; tail -5 "$MIRROR/ios-install3.log" >> "$OUT"; exit 0; }
xcrun devicectl device process launch --device "$DEV" --terminate-existing "$BID" \
  > "$MIRROR/ios-launch3.log" 2>&1
echo "result=SUCCESS app=$APP" >> "$OUT"
