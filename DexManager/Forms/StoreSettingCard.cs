using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DexManager.Forms
{
    internal sealed class StoreSettingCard : Panel
    {
        public readonly RoundedPanel HeaderSurface;
        public readonly FlowLayoutPanel ChildrenPanel;
        public readonly Label TitleLabel, DescriptionLabel;
        public readonly Panel EditorHost;
        private readonly SymbolView _icon;
        private readonly ThemedButton _expand;
        private readonly bool _nested, _expander;
        private bool _expanded;
        private bool _reflowing;
        public readonly List<Control> Editors = new List<Control>();
        private readonly Dictionary<Control, Rectangle> _editorBounds = new Dictionary<Control, Rectangle>();
        public void RegisterEditorBounds(Control editor) { _editorBounds[editor] = editor.Bounds; }

        public StoreSettingCard(string title, string description, UiSymbol symbol, bool nested, bool expander)
        {
            _nested=nested; _expander=expander;
            BackColor=Color.Transparent;
            Margin=new Padding(nested?14:0,0,0,nested?0:8);
            HeaderSurface=new RoundedPanel {Radius=nested?0:4,VerticalInset=nested?0:1,BackColor=Color.Transparent};
            TitleLabel=new UiLabel {Text=title,Font=UiFonts.Pixels(14),BackColor=Color.Transparent,Tag="primary"};
            DescriptionLabel=new UiLabel {Text=description,Font=UiFonts.Pixels(12),BackColor=Color.Transparent,AutoEllipsis=true};
            _icon=new SymbolView {Symbol=symbol,Visible=!nested};
            EditorHost=new Panel {BackColor=Color.Transparent};
            _expand=new ThemedButton {Text="",Symbol=UiSymbol.Chevron,NavigationStyle=true,Visible=expander,TabStop=true,Font=UiFonts.Pixels(14),CornerRadius=4};
            _expand.Click+=delegate {Expanded=!Expanded;};
            HeaderSurface.Controls.AddRange(new Control[]{TitleLabel,DescriptionLabel,_icon,EditorHost,_expand});
            ChildrenPanel=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent,Visible=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink};
            // Keep the group edge spacing after moving the row insets out of the gaps.
            ChildrenPanel.Padding=new Padding(0,1,0,9);
            Controls.AddRange(new Control[]{HeaderSurface,ChildrenPanel});
            SizeChanged+=delegate {Reflow();}; ChildrenPanel.SizeChanged+=delegate {Reflow();};
        }
        public bool Expanded {
            get {return _expanded;}
            set {_expanded=value;ChildrenPanel.Visible=value;_expand.ChevronUp=value;Reflow();Parent?.PerformLayout();}
        }
        public int EditorWidth {get;set;}
        public int EditorHeight {get;set;}=40;
        public bool WideContent {get;set;}
        public void Reflow()
        {
            if(Width<120 || HeaderSurface==null || _reflowing) return;
            _reflowing=true;
            try {
            if(WideContent) {
                int bodyHeight=0;
                foreach(Control editor in EditorHost.Controls) {
                    editor.Width=Math.Max(100,Width-36-editor.Left);
                    if(editor is Label)editor.Height=editor.GetPreferredSize(new Size(editor.Width,0)).Height;
                    bodyHeight=Math.Max(bodyHeight,editor.Bottom);
                }
                EditorHeight=Math.Max(22,bodyHeight);
            }
            int left=_nested?18:56;
            int right=_expander?58:18;
            bool stacked=WideContent || Width<left+EditorWidth+right+118;
            int textWidth=stacked?Width-left-20:Math.Max(100,Width-left-EditorWidth-right-18);
            var cardId=Tag as string;
            if(cardId=="DiagnosticsDeviceCard" || (WideContent && cardId=="DiagnosticsEnvironmentCard"))
                textWidth=Math.Max(100,textWidth-178);
            int titleHeight=Math.Max(22,TitleLabel.GetPreferredSize(new Size(textWidth,0)).Height);
            int descHeight=string.IsNullOrWhiteSpace(DescriptionLabel.Text)?0:Math.Max(18,DescriptionLabel.GetPreferredSize(new Size(textWidth,0)).Height);
            int topHeight=Math.Max(68,16+titleHeight+descHeight+14);
            if(!stacked)topHeight=Math.Max(topHeight,EditorHeight+28);
            int headerHeight=stacked?topHeight+EditorHeight+12:topHeight;
            HeaderSurface.Bounds=new Rectangle(0,0,Width,headerHeight);
            _icon.Bounds=new Rectangle(18,(topHeight-22)/2,22,22);
            TitleLabel.Bounds=new Rectangle(left,descHeight==0?(topHeight-titleHeight)/2:14,textWidth,titleHeight);
            DescriptionLabel.Bounds=new Rectangle(left,16+titleHeight,textWidth,descHeight);
            EditorHost.Bounds=stacked?new Rectangle(18,topHeight,Width-36,EditorHeight):new Rectangle(Width-EditorWidth-right,(topHeight-EditorHeight)/2,EditorWidth,EditorHeight);
            if(!WideContent)foreach(var editor in _editorBounds) if(!editor.Key.IsDisposed)editor.Key.Bounds=editor.Value;
            _expand.Bounds=new Rectangle(Width-48,(topHeight-40)/2,40,40);
            ChildrenPanel.Location=new Point(0,headerHeight+1);ChildrenPanel.Width=Width;
            foreach(Control child in ChildrenPanel.Controls)child.Width=Width-14;
            Height=headerHeight+(Expanded?ChildrenPanel.PreferredSize.Height+1:0);
            } finally {_reflowing=false;}
        }
        public void ApplyTheme()
        {
            var t=ThemeColors.Current;
            HeaderSurface.FillColor=_nested?(t.IsDark?Color.FromArgb(42,47,62):Color.FromArgb(251,252,255)):t.CardBackground;
            HeaderSurface.BorderColor=t.CardBorder;
            _expand.BackColor=HeaderSurface.FillColor;
            TitleLabel.ForeColor=t.TextPrimary;DescriptionLabel.ForeColor=t.TextSecondary;_icon.ForeColor=t.TextPrimary;
            foreach(Control editor in EditorHost.Controls){editor.BackColor=HeaderSurface.FillColor;editor.ForeColor=t.TextPrimary;editor.Invalidate();}
            Invalidate(true);
        }
    }
}
