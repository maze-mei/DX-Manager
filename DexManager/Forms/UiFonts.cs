using System.Drawing;

namespace DexManager.Forms
{
    internal static class UiFonts
    {
        private const string FamilyName = "Malgun Gothic";

        public static Font Create(
            float size,
            FontStyle style = FontStyle.Regular)
        {
            return new Font(FamilyName, size, style);
        }
    }
}
