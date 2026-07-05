using System.Drawing;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal static class UiWindowStyle
    {
        private static readonly Size StandardClientSize =
            new Size(920, 696);

        public static void ApplyFixedStandardSize(Form form)
        {
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.SizeGripStyle = SizeGripStyle.Hide;
            form.ClientSize = StandardClientSize;
        }
    }
}
