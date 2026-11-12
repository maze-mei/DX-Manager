using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DexManager.Models;
using DexManager.Services;
namespace DexManager.Forms {
 public sealed partial class SettingsForm {
  private readonly Dictionary<string,Control> _storeEditors=new Dictionary<string,Control>();
  private readonly Dictionary<string,StoreSettingCard> _storeCards=new Dictionary<string,StoreSettingCard>();
  private sealed class StoreInputModeOption {
   public readonly KeyInputMode Value; private readonly string _key;
   public StoreInputModeOption(KeyInputMode value,string key){Value=value;_key=key;}
   public override string ToString(){return StoreUiText.Get(_key+".Content",Value.ToString());}
   public override bool Equals(object other){return other is KeyInputMode && (KeyInputMode)other==Value || other is StoreInputModeOption && ((StoreInputModeOption)other).Value==Value;}
   public override int GetHashCode(){return (int)Value;}
  }
  private static KeyInputMode ReadStoreInputMode(ThemedSelectControl control){var value=control.SelectedItem as StoreInputModeOption;return value==null?(control.SelectedItem is KeyInputMode?(KeyInputMode)control.SelectedItem:KeyInputMode.SendInputScanCode):value.Value;}
  private int _storeTargetGeneration;
  private string _storeTargetKey;  private bool _storeUiReady;
  private ThemedSelectControl _enterInputModeBox, _companionDeviceSelector;
  private Label _bundleStateText, _installStateText;
  private readonly Action<bool> _hotkeyEditing;
  private readonly Func<IList<EnvironmentCheckItem>> _runStoreEnvironment;
  private Func<string> _mainSelectedSerial, _mainSelectedIdentity;
  private static ThemedNumberControl StoreNumber(int minimum,int maximum) {
   return new ThemedNumberControl {Minimum=minimum,Maximum=maximum,Value=minimum,CenterText=true,ShowStepButtons=false,Font=UiFonts.Pixels(14),Height=40};
  }
  private static ThemedTextControl StorePath() {return new ThemedTextControl {Height=40,Font=UiFonts.Pixels(14),UseMiddleEllipsis=true};}
  private ThemedButton StoreAction(string uid,string fallback,EventHandler handler) {
   var b=new ThemedButton {Text=StoreUiText.Get(uid+".Content",fallback),Font=UiFonts.Pixels(14),Height=40,CornerRadius=6}; if(handler!=null)b.Click+=handler;return b;
  }
  private ThemedSelectControl _dexDisplayModeBox;
  private CheckBox _nativeBluetoothAudioBox,_native120AutoSwitchBox,_autoSaveLogsBox;
  private void LoadDexDisplayMode() {
   if(_dexDisplayModeBox==null)return;
   var identity=ResolveStoreTargetIdentity();
   var value=string.IsNullOrWhiteSpace(identity)?_settings.VirtualDisplay:_settings.GetOrCreateDeviceRunSettings(identity).VirtualDisplay;
   bool wasLoading=_loadingStoreValues;_loadingStoreValues=true;
   try {
    _dexDisplayModeBox.SelectedIndex=value.ActivationMode==DexDisplayMode.LegacyOverlay?1:0;
    _nativeBluetoothAudioBox.Checked=value.KeepBluetoothAudioOnDexStart;
    _native120AutoSwitchBox.Checked=_settings.Features.AutoSwitchToLegacyFor120Fps;
    _autoSaveLogsBox.Checked=_settings.Features.AutoSaveSessionLogs;
    _nativeBluetoothAudioBox.Enabled=value.ActivationMode==DexDisplayMode.Native;
    _native120AutoSwitchBox.Enabled=_nativeBluetoothAudioBox.Enabled;
   } finally {_loadingStoreValues=wasLoading;}
  }
  private void InitializeStoreControls() {
   _dexDisplayModeBox=CreateSelect();
   _dexDisplayModeBox.Items.Add(LocalizationService.IsKorean?"네이티브 DeX":"Native DeX");
   _dexDisplayModeBox.Items.Add(LocalizationService.IsKorean?"보조 디스플레이 시뮬레이션":"Simulated secondary display");
   _storeEditors["DexDisplayModeSelector"]=_dexDisplayModeBox;
   _nativeBluetoothAudioBox=new ThemedCheckBox();
   _storeEditors["NativeBluetoothAudioToggle"]=_nativeBluetoothAudioBox;
   _native120AutoSwitchBox=new ThemedCheckBox();_storeEditors["Native120AutoSwitchToggle"]=_native120AutoSwitchBox;
   _autoSaveLogsBox=new ThemedCheckBox();_storeEditors["AutoSaveSessionLogsToggle"]=_autoSaveLogsBox;
   _languageBox=CreateSelect();foreach(AppLanguage v in Enum.GetValues(typeof(AppLanguage)))_languageBox.Items.Add(new LanguageOption(v));
   _themeBox=CreateSelect();foreach(AppTheme v in Enum.GetValues(typeof(AppTheme)))_themeBox.Items.Add(new ThemeOption(v));
   _miniControlBarSideBox=CreateSelect();foreach(MiniControlBarSide v in Enum.GetValues(typeof(MiniControlBarSide)))_miniControlBarSideBox.Items.Add(new MiniControlBarSideOption(v));
   _wakeUpModeBox=CreateSelect();foreach(ScrcpyWakeUpMode v in Enum.GetValues(typeof(ScrcpyWakeUpMode)))_wakeUpModeBox.Items.Add(v);
   _keyInputModeBox=CreateSelect();_enterInputModeBox=CreateSelect();foreach(var v in new[]{KeyInputMode.SendInputScanCode,KeyInputMode.SendInputVirtualKey,KeyInputMode.Adb}){var key=v==KeyInputMode.SendInputScanCode?"InputModeScanCode":v==KeyInputMode.SendInputVirtualKey?"InputModeVirtualKey":"InputModeAdb";_keyInputModeBox.Items.Add(new StoreInputModeOption(v,key));_enterInputModeBox.Items.Add(new StoreInputModeOption(v,key+"Enter"));}
   _automaticAdbBox=CreateRadio(LocalizationService.Get("Settings.AdbAuto"));_manualAdbBox=CreateRadio(LocalizationService.Get("Settings.AdbManual"));
   _manualAdbPathBox=StorePath();_manualAdbPanel=new Panel();_manualAdbPanel.Controls.Add(_manualAdbPathBox);
   _scrcpyPathBox=StorePath();_scrcpyPathBox.ReadOnly=true;
   _screenshotFolderBox=StorePath();_deviceScreenshotFolderBox=StorePath();_logFolderBox=StorePath();_fileTransferTargetFolderBox=StorePath();_phoneToPcReceiveFolderBox=StorePath();
   _screenshotFolderBox.ReadOnly=true;_deviceScreenshotFolderBox.ReadOnly=true;_phoneToPcReceiveFolderBox.ReadOnly=true;
   _startWithWindowsBox=new ThemedCheckBox();_startMinimizedBox=new ThemedCheckBox();_autoHideBox=new ThemedCheckBox();_autoStartDexBox=new ThemedCheckBox();
   _miniControlBarBox=new ThemedCheckBox();_showConnectedDeviceInfoBox=new ThemedCheckBox();_pushCaptureBox=new ThemedCheckBox();_managedFileTransferBox=new ThemedCheckBox();_phoneToPcTransferBox=new ThemedCheckBox();
   _resetDisplayOnStopBox=new ThemedCheckBox();_disableStayAwakeBox=new ThemedCheckBox();
   _lowLevelHotkeyBox=new ThemedCheckBox();_keyboardDiagnosticsBox=new ThemedCheckBox();_convertHangulBox=new ThemedCheckBox();_rightWindowsBox=new ThemedCheckBox();_convertEnterBox=new ThemedCheckBox();_autoAppEnterBox=new ThemedCheckBox();_ignoreShiftSpaceBox=new ThemedCheckBox();
   _captureHotkeyBox=new ThemedHotkeyControl {CenterText=true,Font=UiFonts.Pixels(14)};_exitHotkeyBox=new ThemedHotkeyControl {CenterText=true,Font=UiFonts.Pixels(14)};
   _deviceMonitorIntervalBox=StoreNumber(1,60);_disconnectMonitorIntervalBox=StoreNumber(1,60);_connectedStartDelayBox=StoreNumber(0,60);_adbWakeUpDelayBox=StoreNumber(0,60);
   _autoHideSecondsBox=StoreNumber(1,3600);_captureWaitSecondsBox=StoreNumber(1,60);_processTimeoutBox=StoreNumber(1,120);_virtualDisplayTimeoutBox=StoreNumber(1,60);
   _usbConnectionBox=CreateRadio(StoreUiText.Get("ConnectionUsb.Content","USB"));_wirelessConnectionBox=CreateRadio(StoreUiText.Get("ConnectionWireless.Content","Wi-Fi"));
   _wirelessDeviceBox=CreateSelect();_wirelessDeviceBox.SelectedIndexChanged+=WirelessDeviceBox_SelectedIndexChanged;
   _companionDeviceSelector=CreateSelect();_companionDeviceSelector.SelectionChangeCommitted+=delegate {if(!_storeUiReady)return;_wirelessDeviceBox.SelectedIndex=_companionDeviceSelector.SelectedIndex;};
   _wirelessHostBox=StorePath();_wirelessPortBox=StoreNumber(1,65535);_wirelessAutoReconnectBox=new ThemedCheckBox();_wirelessStatusLabel=new UiLabel {AutoSize=false};
   _pairingPortBox=StoreNumber(1,65535);_pairingCodeBox=StorePath();_pairingCodeBox.UsePasswordMask=true;_pairingCodeBox.MaxLength=6;
   _wirelessPrepareButton=StoreAction("ConnectionPrepareButton","USB 무선 준비",WirelessPrepareButton_Click);
   _wirelessConnectButton=StoreAction("ConnectionConnectButton","연결",WirelessConnectButton_Click);
   _wirelessDisconnectButton=StoreAction("ConnectionDisconnectButton","해제",WirelessDisconnectButton_Click);
   _pairButton=StoreAction("ConnectionPairButton","페어링",PairButton_Click);
   _displayCleanupStatusLabel=new UiLabel {AutoSize=false};_bundleStateText=new UiLabel {Text=LocalizationService.Get("Settings.DisplayCleanupChecking")};_installStateText=new UiLabel {Text=LocalizationService.Get("Settings.DisplayCleanupChecking")};
   _companionInstallButton=StoreAction("CompanionInstallButton","설치 / 업데이트",CompanionInstallButton_Click);
   _displayCleanupPermissionButton=StoreAction("CompanionGrantButton","권한 부여",DisplayCleanupPermissionButton_Click);
   _companionUninstallButton=StoreAction("CompanionUninstallButton","삭제",CompanionUninstallButton_Click);
   _diagnosticReportButton=StoreAction("DiagnosticsSaveReportButton","보고서 저장",DiagnosticReportButton_Click);
   var devices=BuildSelectedDeviceDiagnosticsPanel();
   _storeEditors["LanguageSelector"]=_languageBox;_storeEditors["ThemeSelector"]=_themeBox;
   _storeEditors["StartWithWindowsToggle"]=_startWithWindowsBox;_storeEditors["StartMinimizedToggle"]=_startMinimizedBox;
   _storeEditors["MiniControlBarToggle"]=_miniControlBarBox;_storeEditors["MiniControlBarSideSelector"]=_miniControlBarSideBox;
   _storeEditors["AutoHideToggle"]=_autoHideBox;_storeEditors["AutoHideSecondsBox"]=_autoHideSecondsBox;
   _storeEditors["AutoStartDexToggle"]=_autoStartDexBox;_storeEditors["AutoStartDelaySecondsBox"]=_connectedStartDelayBox;
   _storeEditors["CaptureHotkeyBox"]=_captureHotkeyBox;_storeEditors["ExitHotkeyBox"]=_exitHotkeyBox;
   _storeEditors["HangulKeyToggle"]=_convertHangulBox;_storeEditors["HangulInputModeSelector"]=_keyInputModeBox;_storeEditors["RightWindowsKeyToggle"]=_rightWindowsBox;_storeEditors["IgnoreShiftSpaceToggle"]=_ignoreShiftSpaceBox;
   _storeEditors["AutoAppEnterToggle"]=_autoAppEnterBox;_storeEditors["ShiftEnterToggle"]=_convertEnterBox;_storeEditors["EnterInputModeSelector"]=_enterInputModeBox;_storeEditors["KeyboardDiagnosticsToggle"]=_keyboardDiagnosticsBox;
   _storeEditors["DeviceSelector"]=_wirelessDeviceBox;_storeEditors["CompanionDeviceSelector"]=_companionDeviceSelector;
   _storeEditors["UsbRadio"]=_usbConnectionBox;_storeEditors["WirelessRadio"]=_wirelessConnectionBox;
   _storeEditors["AutoReconnectToggle"]=_wirelessAutoReconnectBox;_storeEditors["HostBox"]=_wirelessHostBox;_storeEditors["PortBox"]=_wirelessPortBox;
   _storeEditors["PrepareButton"]=_wirelessPrepareButton;_storeEditors["ConnectButton"]=_wirelessConnectButton;_storeEditors["DisconnectButton"]=_wirelessDisconnectButton;_storeEditors["PairingPortBox"]=_pairingPortBox;_storeEditors["PairingCodeBox"]=_pairingCodeBox;_storeEditors["PairButton"]=_pairButton;
   _storeEditors["WirelessStatusLabel"]=_wirelessStatusLabel;_storeEditors["CompanionStatusLabel"]=_displayCleanupStatusLabel;
   _storeEditors["RefreshCompanionButton"]=StoreAction("CompanionRefreshButton","새로고침",delegate {RefreshDisplayCleanupStatusAsync();});
   _storeEditors["InstallCompanionButton"]=_companionInstallButton;_storeEditors["GrantCompanionButton"]=_displayCleanupPermissionButton;_storeEditors["UninstallCompanionButton"]=_companionUninstallButton;
   var adb=StorePath();adb.ReadOnly=true;adb.Text=_adbService.AdbPath;_storeEditors["AdbPathBox"]=adb;_storeEditors["ScrcpyPathBox"]=_scrcpyPathBox;
   _storeEditors["ScreenshotPathBox"]=_screenshotFolderBox;_storeEditors["DeviceScreenshotPathBox"]=_deviceScreenshotFolderBox;_storeEditors["FileTransferTargetFolderBox"]=_fileTransferTargetFolderBox;_storeEditors["PhoneToPcTransferToggle"]=_phoneToPcTransferBox;_storeEditors["PhoneToPcReceiveFolderBox"]=_phoneToPcReceiveFolderBox;
   _storeEditors["BrowseScreenshotFolderButton"]=StoreAction("PathsChoosePcScreenshotButton","선택",delegate {BrowseStorePcPath(_screenshotFolderBox);});
   _storeEditors["BrowseDeviceScreenshotFolderButton"]=StoreAction("PathsChoosePhoneScreenshotButton","휴대폰에서 선택",delegate {BrowseDeviceFolder(_deviceScreenshotFolderBox);SaveStorePaths();});
   _storeEditors["BrowseFileTransferTargetButton"]=StoreAction("PathsChoosePhoneTargetButton","휴대폰에서 선택",delegate {BrowseDeviceFolder(_fileTransferTargetFolderBox);SaveStorePaths();});
   _storeEditors["BrowsePhoneToPcReceiveFolderButton"]=StoreAction("PathsChoosePcReceiveButton","선택",delegate {BrowseStorePcPath(_phoneToPcReceiveFolderBox);});
   _storeEditors["@LogButton_Click"]=StoreAction("DiagnosticsOpenLogButton","로그 창 열기",delegate {if(_showLogs!=null)_showLogs();});
   _storeEditors["DiagnosticEnvironmentText"]=new UiLabel {Text=StoreUiText.Get("DiagnosticsEnvironmentText.Text","검사를 시작하지 않았습니다.")};
   _storeEditors["RefreshDiagnosticsButton"]=StoreAction("DiagnosticsRefreshButton","새로고침",delegate {RefreshStoreEnvironment();RefreshDeviceDiagnosticsAsync();});
   _storeEditors["DiagnosticDeviceCardsGrid"]=devices;
   _storeEditors["DiagnosticReportStatusText"]=new UiLabel {Text=StoreUiText.Get("DiagnosticsReportStatusText.Text","민감한 값을 가린 보고서를 저장합니다.")};
   _storeEditors["SaveDiagnosticReportButton"]=_diagnosticReportButton;
   _storeEditors["ResetSettingsButton"]=StoreAction("DiagnosticsResetButton","기본값 초기화",ResetDefaultsButton_Click);
  }
  private List<Control> BuildStoreSettingsPages() {
   InitializeStoreControls();var result=new List<Control>();
   foreach(var pageName in new[]{"General","Connection","Paths","Input","Diagnostics","About","Companion"}) {
    var page=(FlowLayoutPanel)CreatePage();page.Padding=new Padding(0,0,16,16);
    if(pageName=="About") {BuildStoreAbout(page);result.Add(page);continue;}
    page.Controls.Add(new UiLabel {Text=StoreUiText.Get(pageName+"Heading.Text",pageName),Font=UiFonts.Pixels(20,true),ForeColor=_theme.TextPrimary,AutoSize=true,Margin=new Padding(0,0,0,6),Tag="primary"});
    page.Controls.Add(new UiLabel {Text=StoreUiText.Get(pageName+"Description.Text",""),Font=UiFonts.Pixels(14),ForeColor=_theme.TextSecondary,AutoSize=true,MaximumSize=new Size(CardWidth,0),Margin=new Padding(0,0,0,14)});
    foreach(var spec in StoreSettingSpec.All.Where(s=>s.Page==pageName && s.Uid!="CompanionBundleCard" && s.Uid!="CompanionInstallStatusCard")) {
     if(spec.Uid=="PathsPcToPhoneExpander") {
      AddStoreSectionLabel(page,"PathsTransferHeading.Text","파일 전송 경로",true);
      AddStoreSectionLabel(page,"PathsTransferDescription.Text","PC와 휴대폰 사이의 전송 대상 폴더를 설정합니다.",false);
     }
     var card=new StoreSettingCard(StoreUiText.Get(spec.Uid+".Header",spec.Header),StoreUiText.Get(spec.Uid+".Description",spec.Description),spec.Symbol,spec.Parent.Length>0,spec.Expander) {Width=CardWidth,Tag=spec.Uid};
     _storeCards[spec.Uid]=card;
     if(spec.Parent.Length>0)_storeCards[spec.Parent].ChildrenPanel.Controls.Add(card);else page.Controls.Add(card);
     AddStoreEditors(card,spec);
     if(spec.Uid=="InputAiEnterCard") AddAiProfileCards(card);
     card.Expanded=spec.Expanded;card.ApplyTheme();card.Reflow();
     if(spec.Editors.Any(editor=>editor.Name=="ExitHotkeyBox"))
      AddStoreSectionLabel(page,"InputHotkeyHint.Text","보조키가 아닌 키를 함께 눌러 단축키를 지정하세요.",false);
    }
    result.Add(page);
   }
   return result;
  }
  private void AddStoreSectionLabel(FlowLayoutPanel page,string key,string fallback,bool heading) {
   page.Controls.Add(new UiLabel {
    Text=StoreUiText.Get(key,fallback), AutoSize=true, MaximumSize=new Size(CardWidth,0),
    Font=UiFonts.Pixels(heading?16:13,heading), Tag=heading?"primary":null,
    ForeColor=heading?_theme.TextPrimary:_theme.TextTertiary,
    Margin=new Padding(0,heading?6:0,0,heading?5:12)
   });
  }

