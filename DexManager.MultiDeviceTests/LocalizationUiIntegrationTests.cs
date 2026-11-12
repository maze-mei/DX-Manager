using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

internal static class LocalizationUiIntegrationTests
{
    private static readonly Assembly Product=typeof(MainForm).Assembly;
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Regex Hangul=new Regex("[\\uac00-\\ud7a3\\u3131-\\u318e\\u1100-\\u11ff]");
    private static readonly List<string> Failures=new List<string>();
    private static int checks, strings;
    private static object Field(object value,string name){return value.GetType().GetField(name,Hidden).GetValue(value);}
    private static object Call(object value,string name,params object[] args)
    {
        return value.GetType().GetMethods(Hidden|BindingFlags.Public).Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(value,args);
    }
    private static void Check(bool value,string message){checks++;if(!value)Failures.Add(message);}
    private static void Text(string value,string where)
    {
        if(string.IsNullOrWhiteSpace(value))return;
        strings++;
        // The Korean language's native name is intentional in the language selector.
        if(value=="한국어")return;
        if(Hangul.IsMatch(value))Failures.Add(where+": "+value);
    }
    private static void Scan(Control root,string view)
    {
        Text(root.Text,view+"/"+root.GetType().Name);
        Text(root.AccessibleName,view+"/accessible name");Text(root.AccessibleDescription,view+"/accessible description");
        foreach(var property in new[]{"SecondaryText","TrailingText","PlaceholderText"})
        {
            var p=root.GetType().GetProperty(property);if(p!=null && p.PropertyType==typeof(string))Text((string)p.GetValue(root,null),view+"/"+property);
        }
        var items=root.GetType().GetProperty("Items");
        if(items!=null && items.GetValue(root,null) is IEnumerable)
            foreach(var value in (IEnumerable)items.GetValue(root,null))if(value!=null)Text(value.ToString(),view+"/item");
        foreach(Control child in root.Controls)Scan(child,view);
    }
    private static bool ContainsText(Control control, string value)
    {
        return control.Text.Contains(value) ||
            control.Controls.Cast<Control>().Any(child => ContainsText(child, value));
    }

    private static void CheckAbout(SettingsForm options, AppLanguage language)
    {
        Call(options, "ShowPage", 5);
        PumpMessages();
        var page = (Control)((IList)Field(options, "_pages"))[5];
        Check(ContainsText(page, "scrcpy 5.0.1") && ContainsText(page, "ScrcpyDeX v1.1") &&
              !ContainsText(page, "scrcpy 4.1"), "About shows the current runtime and native license in " + language);
        Check(ContainsText(page, language == AppLanguage.Korean ? "버그 신고 및 문의" : "Bug reports and support"),
              "About shows the localized support label in " + language);
    }

