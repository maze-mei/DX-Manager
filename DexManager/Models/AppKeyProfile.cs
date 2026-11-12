using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace DexManager.Models
{
    public enum AppEnterRule { Default = 0, ShiftEnter = 1 }

    [DataContract]
    public sealed class AppKeyProfile
    {
        [DataMember(Order = 1)] public string PackageName { get; set; }
        [DataMember(Order = 2)] public string DisplayName { get; set; }
        [DataMember(Order = 3)] public bool Enabled { get; set; }
        [DataMember(Order = 4)] public AppEnterRule EnterRule { get; set; }
        [DataMember(Order = 5, EmitDefaultValue = false)] public string WindowStateClass { get; set; }
        [DataMember(Order = 6, EmitDefaultValue = false)] public string WindowTitle { get; set; }

        public static List<AppKeyProfile> CreateDefaults()
        {
            return new List<AppKeyProfile>
            {
                Profile("com.openai.chatgpt", "ChatGPT"),
                CreateClaudeDefault(),
                Profile("com.google.android.apps.bard", "Gemini"),
                Profile("ai.x.grok", "Grok"),
                CreateGeminiWebAppDefault()
            };
        }

        internal static AppKeyProfile CreateClaudeDefault()
        {
            return Profile("com.anthropic.claude", "Claude");
        }

        public static AppKeyProfile CreateGeminiWebAppDefault()
        {
            var profile = Profile("com.android.chrome", "Gemini WebAPK");
            profile.WindowStateClass = "org.chromium.chrome.browser.webapps.SameTaskWebApkActivity";
            profile.WindowTitle = "Google Gemini";
            return profile;
        }

        public bool HasSameTarget(AppKeyProfile other)
        {
            return other != null && string.Equals(PackageName, other.PackageName, StringComparison.Ordinal) &&
                string.Equals(WindowStateClass ?? "", other.WindowStateClass ?? "", StringComparison.Ordinal) &&
                string.Equals(NormalizeWindowTitle(WindowTitle), NormalizeWindowTitle(other.WindowTitle),
                    StringComparison.Ordinal);
        }

        // Direction marks affect presentation, not the window's app identity.
        // Normalize once when receiving state or compiling profiles, never on a key event.
        public static string NormalizeWindowTitle(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var result = new StringBuilder(value.Length);
            foreach (var c in value)
                if (c != '\u061C' && c != '\u200E' && c != '\u200F' &&
                    !(c >= '\u202A' && c <= '\u202E') && !(c >= '\u2066' && c <= '\u2069'))
                    result.Append(c);
            return result.ToString().Trim();
        }

        private static AppKeyProfile Profile(string package, string name)
        {
            return new AppKeyProfile
            {
                PackageName = package, DisplayName = name,
                Enabled = true, EnterRule = AppEnterRule.ShiftEnter
            };
        }
    }
}