  private readonly Dictionary<CheckBox,AppKeyProfile[]> _aiProfileToggles=new Dictionary<CheckBox,AppKeyProfile[]>();

  private bool IsAiProfileGroupEnabled(AppKeyProfile[] targets) {
   return _settings.KeyMappings.AppKeyProfiles.Any(saved=>saved!=null&&saved.Enabled&&targets.Any(saved.HasSameTarget));
  }

  private void LoadAiProfileControls() {
   var wasLoading=_loadingStoreValues;
   _loadingStoreValues=true;
   try {
    foreach(var entry in _aiProfileToggles) entry.Key.Checked=IsAiProfileGroupEnabled(entry.Value);
   } finally {_loadingStoreValues=wasLoading;}
  }

  private void AddAiProfileCards(StoreSettingCard parent) {
   var profiles=AppKeyProfile.CreateDefaults();
   foreach(var group in profiles.GroupBy(profile=>profile.DisplayName=="Gemini WebAPK"?"Gemini":profile.DisplayName)) {
    var targets=group.ToArray();
    var card=new StoreSettingCard(group.Key,"",UiSymbol.Keyboard,true,false) {
     Width=CardWidth-14, EditorWidth=104, Tag="InputAiProfile"+group.Key
    };
    var toggle=new ThemedCheckBox {
     AccessibleName=group.Key,
     Bounds=new Rectangle(52,0,52,40),
     Checked=IsAiProfileGroupEnabled(targets)
    };
    var state=new UiLabel {Font=UiFonts.Pixels(14),TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,0,48,40)};
    Action updateState=()=>state.Text=StoreUiText.Get(toggle.Checked?"CommonToggleOn.Text":"CommonToggleOff.Text",toggle.Checked?"켬":"끔");
    updateState();
    toggle.CheckedChanged+=delegate {
     updateState();
     SaveStoreScope(settings=>{
      foreach(var target in targets) {
       var matches=settings.KeyMappings.AppKeyProfiles.Where(target.HasSameTarget).ToList();
       if(matches.Count==0&&toggle.Checked) {
        settings.KeyMappings.AppKeyProfiles.Add(target);
       } else {
        foreach(var saved in matches) saved.Enabled=toggle.Checked;
       }
      }
     });
    };
    card.EditorHost.Controls.AddRange(new Control[]{state,toggle});
    card.RegisterEditorBounds(state);card.RegisterEditorBounds(toggle);
    _aiProfileToggles.Add(toggle,targets);
    _storeCards.Add((string)card.Tag,card);
    parent.ChildrenPanel.Controls.Add(card);
    card.ApplyTheme();
   }
  }

