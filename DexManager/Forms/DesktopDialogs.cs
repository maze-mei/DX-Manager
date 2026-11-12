using System;
using System.Drawing;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.Forms
{
    internal static class DesktopDialogs
    {
        public static DialogResult Show(string text, string title, MessageBoxButtons buttons=MessageBoxButtons.OK,
            MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1)
        {
            return Show(null,text,title,buttons,icon,defaultButton);
        }
        public static DialogResult Show(IWin32Window owner, string text, string title,
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            using(var dialog=new DesktopDialog(title,text,false,buttons,defaultButton)) return dialog.ShowDialog(owner);
        }
        public static bool ConfirmFirstRun(Form owner)
        {
            var paragraphs=new[]{"Intro","Display","Power","Recovery","Companion","Independent","Liability"};
            var text=string.Join("\n\n",Array.ConvertAll(paragraphs,key=>StoreUiText.Get("FirstRunDialog"+key,"")));
            using(var shade=new Form {FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,
                BackColor=Color.Black,Opacity=0.35,Bounds=new Rectangle(owner.PointToScreen(Point.Empty),owner.ClientSize)})
            using(var dialog=new DesktopDialog(StoreUiText.Get("FirstRunDialogTitle","Before starting DX Manager"),text,true,MessageBoxButtons.OKCancel,MessageBoxDefaultButton.Button2))
            {
                shade.Show(owner);shade.Enabled=false;
                return dialog.ShowDialog(owner)==DialogResult.OK;
            }
        }
    }

    internal sealed class DesktopDialog : Form
    {
        private readonly RoundedPanel _surface;
        private readonly Button _primaryButton;
        private CheckBox _option;
        public bool OptionChecked {get{return _option!=null&&_option.Checked;}}
        public void SetPrimaryButtonText(string text){_primaryButton.Text=text;}
        public DesktopDialog(string title,string text,bool acknowledgment,MessageBoxButtons buttons,MessageBoxDefaultButton defaultButton,string optionalChoice=null)
        {
            Font=UiFonts.Pixels(14);Icon=AppIconProvider.Current;Text=title;
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            var area=Screen.PrimaryScreen.WorkingArea;
            var width=Math.Min(580,area.Width-48);
            var titleLabel=new UiLabel {Text=title,Font=UiFonts.Pixels(22,true),AutoSize=false,Bounds=new Rectangle(24,22,width-48,36),ForeColor=ThemeColors.Current.TextPrimary,BackColor=Color.Transparent};
            var body=new UiLabel {Text=text,AutoSize=true,MaximumSize=new Size(width-48,0),Font=UiFonts.Pixels(14),ForeColor=ThemeColors.Current.TextPrimary,BackColor=Color.Transparent};
            var preferred=body.GetPreferredSize(new Size(width-48,0));
            int contentHeight=Math.Min(preferred.Height+8,Math.Max(140,area.Height-240));
            ClientSize=new Size(width,Math.Max(230,80+contentHeight+(acknowledgment||optionalChoice!=null?64:0)+76));
            _surface=new RoundedPanel {Dock=DockStyle.Fill,Radius=10,FillColor=ThemeColors.Current.CardBackground,BorderColor=ThemeColors.Current.CardBorder};
            var scroll=new Panel {AutoScroll=true,BackColor=ThemeColors.Current.CardBackground,Bounds=new Rectangle(24,76,width-48,contentHeight)};
            body.Location=Point.Empty;scroll.Controls.Add(body);
            var footer=new Panel {BackColor=ThemeColors.Current.CardSoft,Bounds=new Rectangle(0,ClientSize.Height-76,width,76)};
            string primaryText=acknowledgment?StoreUiText.Get("FirstRunDialogPrimary","Confirm and start"):buttons==MessageBoxButtons.YesNo||buttons==MessageBoxButtons.YesNoCancel?(LocalizationService.IsKorean?"확인":"Yes"):(LocalizationService.IsKorean?"확인":"OK");
            var primary=new ThemedButton {Text=primaryText,Primary=true,Font=UiFonts.Pixels(14,true),Bounds=new Rectangle(24,18,(width-56)/2,40)};
            _primaryButton=primary;
            bool cancel=buttons!=MessageBoxButtons.OK;
            var secondary=new ThemedButton {Text=acknowledgment?StoreUiText.Get("FirstRunDialogExit","Exit"):LocalizationService.Get("Common.Cancel"),Bounds=new Rectangle(32+(width-56)/2,18,(width-56)/2,40)};
            var yes=buttons==MessageBoxButtons.YesNo||buttons==MessageBoxButtons.YesNoCancel;
            primary.DialogResult=yes?DialogResult.Yes:DialogResult.OK;
            secondary.DialogResult=yes?DialogResult.No:DialogResult.Cancel;
            if(!cancel){primary.Bounds=new Rectangle(width-134,18,110,40);secondary.Visible=false;}
            footer.Controls.AddRange(new Control[]{primary,secondary});
            _surface.Controls.AddRange(new Control[]{titleLabel,scroll,footer});Controls.Add(_surface);
            ThemedScrollBar.Attach(scroll);
            CancelButton=secondary;
            AcceptButton=defaultButton==MessageBoxDefaultButton.Button2?secondary:primary;
            if(acknowledgment||optionalChoice!=null)
            {
                var check=new UiCheckBox {Text=optionalChoice??StoreUiText.Get("FirstRunDialogCheck","I have read the above."),Font=UiFonts.Pixels(14),Bounds=new Rectangle(24,80+contentHeight,width-48,40),ForeColor=ThemeColors.Current.TextPrimary,BackColor=ThemeColors.Current.CardBackground};
                _option=check;
                if(acknowledgment){primary.Enabled=false;check.CheckedChanged+=delegate {primary.Enabled=check.Checked;};}
                _surface.Controls.Add(check);
            }
            if(defaultButton==MessageBoxDefaultButton.Button2 && cancel){
                primary.TabIndex=1;secondary.TabIndex=0;
                Shown+=delegate {ActiveControl=secondary;BeginInvoke((Action)delegate {if(!IsDisposed&&secondary.Visible){ActiveControl=secondary;secondary.Focus();}});};
            }
        }
    }
}
