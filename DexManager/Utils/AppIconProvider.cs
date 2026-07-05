using System;
using System.Drawing;
using System.Windows.Forms;

namespace DexManager.Utils
{
    internal static class AppIconProvider
    {
        private static readonly Icon ApplicationIcon = LoadIcon();

        public static Icon Current
        {
            get { return ApplicationIcon; }
        }

        private static Icon LoadIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(
                    Application.ExecutablePath) ??
                    SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }
}
