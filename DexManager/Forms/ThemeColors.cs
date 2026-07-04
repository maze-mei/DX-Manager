using System.Drawing;
using Microsoft.Win32;
using DexManager.Models;

namespace DexManager.Forms
{
    internal sealed class ThemePalette
    {
        public bool IsDark { get; set; }
        public Color WindowBackground { get; set; }
        public Color NavigationBackground { get; set; }
        public Color CardBackground { get; set; }
        public Color CardSoft { get; set; }
        public Color CardBorder { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color TextTertiary { get; set; }
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentPressed { get; set; }
        public Color AccentSoft { get; set; }
        public Color ControlBorder { get; set; }
        public Color DisabledBackground { get; set; }
        public Color DisabledText { get; set; }
    }

    internal static class ThemeColors
    {
        private static ThemePalette _current = CreateLight();

        public static ThemePalette Current
        {
            get { return _current; }
        }

        public static ThemePalette Use(AppTheme theme)
        {
            _current = Resolve(theme);
            return _current;
        }

        public static ThemePalette Resolve(AppTheme theme)
        {
            var dark = theme == AppTheme.Dark ||
                (theme == AppTheme.Auto && IsWindowsDarkMode());
            return dark ? CreateDark() : CreateLight();
        }

        private static bool IsWindowsDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key == null
                        ? null
                        : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static ThemePalette CreateLight()
        {
            return new ThemePalette
            {
                IsDark = false,
                WindowBackground = Color.FromArgb(243, 243, 243),
                NavigationBackground = Color.FromArgb(249, 249, 250),
                CardBackground = Color.White,
                CardSoft = Color.FromArgb(251, 251, 251),
                CardBorder = Color.FromArgb(232, 232, 234),
                TextPrimary = Color.FromArgb(27, 27, 27),
                TextSecondary = Color.FromArgb(92, 92, 92),
                TextTertiary = Color.FromArgb(128, 128, 134),
                Accent = Color.FromArgb(111, 95, 214),
                AccentHover = Color.FromArgb(125, 110, 224),
                AccentPressed = Color.FromArgb(92, 78, 194),
                AccentSoft = Color.FromArgb(239, 236, 252),
                ControlBorder = Color.FromArgb(211, 211, 214),
                DisabledBackground = Color.FromArgb(241, 241, 243),
                DisabledText = Color.FromArgb(156, 156, 162)
            };
        }

        private static ThemePalette CreateDark()
        {
            return new ThemePalette
            {
                IsDark = true,
                WindowBackground = Color.FromArgb(30, 30, 32),
                NavigationBackground = Color.FromArgb(38, 38, 42),
                CardBackground = Color.FromArgb(35, 35, 38),
                CardSoft = Color.FromArgb(40, 40, 44),
                CardBorder = Color.FromArgb(55, 55, 60),
                TextPrimary = Color.FromArgb(242, 242, 243),
                TextSecondary = Color.FromArgb(200, 200, 204),
                TextTertiary = Color.FromArgb(158, 158, 164),
                Accent = Color.FromArgb(139, 123, 240),
                AccentHover = Color.FromArgb(156, 142, 244),
                AccentPressed = Color.FromArgb(111, 95, 214),
                AccentSoft = Color.FromArgb(56, 50, 84),
                ControlBorder = Color.FromArgb(65, 65, 70),
                DisabledBackground = Color.FromArgb(47, 47, 51),
                DisabledText = Color.FromArgb(112, 112, 118)
            };
        }
    }
}
