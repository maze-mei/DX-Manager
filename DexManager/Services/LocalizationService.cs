using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using DexManager.Models;

namespace DexManager.Services
{
    public static class LocalizationService
    {
        private static readonly ResourceManager Resources =
            new ResourceManager(
                "DexManager.Resources.Strings",
                Assembly.GetExecutingAssembly());

        private static CultureInfo _culture = GetAutomaticCulture();

        public static CultureInfo Culture
        {
            get { return _culture; }
        }

        public static bool IsKorean
        {
            get
            {
                return string.Equals(
                    _culture.TwoLetterISOLanguageName,
                    "ko",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Apply(AppLanguage language)
        {
            if (language == AppLanguage.Korean)
                _culture = CultureInfo.GetCultureInfo("ko");
            else if (language == AppLanguage.English)
                _culture = CultureInfo.GetCultureInfo("en");
            else
                _culture = GetAutomaticCulture();

            CultureInfo.CurrentUICulture = _culture;
        }

        public static string Get(string key)
        {
            var value = Resources.GetString(key, _culture);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        public static string Format(string key, params object[] values)
        {
            return string.Format(_culture, Get(key), values);
        }

        public static string GetLanguageName(AppLanguage language)
        {
            if (language == AppLanguage.Korean)
                return Get("Language.Korean");
            if (language == AppLanguage.English)
                return Get("Language.English");
            return Get("Language.Auto");
        }

        private static CultureInfo GetAutomaticCulture()
        {
            var current = CultureInfo.CurrentUICulture;
            return string.Equals(
                current.TwoLetterISOLanguageName,
                "ko",
                StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("ko")
                : CultureInfo.GetCultureInfo("en");
        }
    }
}