    private static void Capture(Form form,string label)
    {
        using(var bitmap=new Bitmap(form.Width,form.Height))
        {
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,label+".png"),ImageFormat.Png);
        }
    }
    private static void PumpMessages()
    {
        // The fixture has no Application.Run loop to keep the UI context installed.
        var context = SynchronizationContext.Current;
        Application.DoEvents();
        SynchronizationContext.SetSynchronizationContext(context);
    }
    private static void Show(Form form)
    {
        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.ShowInTaskbar=false;form.Show();PumpMessages();
    }
    private static void Resources()
    {
        foreach(var family in new[]{"Strings","StoreStrings"})
        {
            var rm=new ResourceManager("DexManager.Resources."+family,Product);
            var set=rm.GetResourceSet(CultureInfo.GetCultureInfo("en"),true,true);
            foreach(DictionaryEntry entry in set)if(entry.Value is string)Text((string)entry.Value,family+"/"+entry.Key);
            Check(set!=null,"English resource set loads: "+family);
        }
        foreach(var name in Product.GetManifestResourceNames().Where(n=>n.StartsWith("DexManager.Resources.Help.en.",StringComparison.Ordinal)))
            using(var stream=Product.GetManifestResourceStream(name))
            using(var reader=new StreamReader(stream))Text(reader.ReadToEnd(),name);
        foreach(var pair in new[]{
            new[]{"FirstRunDialog","Intro","Display","Power","Recovery","Companion","Independent","Liability"},
            new[]{"ExperimentalFps","ScreenWarning","RiskWarning","CareWarning","LimitWarning"},
            new[]{"UpdateNotice","Title","Intro","Fps","ScreenOff","Scrcpy","Companion","Confirm"}})
        {
            var rm=new ResourceManager("DexManager.Resources.StoreStrings",Product);
            foreach(var suffix in pair.Skip(1)){var value=rm.GetString(pair[0]+suffix,CultureInfo.GetCultureInfo("en"));Check(!string.IsNullOrWhiteSpace(value),"dynamic resource resolves: "+pair[0]+suffix);Text(value,pair[0]+suffix);}
        }
    }
    private static void Ui()
    {
        var settings=AppSettings.CreateDefault();settings.Language=AppLanguage.English;settings.Features.AutoStartDexOnDeviceConnected=false;
        var log=new LogService();var store=new SettingsService(log);
        var runner=new ProcessRunner(log);runner.BlockNewProcessesForWindowsShutdown();
        var adb=new AdbService("unused-adb.exe",1000,runner,log);var paths=new PathService(store,log,runner);
        var wifi=new WirelessAdbService(adb,store,settings,log);var registry=new PhysicalDeviceRegistry();
        var sessions=new DeviceRuntimeSessionRegistry();var launch=new ScrcpyLaunchCoordinator();
        var factory=new DeviceRuntimeServiceFactory("unused-scrcpy.exe","unused-adb.exe",1000,runner,adb,launch,store,settings,log,sessions);
        var runtime=factory.Create();
        var monitor=new DeviceMonitorService(adb,wifi,registry,log,1000,1000);
        var hotkeys=new HotkeyService(log,settings);var capture=new CaptureService(adb,store,settings,log);
        var captures=new CaptureCoordinator(hotkeys,capture,runtime.Scrcpy,runtime.SingleWindows,settings,log);
        var autoHide=new AutoHideService(runtime.Scrcpy,runtime.SingleWindows,log,60);
        var environment=new EnvironmentCheckService(adb,runtime.Scrcpy,paths,log,store,settings,()=>null);
        var keymap=new KeyMappingService(runtime.Scrcpy,runtime.SingleWindows,adb,settings,settings.KeyMappings,log);
        try
        {
            foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark})
            {
                settings.Theme=theme;LocalizationService.Apply(AppLanguage.English);
                using(var main=new MainForm(store,settings,log,adb,wifi,runtime.Scrcpy,runtime.SingleWindows,runtime.ScreenOff,launch,monitor,runtime.Dex,captures,autoHide,new AutoStartService(log),environment,keymap,runtime.FileTransfers,runtime.PhoneTransfers,sessions,runtime,registry,factory,paths,capture,false))
                {
                    main.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),main,typeof(MainForm).GetMethod("MainForm_Shown",Hidden));
                    Show(main);
                    var help=(Button)Field(main,"_helpSidebarButton");var settingsButton=(Button)Field(main,"_settingsSidebarButton");
                    foreach(var size in new[]{new Size(1184,760),new Size(1200,768),new Size(1600,960)})
                    {
                        main.ClientSize=size;Call(main,"LayoutDesktopContent");PumpMessages();
                        var measure=Product.GetType("DexManager.Forms.UiText").GetMethods(BindingFlags.Public|BindingFlags.Static).Single(x=>x.Name=="Measure" && x.GetParameters().Length==5);
                        using(var bitmap=new Bitmap(1,1))using(var graphics=Graphics.FromImage(bitmap))
                        {
                            var needed=(SizeF)measure.Invoke(null,new object[]{graphics,help.Text,help.Font,Size.Empty,TextFormatFlags.NoPrefix});
                            Check(Math.Ceiling(needed.Width)<=help.Width-66,"English help text fits at "+size+" ("+needed.Width+" <= "+(help.Width-66)+")");
                        }
                        Check(help.Font.Size<settingsButton.Font.Size,"only English help uses the smaller sidebar font");
                    }
                    main.ClientSize=new Size(1184,760);PumpMessages();
                    for(var mode=0;mode<=3;mode++){Call(main,"DisplayMode",mode);Scan(main,"English main mode"+mode+"/"+theme);}
                    Call(main,"DisplayMode",0);Capture(main,"English-Main-"+theme);
                    using(var options=new SettingsForm(store,settings,adb,wifi,null,null,null,null,log,null,null,null,null,null,null,null,
                        ()=>new List<EnvironmentCheckItem>{new EnvironmentCheckItem{Name="scrcpy version",Message="5.0.1",Status=EnvironmentCheckStatus.Passed}}))
                    {
                        Show(options);
                        for(var page=0;page<7;page++)
                        {
                            Call(options,"ShowPage",page);
                            foreach(DictionaryEntry card in (IDictionary)Field(options,"_storeCards"))card.Value.GetType().GetProperty("Expanded").SetValue(card.Value,true,null);
                            PumpMessages();Scan(options,"English settings page"+page+"/"+theme);
                            if(page==4)Capture(options,"English-Diagnostics-Initial-"+theme);
                        }
                        CheckAbout(options, AppLanguage.English);
                        Capture(options,"English-About-"+theme);
                        Call(options,"ShowPage",4);Call(options,"RefreshStoreEnvironment");
                        var label=(Label)((IDictionary)Field(options,"_storeEditors"))["DiagnosticEnvironmentText"];
                        var deadline=DateTime.UtcNow.AddSeconds(3);
                        while(!label.Text.Contains("5.0.1") && DateTime.UtcNow<deadline){PumpMessages();Thread.Sleep(10);}
                        Check(label.Text.Contains("5.0.1"),"English environment check completes");Scan(options,"English diagnostics complete/"+theme);
                    }
                    using(var helpForm=new HelpForm())
                    {
                        Show(helpForm);
                        for(var topic=0;topic<5;topic++){Call(helpForm,"ShowTopic",topic);Scan(helpForm,"English help topic"+topic+"/"+theme);}
                    }
                    using(var guide=(Form)Activator.CreateInstance(Product.GetType("DexManager.Forms.DocumentationForm"),true))
                    {
                        Show(guide);
                        for(var document=0;document<2;document++){Call(guide,"SwitchDocument",document);Scan(guide,"English embedded document"+document+"/"+theme);}
                    }
                    using(var transfer=new FileTransferStatusForm(runtime.FileTransfers,theme,"Fixture phone")){Show(transfer);Scan(transfer,"English PC transfer/"+theme);}
                    using(var transfer=new PhoneTransferStatusForm(theme,"Fixture phone")){Show(transfer);Scan(transfer,"English phone transfer/"+theme);}
                    using(var logs=new LogForm(log)){Scan(logs,"English session log/"+theme);}
                    using(var licenses=new ThirdPartyLicensesForm())
                    {
                        Show(licenses);
                        var tab=(Button)Field(licenses,"_scrcpyDexButton");
                        tab.PerformClick();
                        var text=((RichTextBox)Field(licenses,"_contentBox")).Text;
                        Check(tab.Text.Contains("ScrcpyDeX") && text.Contains("Aureliano Peixoto") &&
                              text.Contains("TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION"),
                              "Native DeX tab shows attribution and the full Apache license");
                        Capture(licenses,"English-Native-License-"+theme);
                    }
                    using(var notices=(Form)Activator.CreateInstance(Product.GetType("DexManager.Forms.UpdateCheckDialog"),Hidden,null,
                        new object[]{"3.0.0",(Func<Task<UpdateCheckResult>>)(()=>Task.FromResult(new UpdateCheckResult(UpdateCheckOutcome.Current)))},null))
                    {
                        Show(notices);ReleaseUpdate release;ReleaseUpdate.TryCreate("3.0.1","https://github.com/maze-mei/DX-Manager/releases/tag/v3.0.1",out release);
                        foreach(var outcome in new[]{UpdateCheckOutcome.Available,UpdateCheckOutcome.Current,UpdateCheckOutcome.Failed,UpdateCheckOutcome.Busy})
                        {Call(notices,"DisplayResult",new UpdateCheckResult(outcome,outcome==UpdateCheckOutcome.Available?release:null));Scan(notices,"English update "+outcome+"/"+theme);}
                    }
                    Call(main,"HideToTray",false);
                }
            }
            // Korean Help keeps the same size as all other sidebar buttons.
            settings.Language=AppLanguage.Korean;settings.Theme=AppTheme.Light;LocalizationService.Apply(AppLanguage.Korean);
            using(var main=new MainForm(store,settings,log,adb,wifi,runtime.Scrcpy,runtime.SingleWindows,runtime.ScreenOff,launch,monitor,runtime.Dex,captures,autoHide,new AutoStartService(log),environment,keymap,runtime.FileTransfers,runtime.PhoneTransfers,sessions,runtime,registry,factory,paths,capture,false))
            {
                main.Shown-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),main,typeof(MainForm).GetMethod("MainForm_Shown",Hidden));
                Show(main);Check(((Button)Field(main,"_helpSidebarButton")).Font.Size==((Button)Field(main,"_settingsSidebarButton")).Font.Size,"Korean Help font remains unchanged");
                using(var options=new SettingsForm(store,settings,adb,wifi,null,null,null,null,log,null,null,null,null,null,null,null))
                {
                    Show(options);
                    CheckAbout(options, AppLanguage.Korean);
                    Capture(options,"Korean-About-Light");
                }
                Call(main,"HideToTray",false);
            }
        }
        finally{captures.Dispose();autoHide.Dispose();hotkeys.Dispose();keymap.Dispose();monitor.Dispose();runtime.ScreenOff.Dispose();}
    }
    [STAThread] private static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("ko-KR");LocalizationService.Apply(AppLanguage.English);
            var previousContext = SynchronizationContext.Current;
            using (var uiContext = new WindowsFormsSynchronizationContext())
            {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                try { Resources(); Ui(); }
                finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            }
            Check(strings>3000,"Broad English resource/control scan ran: "+strings);
            Console.WriteLine("English UI scan checked "+strings+" strings.");
            foreach(var failure in Failures)Console.WriteLine("FAIL "+failure);
            Console.WriteLine("English localization checks: "+checks+"; failures: "+Failures.Count);
            return Failures.Count==0?0:1;
        }
        catch(Exception exception){Console.WriteLine(exception);return 1;}
    }
}
