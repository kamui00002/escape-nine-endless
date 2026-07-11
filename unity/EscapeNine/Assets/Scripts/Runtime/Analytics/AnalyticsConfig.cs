// AnalyticsConfig.cs
// PostHog REST 送信先の設定値。キー本体は AnalyticsSecrets (gitignore済み) から取得する
// (Facebook App Keys / PostHogAPIKey と同じ「実値は本ファイルに書かない」方針、
//  feedback_no_hardcode_secrets 準拠)。

namespace EscapeNine.Runtime.Analytics
{
    public static class AnalyticsConfig
    {
        /// <summary>PostHog Cloud (US region) の capture エンドポイント host。project 467042 (Swift と共有)。</summary>
        public const string PostHogHost = "https://us.i.posthog.com";

        /// <summary>PostHog Project API Key。実値は AnalyticsSecrets.cs (gitignore済み、実装者が配置) を参照する。</summary>
        public static string ProjectKey => AnalyticsSecrets.PostHogProjectKey;
    }
}