  private void AddStoreEditors(StoreSettingCard card,StoreSettingSpec spec) {
   var editors=spec.Editors.ToList();
   if(spec.Uid=="GeneralSingleInstanceCard" || spec.Uid=="GeneralCloseBehaviorCard") {
    var text=new UiLabel {Text=StoreUiText.Get(spec.Uid=="GeneralSingleInstanceCard"?"GeneralSingleInstanceValue.Text":"GeneralCloseBehaviorValue.Text",""),Font=UiFonts.Pixels(14,true),TextAlign=ContentAlignment.MiddleRight,Dock=DockStyle.Fill};card.EditorWidth=220;card.EditorHost.Controls.Add(text);
   }
   if(spec.Uid=="CompanionBundleCard") {card.EditorWidth=300;_bundleStateText.Dock=DockStyle.Fill;card.EditorHost.Controls.Add(_bundleStateText);}
   if(spec.Uid=="CompanionInstallStatusCard") {card.DescriptionLabel.TextChanged+=delegate {card.Reflow();};}
   int x=0,maxHeight=40;
   foreach(var entry in editors) {
    if(entry.Name=="CompanionStatusLabel" || entry.Name=="RefreshDiagnosticsButton") continue;
    Control control;if(!_storeEditors.TryGetValue(entry.Name,out control))throw new InvalidOperationException("Unmapped Store setting: "+entry.Name);
    control.Font=UiFonts.Pixels(14);control.Dock=DockStyle.None;control.Anchor=AnchorStyles.Top|AnchorStyles.Left;
    var select=control as ThemedSelectControl;if(select!=null)select.CenterSelectedText=true;
    var label=control as Label;if(label!=null){label.AutoSize=false;label.TextAlign=ContentAlignment.MiddleLeft;}
    if(!string.IsNullOrEmpty(entry.Uid)) {
     var value=StoreUiText.Get(entry.Uid+".Content",StoreUiText.Get(entry.Uid+".Text",""));
     if(!string.IsNullOrEmpty(value) && (control is Button || control is RadioButton))control.Text=value;
    }
    int width=entry.Kind=="TextBlock" && editors.Count>1 ? 328 : entry.Width;
    if(spec.Uid=="PathsAdbCard" || spec.Uid=="PathsScrcpyCard")width=496;
    if(control is CheckBox) {
     control.Text="";var enabled=new UiLabel {Text=StoreUiText.Get("CommonEnabled.Text","사용"),Font=UiFonts.Pixels(14),TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(x,0,48,40)};card.RegisterEditorBounds(enabled);card.EditorHost.Controls.Add(enabled);x+=52;
     if(entry.Name=="NativeBluetoothAudioToggle") {
      var toggle=(CheckBox)control;
      EventHandler update=delegate {enabled.Text=LocalizationService.IsKorean?(toggle.Checked?"켬":"끔"):(toggle.Checked?"On":"Off");};
      toggle.CheckedChanged+=update;update(toggle,EventArgs.Empty);
     }
    }
    if(entry.Kind=="Grid") {card.WideContent=true;width=CardWidth-36;control.AutoSize=false;
     control.SizeChanged+=delegate {card.EditorHeight=control.Height;card.Reflow();};maxHeight=control.Height;}
    int height=entry.Kind=="Grid"?maxHeight:40;
    if(!string.IsNullOrEmpty(entry.Header)) {
     var header=new UiLabel {Text=StoreUiText.Get(entry.Uid+".Header",entry.Header),Font=UiFonts.Pixels(12),Bounds=new Rectangle(x,0,width,20)};card.RegisterEditorBounds(header);card.EditorHost.Controls.Add(header);control.Bounds=new Rectangle(x,22,width,height);maxHeight=Math.Max(maxHeight,62);
    } else control.Bounds=new Rectangle(x,0,width,height);
    if(control is RadioButton) { ((RadioButton)control).AutoSize=false; ((RadioButton)control).TextAlign=ContentAlignment.MiddleLeft; control.Height=40; }
    card.RegisterEditorBounds(control);card.EditorHost.Controls.Add(control);card.Editors.Add(control);x+=width+8;
   }
   if(x>0)card.EditorWidth=x-8;card.EditorHeight=maxHeight;
   if(spec.Uid=="DiagnosticsEnvironmentCard") {
    var text=(Label)_storeEditors["DiagnosticEnvironmentText"];
    var refresh=_storeEditors["RefreshDiagnosticsButton"];
    card.WideContent=true;
    text.TextAlign=ContentAlignment.TopLeft;
    text.UseMnemonic=false;
    refresh.Parent=card.HeaderSurface;
    refresh.Anchor=AnchorStyles.Top|AnchorStyles.Right;
    refresh.Bounds=new Rectangle(card.Width-178,14,160,40);
   }
   if(spec.Uid=="DiagnosticsDeviceCard") {
    _deviceDiagnosticsRefreshButton.Text=LocalizationService.IsKorean?"새로고침":"Refresh";
    _deviceDiagnosticsRefreshButton.Parent=card.HeaderSurface;
    _deviceDiagnosticsRefreshButton.Anchor=AnchorStyles.Top|AnchorStyles.Right;
    _deviceDiagnosticsRefreshButton.Bounds=new Rectangle(card.Width-178,14,160,40);
   }
   if(spec.Uid=="CompanionStatusExpanderText") {
    var refresh=_storeEditors["RefreshCompanionButton"];
    refresh.Bounds=new Rectangle(0,0,160,40);refresh.Text=LocalizationService.IsKorean?"새로고침":"Refresh";
    card.EditorWidth=160;card.EditorHeight=40;card.RegisterEditorBounds(refresh);card.EditorHost.Controls.Add(refresh);
   }
   card.Reflow();
  }
  private void BuildStoreAbout(FlowLayoutPanel page) {
   page.Controls.Add(new UiLabel {Text=StoreUiText.Get("SettingsNavAbout.Content",LocalizationService.Get("Settings.About")),Font=UiFonts.Pixels(20,true),AutoSize=true,ForeColor=_theme.TextPrimary,Margin=new Padding(0,0,0,12),Tag="primary"});
   var card=new RoundedPanel {Radius=20,Width=CardWidth,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(20),BackColor=Color.Transparent};
   var stack=new FlowLayoutPanel {Location=new Point(20,20),Width=CardWidth-40,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent};
   stack.Controls.Add(new UiLabel {Text="DX Manager v"+Application.ProductVersion,Font=UiFonts.Pixels(15,true),AutoSize=true,Tag="primary",Margin=new Padding(0,0,0,12)});
   var paragraphs=new[]{
    StoreUiText.Get("AboutSummaryText.Text",""),
    string.Join(Environment.NewLine,new[]{
     StoreUiText.Get("AboutDeveloperText.Text",""),
     StoreUiText.Get("AboutCopyrightText.Text",""),
     StoreUiText.Get("AboutLicenseText.Text","")
    }),
    StoreUiText.Get("AboutScrcpyText.Text",""),
    StoreUiText.Get("AboutLibrariesText.Text",""),
    StoreUiText.Get("AboutSamsungText.Text",""),
    StoreUiText.Get("AboutDisclaimerText.Text","")
   };
   foreach(var text in paragraphs) {
    stack.Controls.Add(new UiLabel {
     Text=text,Font=UiFonts.Pixels(14),AutoSize=true,MaximumSize=new Size(CardWidth-40,0),
     Margin=new Padding(0,0,0,12),Tag="primary"
    });
   }
   const string supportAddress="dxmanager.support@gmail.com";
   var supportPrefix=StoreUiText.Get("AboutSupportPrefix","Bug reports and support: ");
   var contact=new UiLinkLabel {
    Text=supportPrefix+supportAddress,LinkArea=new LinkArea(supportPrefix.Length,supportAddress.Length),
    AutoSize=true,Font=UiFonts.Pixels(14),Margin=new Padding(0,0,0,12),Tag="primary"
   };
   contact.LinkClicked+=delegate {
    try {
     System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:"+supportAddress){UseShellExecute=true});
    } catch(Exception ex){ShowStoreFailure(ex.Message);}
   };
   stack.Controls.Add(contact);
   var actions=new FlowLayoutPanel {Name="AboutActionsRow",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,BackColor=Color.Transparent,Margin=Padding.Empty};
   var notices=StoreAction("AboutOpenNoticesButton","제3자 라이선스 보기",delegate {OpenThirdPartyNotices();});notices.Width=200;notices.Margin=new Padding(0,0,12,0);actions.Controls.Add(notices);
   var updateButton=new ThemedButton {Name="CheckForUpdatesButton",Text=LocalizationService.Get("Update.CheckButton"),Font=UiFonts.Pixels(14),Width=200,Height=40,Margin=Padding.Empty};updateButton.Click+=delegate {if(UpdateCheckRequested!=null)UpdateCheckRequested(this);};actions.Controls.Add(updateButton);
   stack.Controls.Add(actions);card.Controls.Add(stack);page.Controls.Add(card);
  }
  private void BrowseStorePcPath(ThemedTextControl box) {using(var dialog=new FolderBrowserDialog()){if(System.IO.Directory.Exists(box.Text))dialog.SelectedPath=box.Text;if(dialog.ShowDialog(this)==DialogResult.OK){box.Text=dialog.SelectedPath;SaveStorePaths();}}}
  private string ResolveStoreTargetSerial() {
   if(!_storeUiReady)return _mainSelectedSerial==null?"":_mainSelectedSerial();
   var option=GetSelectedWirelessDeviceOption();if(option==null)return "";
   var profile=_settings.FindDeviceWirelessConnection(option.DeviceIdentity);
   // Management follows an authorized transport on this selected physical device.
   // A saved USB launch policy must not hide a live TLS/mDNS-only connection.
   var usb=option.UsbSerial;if(!string.IsNullOrWhiteSpace(usb))return usb;
   return option.FindAuthorizedWirelessSerial(profile==null?"":WirelessAdbService.BuildEndpoint(profile.WirelessHost,profile.WirelessPort));
  }
  private string ResolveStoreTargetIdentity() {var option=GetSelectedWirelessDeviceOption();return _storeUiReady?(option==null?"":option.DeviceIdentity):(_mainSelectedIdentity==null?"":_mainSelectedIdentity());}
  private void SynchronizeStoreSelectors() {
   if(_companionDeviceSelector==null || _wirelessDeviceBox==null)return;
   _companionDeviceSelector.SelectedIndex=-1;_companionDeviceSelector.Items.Clear();foreach(var item in _wirelessDeviceBox.Items)_companionDeviceSelector.Items.Add(item);
   _companionDeviceSelector.SelectedIndex=_wirelessDeviceBox.SelectedIndex;
   var key=ResolveStoreTargetIdentity()+"|"+ResolveStoreTargetSerial();
   if(key!=_storeTargetKey){_storeTargetKey=key;_storeTargetGeneration++;if(_companionInstallButton!=null){_companionInstallButton.Enabled=false;_displayCleanupPermissionButton.Enabled=false;_companionUninstallButton.Enabled=false;}}
  }
  private void UpdateStoreCardSizes() {
   if(_contentHost==null)return;
   foreach(var page in _pages)ResizeStorePageCards(page);
  }
  private void ResizeStorePageCards(Control page) {
   if(page.Parent==null || page.ClientSize.Width<120)return;
   int width=Math.Max(120,page.Width-SystemInformation.VerticalScrollBarWidth-page.Padding.Horizontal-4);
   foreach(Control control in page.Controls){var card=control as StoreSettingCard;if(card!=null){card.Width=width;card.Reflow();}else if(control is RoundedPanel)control.Width=width;}
  }
  private void ApplyStoreCardTheme() {foreach(var card in _storeCards.Values)card.ApplyTheme();}
  private void UpdateStoreCompanionLabels() {
   StoreSettingCard summary;if(_storeCards.TryGetValue("CompanionStatusExpanderText",out summary)){summary.DescriptionLabel.Text=_displayCleanupStatusLabel.Text+(_bundledCompanionStatus==null?"":Environment.NewLine+_bundledCompanionStatus.Detail);summary.Reflow();}
   if(_bundleStateText!=null)_bundleStateText.Text=_bundledCompanionStatus==null?LocalizationService.Get("Settings.DisplayCleanupChecking"):_bundledCompanionStatus.Detail;
   StoreSettingCard card;if(_storeCards.TryGetValue("CompanionInstallStatusCard",out card) && _displayCleanupStatus!=null) {card.DescriptionLabel.Text=_displayCleanupStatus.Detail;card.Reflow();}
  }
 }
}
