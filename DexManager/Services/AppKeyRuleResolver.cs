using System;
using System.Collections.Generic;
using DexManager.Models;

namespace DexManager.Services
{
    public sealed class AppKeyRuleResolver
    {
        private sealed class Rule
        {
            public string StateClass, Title;
            public AppEnterRule Enter;
        }

        private readonly Dictionary<string, List<Rule>> _rules =
            new Dictionary<string, List<Rule>>(StringComparer.Ordinal);

        public AppKeyRuleResolver(IEnumerable<AppKeyProfile> profiles)
        {
            if (profiles == null) return;
            foreach (var profile in profiles)
            {
                if (profile == null || !profile.Enabled ||
                    !IsPackageName(profile.PackageName) ||
                    !Enum.IsDefined(typeof(AppEnterRule), profile.EnterRule) ||
                    !IsSelector(profile.WindowStateClass) || !IsSelector(profile.WindowTitle))
                    continue;
                var title = AppKeyProfile.NormalizeWindowTitle(profile.WindowTitle);
                if (!string.IsNullOrEmpty(profile.WindowTitle) && title.Length == 0) continue;
                List<Rule> rules;
                if (!_rules.TryGetValue(profile.PackageName, out rules))
                    _rules.Add(profile.PackageName, rules = new List<Rule>());
                // First matching enabled profile wins, including an explicit Default rule.
                // Keep multiple window targets belonging to the same browser package.
                rules.Add(new Rule { StateClass = profile.WindowStateClass ?? "",
                    Title = title, Enter = profile.EnterRule });
            }
        }

        public bool ShouldConvertEnter(string package, bool shift, bool control,
            bool alt, bool windows)
        {
            return ShouldConvertEnter(package, "", "", shift, control, alt, windows);
        }

        public bool ShouldConvertEnter(ForegroundAppIdentity app, bool shift, bool control,
            bool alt, bool windows)
        {
            return app != null && ShouldConvertEnter(app.PackageName, app.WindowStateClass,
                app.WindowTitle, shift, control, alt, windows);
        }

        private bool ShouldConvertEnter(string package, string stateClass, string title,
            bool shift, bool control, bool alt, bool windows)
        {
            if (shift || control || alt || windows || string.IsNullOrEmpty(package))
                return false;
            List<Rule> rules;
            if (!_rules.TryGetValue(package, out rules)) return false;
            foreach (var rule in rules)
                if ((rule.StateClass.Length == 0 || rule.StateClass == stateClass) &&
                    (rule.Title.Length == 0 || rule.Title == title))
                    return rule.Enter == AppEnterRule.ShiftEnter;
            return false;
        }

        private static bool IsSelector(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;
            if (value.Length > 512 || string.IsNullOrWhiteSpace(value)) return false;
            foreach (var c in value)
                if (char.IsControl(c)) return false;
            return true;
        }

        public static bool IsPackageName(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 255) return false;
            bool hasDot = false, segmentStart = true;
            foreach (var c in value)
            {
                if (c == '.')
                {
                    if (segmentStart) return false;
                    hasDot = true; segmentStart = true; continue;
                }
                bool letter = c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
                if (!letter && c != '_' && !(c >= '0' && c <= '9' && !segmentStart))
                    return false;
                segmentStart = false;
            }
            return hasDot && !segmentStart;
        }
    }
}
