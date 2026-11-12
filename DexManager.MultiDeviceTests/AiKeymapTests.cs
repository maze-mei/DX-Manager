using System;
using System.Collections.Generic;
using System.IO;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.MultiDeviceTests
{
    internal static partial class Program
    {
        private static void AiProfilesTargetOnlyExplicitNativeApps()
        {
            var rules = new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
            foreach (var app in new[] { "com.openai.chatgpt", "com.anthropic.claude", "com.google.android.apps.bard", "ai.x.grok" })
                True(rules.ShouldConvertEnter(app, false, false, false, false), "native AI profile");
            foreach (var app in new[] { "", "com.android.chrome", "com.sec.android.app.sbrowser",
                "com.google.android.googlequicksearchbox", "com.fake.chatgpt", "COM.OPENAI.CHATGPT" })
                True(!rules.ShouldConvertEnter(app, false, false, false, false), "no guessing or browser/Google-search conversion");
        }

        private static void AiProfilesKeepModifiedEnterUnchanged()
        {
            var rules = new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
            for (int mask = 1; mask < 16; mask++)
                True(!rules.ShouldConvertEnter("com.openai.chatgpt", (mask & 1) != 0,
                    (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0), "preserve modified Enter");
        }

        private static void AiProfilesRespectDisabledAndDefaultRules()
        {
            var profiles = AppKeyProfile.CreateDefaults();
            profiles[0].Enabled = false;
            profiles[1].EnterRule = AppEnterRule.Default;
            var rules = new AppKeyRuleResolver(profiles);
            True(!rules.ShouldConvertEnter(profiles[0].PackageName, false, false, false, false), "disabled rule");
            True(!rules.ShouldConvertEnter(profiles[1].PackageName, false, false, false, false), "explicit default rule");
            True(rules.ShouldConvertEnter("ai.x.grok", false, false, false, false), "independent rule");
        }

        private static long ReadyAiCache(ForegroundAppCache cache)
        {
            var generation = cache.BeginConnection();
            True(cache.SetAvailable(generation, 1, true), "initial service status");
            return generation;
        }

        private static void AiCacheSeparatesDisplaysAndPhysicalDevices()
        {
            var phoneA = new ForegroundAppCache(); var phoneB = new ForegroundAppCache();
            var a = ReadyAiCache(phoneA); var b = ReadyAiCache(phoneB);
            phoneA.Update(a, 2, 2, "com.openai.chatgpt");
            phoneA.Update(a, 3, 3, "com.android.chrome");
            phoneB.Update(b, 2, 2, "ai.x.grok");
            True(phoneA.GetPackage(2) == "com.openai.chatgpt" && phoneA.GetPackage(3) == "com.android.chrome", "per-display focus");
            True(phoneB.GetPackage(2) == "ai.x.grok", "same display ID on another phone");
            True(phoneA.GetPackage(0) == "", "phone primary display cannot leak into DeX");
        }

        private static void AiCacheRejectsOldConnectionMessages()
        {
            var cache = new ForegroundAppCache(); var old = ReadyAiCache(cache);
            cache.Update(old, 2, 2, "com.openai.chatgpt");
            var fresh = ReadyAiCache(cache);
            cache.Update(fresh, 2, 2, "com.android.chrome");
            True(!cache.Update(old, 100, 2, "com.openai.chatgpt"), "old reader rejected");
            cache.Disconnect(old);
            True(cache.GetPackage(2) == "com.android.chrome", "old socket cleanup does not clear replacement");
        }

        private static void AiCacheClearsOnServiceAndTransportLoss()
        {
            var cache = new ForegroundAppCache(); var generation = ReadyAiCache(cache);
            cache.Update(generation, 2, 2, "com.openai.chatgpt");
            cache.SetAvailable(generation, 3, false);
            True(cache.GetPackage(2) == "", "accessibility disabled");
            cache.SetAvailable(generation, 4, true);
            True(cache.GetPackage(2) == "", "new snapshot required after enabling");
            cache.Update(generation, 5, 2, "ai.x.grok");
            cache.Disconnect(generation);
            True(cache.GetPackage(2) == "", "transport lost");
        }

        private static void AiCacheExpiresWithoutLiveness()
        {
            long now = 1000; var cache = new ForegroundAppCache(() => now);
            var generation = ReadyAiCache(cache); cache.Update(generation, 2, 2, "com.openai.chatgpt");
            now += 8001;
            True(cache.GetPackage(2) == "", "stalled channel fails closed");
            cache.Heartbeat(generation);
            True(cache.GetPackage(2) == "", "late heartbeat cannot resurrect an old rule");
        }

        private static void AiCacheKeepsUnchangedAppsWithHeartbeats()
        {
            long now = 1000; var cache = new ForegroundAppCache(() => now);
            var generation = ReadyAiCache(cache); cache.Update(generation, 2, 2, "com.openai.chatgpt");
            for (int i = 0; i < 40; i++) { now += 3000; cache.Heartbeat(generation); }
            True(cache.GetPackage(2) == "com.openai.chatgpt", "unchanged app needs no periodic package polling");
        }

        private static void AiCacheRejectsOutOfOrderMessages()
        {
            var cache = new ForegroundAppCache(); var generation = ReadyAiCache(cache);
            cache.Update(generation, 5, 2, "com.android.chrome");
            True(!cache.Update(generation, 4, 2, "com.openai.chatgpt"), "old package update rejected");
            True(!cache.SetAvailable(generation, 3, false), "old status update rejected");
            True(cache.GetPackage(2) == "com.android.chrome", "latest app preserved");
        }

        private static void AiCacheRejectsInvalidPackages()
        {
            var cache = new ForegroundAppCache(); var generation = ReadyAiCache(cache);
            foreach (var invalid in new[] { "../chatgpt", "com.openai.chatgpt\n", "com..chatgpt", ".chatgpt", "chrome" })
                True(!cache.Update(generation, 2, 2, invalid), "invalid package rejected");
            True(!cache.Update(generation, 2, -1, "com.openai.chatgpt"), "invalid display rejected");
            True(cache.GetPackage(2) == "", "invalid package cannot create an AI rule");
        }

        private static void AiProfilesSurviveSettingsRoundTrip()
        {
            var settings = AppSettings.CreateDefault();
            settings.KeyMappings.ConvertEnterToShiftEnter = false;
            settings.KeyMappings.AutoAppEnterEnabled = true; // Explicitly opt in for this enabled-feature round-trip test.
            settings.KeyMappings.AppKeyProfiles.Add(new AppKeyProfile {
                PackageName = "test.private.assistant", Enabled = true, EnterRule = AppEnterRule.ShiftEnter });
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(AppSettings));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings); stream.Position = 0;
                var restored = (AppSettings)serializer.ReadObject(stream); restored.EnsureDefaults();
                var rules = new AppKeyRuleResolver(restored.KeyMappings.AppKeyProfiles);
                True(restored.KeyMappings.AutoAppEnterEnabled &&
                    rules.ShouldConvertEnter("test.private.assistant", false, false, false, false), "custom package persists");
            }
        }

        private static void AiManualModeKeepsScrollLockPriority()
        {
            var settings = AppSettings.CreateDefault();
            settings.KeyMappings.ConvertEnterToShiftEnter = true;
            settings.KeyMappings.AutoAppEnterEnabled = true;
            settings.EnsureDefaults();
            True(settings.KeyMappings.ConvertEnterToShiftEnter && !settings.KeyMappings.AutoAppEnterEnabled, "manual mode wins saved conflict");
            settings.KeyMappings.ConvertEnterToShiftEnter = false;
            settings.KeyMappings.AutoAppEnterEnabled = true;
            settings.EnsureDefaults();
            True(settings.KeyMappings.AutoAppEnterEnabled, "automatic mode remains distinct");
        }

        private static ForegroundWindowDiagnostic GeminiWindow(int display = 77, int window = 42)
        {
            var profile = AppKeyProfile.CreateGeminiWebAppDefault();
            return new ForegroundWindowDiagnostic { DisplayId = display, WindowId = window,
                WindowType = 1, Focused = true, Active = true, Package = profile.PackageName,
                StateClass = profile.WindowStateClass, Title = "\u200EGoogle Gemini" };
        }

        private static bool ConvertsAiEnter(AppKeyRuleResolver rules, ForegroundAppCache cache, int display)
        {
            return rules.ShouldConvertEnter(cache.GetIdentity(display), false, false, false, false);
        }

        private static void AiGeminiWebRuleRequiresVerifiedWindowIdentity()
        {
            var rules = new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
            var profile = AppKeyProfile.CreateGeminiWebAppDefault();
            var gemini = new ForegroundAppIdentity(profile.PackageName, 999, profile.WindowStateClass,
                "\u200EGoogle Gemini");
            True(rules.ShouldConvertEnter(gemini, false, false, false, false), "verified Gemini WebAPK maps Enter");
            foreach (var app in new[] {
                new ForegroundAppIdentity(profile.PackageName),
                new ForegroundAppIdentity(profile.PackageName, 42,
                    "org.chromium.chrome.browser.ChromeTabbedActivity", "Google Gemini"),
                new ForegroundAppIdentity(profile.PackageName, 42, profile.WindowStateClass, "ChatGPT"),
                new ForegroundAppIdentity(profile.PackageName, 42, profile.WindowStateClass, "Google Gemini - other"),
                new ForegroundAppIdentity("com.sec.android.app.sbrowser", 42, profile.WindowStateClass, "Google Gemini") })
                True(!rules.ShouldConvertEnter(app, false, false, false, false), "package/class/title all required");
            for (int mask = 1; mask < 16; mask++)
                True(!rules.ShouldConvertEnter(gemini, (mask & 1) != 0, (mask & 2) != 0,
                    (mask & 4) != 0, (mask & 8) != 0), "web rule preserves modified Enter");
        }

        private static void AiWebProfilesKeepMultipleTargetsAndExplicitPriority()
        {
            var gemini = AppKeyProfile.CreateGeminiWebAppDefault();
            var other = AppKeyProfile.CreateGeminiWebAppDefault(); other.WindowTitle = "Other assistant";
            var rules = new AppKeyRuleResolver(new[] { other, gemini });
            True(rules.ShouldConvertEnter(new ForegroundAppIdentity(gemini.PackageName, 42,
                gemini.WindowStateClass, gemini.WindowTitle), false, false, false, false),
                "same-package profiles must not collapse into one rule");
            True(rules.ShouldConvertEnter(new ForegroundAppIdentity(other.PackageName, 43,
                other.WindowStateClass, other.WindowTitle), false, false, false, false), "second window target works");
            var normal = new AppKeyProfile { PackageName = gemini.PackageName, Enabled = true,
                EnterRule = AppEnterRule.Default };
            rules = new AppKeyRuleResolver(new[] { normal, gemini });
            True(!rules.ShouldConvertEnter(new ForegroundAppIdentity(gemini.PackageName, 42,
                gemini.WindowStateClass, gemini.WindowTitle), false, false, false, false),
                "first matching explicit Default keeps user priority");
            foreach (var title in new[] { "\u200E", "\r\n", new string('X', 513) })
            {
                gemini.WindowTitle = title;
                rules = new AppKeyRuleResolver(new[] { gemini });
                True(!rules.ShouldConvertEnter(new ForegroundAppIdentity(gemini.PackageName, 42,
                    gemini.WindowStateClass, "Google Gemini"), false, false, false, false),
                    "invalid selector cannot become an unrestricted browser rule");
            }
        }

        private static void AiGeminiWebProfileMigrationPreservesCustomSettings()
        {
            var settings = AppSettings.CreateDefault(); settings.SchemaVersion = 28;
            var target = AppKeyProfile.CreateGeminiWebAppDefault();
            settings.KeyMappings.AppKeyProfiles.RemoveAll(p =>
                p.HasSameTarget(target) || p.PackageName == "com.anthropic.claude");
            settings.KeyMappings.AppKeyProfiles[0].Enabled = false;
            settings.KeyMappings.AutoAppEnterEnabled = false;
            settings.KeyMappings.AppKeyProfiles.Add(new AppKeyProfile { PackageName = "test.private.assistant",
                Enabled = true, EnterRule = AppEnterRule.ShiftEnter });
            settings.EnsureDefaults(); settings.EnsureDefaults();
            True(settings.SchemaVersion == AppSettings.CurrentSchemaVersion && settings.KeyMappings.AppKeyProfiles.Count == 6 &&
                settings.KeyMappings.AppKeyProfiles.FindAll(p => p.HasSameTarget(target)).Count == 1,
                "schema28 gains exactly one Gemini web rule");
            True(settings.KeyMappings.AppKeyProfiles.FindAll(p =>
                p.PackageName == "com.anthropic.claude").Count == 1, "existing profiles gain Claude once");
            True(!settings.KeyMappings.AutoAppEnterEnabled && !settings.KeyMappings.AppKeyProfiles[0].Enabled &&
                settings.KeyMappings.AppKeyProfiles[3].PackageName == "test.private.assistant",
                "migration preserves user mode, disabled rules and custom profiles");
            settings.SchemaVersion = 28;
            var saved = settings.KeyMappings.AppKeyProfiles.Find(p => p.HasSameTarget(target));
            saved.Enabled = false; saved.EnterRule = AppEnterRule.Default; saved.WindowTitle = "\u200EGoogle Gemini";
            settings.EnsureDefaults();
            True(settings.KeyMappings.AppKeyProfiles.Count == 6 && !saved.Enabled &&
                saved.EnterRule == AppEnterRule.Default, "equivalent disabled web rule is not replaced or duplicated");
            settings.KeyMappings.AppKeyProfiles.Remove(saved); settings.EnsureDefaults();
            True(settings.KeyMappings.AppKeyProfiles.Count == 5, "schema29 respects a deliberately removed rule");
        }

        private static void AiWebProfileSelectorsSurviveSettingsRoundTrip()
        {
            var settings = AppSettings.CreateDefault();
            var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(AppSettings));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings); stream.Position = 0;
                var restored = (AppSettings)serializer.ReadObject(stream); restored.EnsureDefaults();
                var target = AppKeyProfile.CreateGeminiWebAppDefault();
                var saved = restored.KeyMappings.AppKeyProfiles.Find(p => p.HasSameTarget(target));
                True(saved != null && saved.Enabled && saved.WindowStateClass == target.WindowStateClass &&
                    saved.WindowTitle == target.WindowTitle, "window selectors persist in settings JSON");
            }
        }

        private static void AiCacheTracksWindowSwitchesWithinChrome()
        {
            var cache = new ForegroundAppCache(); var generation = ReadyAiCache(cache);
            var rules = new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
            cache.Update(generation, 2, 77, "com.android.chrome");
            cache.Update(generation, 3, 78, "com.openai.chatgpt");
            True(!ConvertsAiEnter(rules, cache, 77), "browser package alone stays default");
            var window = GeminiWindow();
            True(cache.UpdateWindow(generation, window) && ConvertsAiEnter(rules, cache, 77), "Gemini context enables rule");
            var previous = cache.GetIdentity(77);
            window.WindowId = 99; window.StateClass = "org.chromium.chrome.browser.ChromeTabbedActivity";
            window.Title = "Chrome: 새 탭";
            True(cache.UpdateWindow(generation, window) && !ConvertsAiEnter(rules, cache, 77),
                "same package returning to normal Chrome clears web rule");
            True(previous.WindowId == 42 && previous.WindowTitle == "Google Gemini" &&
                ConvertsAiEnter(rules, cache, 78), "published snapshots are immutable and displays stay separate");
            window = GeminiWindow(77, 123);
            True(cache.UpdateWindow(generation, window) && ConvertsAiEnter(rules, cache, 77),
                "returning to Gemini needs no package-change event or fixed window ID");
            True(!cache.UpdateWindow(generation, window), "identical metadata needs no cache republish");
            cache.Update(generation, 4, 77, "ai.x.grok");
            True(cache.GetIdentity(77).WindowId == -1 && !cache.UpdateWindow(generation, window),
                "new package clears browser details and rejects mismatched metadata");
        }

        private static void AiCacheUsesFocusedWindowInsteadOfEventSource()
        {
            var cache = new ForegroundAppCache(); var generation = ReadyAiCache(cache);
            cache.Update(generation, 2, 77, "com.android.chrome");
            var window = GeminiWindow();
            window.EventPackage = "com.android.systemui"; window.EventClass = "android.widget.FrameLayout";
            window.EventWindowId = 500; window.EventDisplayId = -1;
            cache.UpdateWindow(generation, window);
            var rules = new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
            True(ConvertsAiEnter(rules, cache, 77), "unrelated event source does not replace focused-window identity");
            window.Focused = false; cache.UpdateWindow(generation, window);
            True(!ConvertsAiEnter(rules, cache, 77), "unfocused metadata clears the browser context");
            window.Focused = true; window.WindowType = 2; cache.UpdateWindow(generation, window);
            True(!ConvertsAiEnter(rules, cache, 77), "system windows cannot select an app profile");
        }

        private static void AiCacheWindowDetailsNeverReviveStaleState()
        {
            long now = 1000; var cache = new ForegroundAppCache(() => now);
            var generation = ReadyAiCache(cache); cache.Update(generation, 2, 77, "com.android.chrome");
            var window = GeminiWindow(); cache.UpdateWindow(generation, window);
            now += 7000; window.WindowId++;
            True(cache.UpdateWindow(generation, window), "live metadata can update context");
            now += 1001;
            True(cache.GetIdentity(77) == null && !cache.UpdateWindow(generation, window),
                "metadata does not extend heartbeat liveness");
            cache.Heartbeat(generation);
            True(cache.GetIdentity(77) == null && !cache.UpdateWindow(generation, window),
                "late heartbeat cannot restore expired web context");
            cache.Update(generation, 3, 77, "com.android.chrome"); cache.UpdateWindow(generation, window);
            cache.Update(generation, 4, 77, "");
            True(!cache.UpdateWindow(generation, window), "stale/unknown package cannot be restored by metadata");
            cache.Update(generation, 5, 77, "com.android.chrome"); cache.SetAvailable(generation, 6, false);
            True(!cache.UpdateWindow(generation, window), "disabled accessibility rejects metadata");
            var fresh = ReadyAiCache(cache); cache.Update(fresh, 2, 77, "com.android.chrome");
            True(!cache.UpdateWindow(generation, window) && cache.GetIdentity(77).WindowId == -1,
                "previous connection cannot repopulate new window context");
            cache.UpdateWindow(fresh, window); cache.Disconnect(fresh);
            True(cache.GetIdentity(77) == null, "disconnect clears both app and window details");
        }

        private static MemoryStream AiWindowFrame(string title = "Google Gemini")
        {
            var stream = new MemoryStream();
            foreach(var value in new[]{77,42,1,10}) CompanionGuardianProtocol.WriteInt32(stream,value);
            stream.WriteByte(1); stream.WriteByte(1);
            foreach(var value in new[]{32,77,42}) CompanionGuardianProtocol.WriteInt32(stream,value);
            foreach(var value in new[]{"com.android.chrome","com.android.chrome","WebappActivity",
                "WebappActivity","android.widget.FrameLayout",title})
                CompanionGuardianProtocol.WriteString(stream,value);
            stream.Position=0; return stream;
        }

        private static void AiWindowDiagnosticsPreserveBrowserIdentity()
        {
            using(var stream=AiWindowFrame())
            {
                var details=ForegroundWindowDiagnostic.Read(stream);
                True(details.Package=="com.android.chrome" && details.Title=="Google Gemini" &&
                    details.WindowId==42 && details.DisplayId==77, "web app metadata belongs to the actual browser window");
                var rules=new AppKeyRuleResolver(AppKeyProfile.CreateDefaults());
                True(!rules.ShouldConvertEnter(details.Package,false,false,false,false),
                    "diagnostic title must not turn all Chrome windows into an AI input rule");
            }
        }

        private static void AiWindowDiagnosticsStayOnOneLogLine()
        {
            using(var stream=AiWindowFrame("Google Gemini\r\n\"label\"\u2028next"))
            {
                var line=ForegroundWindowDiagnostic.Read(stream).FormatLog();
                True(line.Contains("AI web: display=77 window=42") &&
                    !line.Contains("\r") && !line.Contains("\n") && !line.Contains("\u2028") &&
                    line.Contains("\\\"label\\\""), "metadata cannot inject another log line");
            }
        }

        private static void AiWindowDiagnosticsRejectOversizedAndTruncatedFrames()
        {
            bool oversized=false, truncated=false;
            try { using(var stream=AiWindowFrame(new string('X',513))) ForegroundWindowDiagnostic.Read(stream); }
            catch(InvalidDataException) { oversized=true; }
            try {
                using(var full=AiWindowFrame())
                using(var stream=new MemoryStream(full.ToArray(),0,(int)full.Length-1))
                    ForegroundWindowDiagnostic.Read(stream);
            } catch(EndOfStreamException) { truncated=true; }
            True(oversized && truncated,"bounded diagnostic reader rejects unsafe/incomplete frames");
        }
        private static void AiGuardianHandlesLongValuesAndTruncation()
        {
            using (var stream = new MemoryStream())
            {
                foreach (var value in new[] { 0L, 1L, long.MaxValue, -1L })
                {
                    stream.SetLength(0); stream.Position = 0;
                    CompanionGuardianProtocol.WriteInt64(stream, value); stream.Position = 0;
                    True(CompanionGuardianProtocol.ReadInt64(stream) == value, "big-endian long round trip");
                }
            }
            bool rejected = false;
            try { CompanionGuardianProtocol.ReadInt64(new MemoryStream(new byte[7])); }
            catch (EndOfStreamException) { rejected = true; }
            True(rejected, "partial frame cannot create a valid state");
        }
    }
}
