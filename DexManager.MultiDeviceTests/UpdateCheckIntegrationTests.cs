using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;

internal static class UpdateCheckIntegrationTests
{
    private static int checks;
    private sealed class MemoryStore : IUpdateCheckStateStore
    {
        internal UpdateCheckState State = new UpdateCheckState();
        internal bool Writable = true;
        public UpdateCheckState Load() { return State.Copy(); }
        public bool TrySave(UpdateCheckState value)
        {
            if (!Writable) return false;
            State = value.Copy();
            return true;
        }
    }
    private sealed class Source : IReleaseUpdateSource
    {
        internal int Calls;
        internal Func<CancellationToken, Task<ReleaseUpdate>> Work;
        public Task<ReleaseUpdate> FetchLatestAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return Work(token);
        }
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }
    private static ReleaseUpdate Release(string tag)
    {
        ReleaseUpdate update;
        if (!ReleaseUpdate.TryCreate(tag, ReleaseUpdate.ProjectUrl + "/releases/tag/" +
            Uri.EscapeDataString(tag), out update)) throw new Exception("bad fixture release");
        return update;
    }
    private static ReleaseUpdate Parse(string json)
    {
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            return GitHubReleaseSource.ParseResponse(stream);
    }
    private static void Throws(Action action, string name)
    {
        try { action(); } catch { Check(true, name); return; }
        throw new Exception(name);
    }
    private static UpdateCheckResult Run(UpdateCheckService service, bool automatic)
    {
        return service.CheckAsync(automatic).GetAwaiter().GetResult();
    }
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--live")
            {
                var watch = Stopwatch.StartNew();
                using (var service = new UpdateCheckService(typeof(UpdateCheckService).Assembly.GetName().Version.ToString(3),
                    new GitHubReleaseSource(), new MemoryStore()))
                {
                    var result = Run(service, false);
                    Console.WriteLine("LIVE GitHub check: " + result.Outcome +
                        "; elapsed=" + watch.ElapsedMilliseconds + "ms");
                    return result.Outcome == UpdateCheckOutcome.Current ||
                        result.Outcome == UpdateCheckOutcome.Available ? 0 : 2;
                }
            }
            Version older, newer;
            Check(ReleaseUpdate.TryParseVersion("v2.9.0", out older) &&
                ReleaseUpdate.TryParseVersion("2.10.0", out newer) &&
                newer.CompareTo(older) > 0, "version comparison uses numeric components");
            Check(ReleaseUpdate.TryParseVersion("2.5.0.0", out older) &&
                ReleaseUpdate.TryParseVersion("v2.5.0", out newer) &&
                newer == older, "three/four-component versions normalize equally");
            Check(!ReleaseUpdate.TryParseVersion("v2.6.0-beta.1", out newer),
                "prerelease version tags are not stable updates");
            Check(!ReleaseUpdate.TryParseVersion("2.999999999999.0", out newer),
                "oversized version components are rejected");
            ReleaseUpdate invalid;
            Check(!ReleaseUpdate.TryCreate("v2.5.1",
                "https://github.com.evil.invalid/maze-mei/DX-Manager/releases/tag/v2.5.1", out invalid),
                "release links cannot leave the project host");
            Check(!ReleaseUpdate.TryCreate("v2.5.1",
                "https://github.com/another/repository/releases/tag/v2.5.1", out invalid),
                "release links cannot select another repository");
            Check(!ReleaseUpdate.TryCreate("v2.5.1",
                "file:///C:/Windows/System32/cmd.exe", out invalid),
                "non-HTTPS release links are rejected");
            Check(!ReleaseUpdate.TryCreate("v2.5.1",
                ReleaseUpdate.ProjectUrl + "/releases/tag/v2.5.2", out invalid),
                "tag and release URL must agree");
            Check(Parse("{\"tag_name\":\"v2.5.1\",\"html_url\":\"https://github.com/maze-mei/DX-Manager/releases/tag/v2.5.1\",\"draft\":false,\"prerelease\":false}").VersionText == "2.5.1",
                "actual GitHub response parser accepts a stable release");
            Check(Parse("{\"tag_name\":\"v2.6.0\",\"draft\":true}") == null,
                "draft responses are ignored");
            Check(Parse("{\"tag_name\":\"v2.6.0\",\"prerelease\":true}") == null,
                "prerelease responses are ignored");
            Throws(delegate { Parse("{broken"); }, "malformed API JSON is rejected");
            Throws(delegate {
                using (var huge = new MemoryStream(new byte[GitHubReleaseSource.MaximumResponseBytes + 1]))
                    GitHubReleaseSource.ParseResponse(huge);
            }, "API response size is bounded");

            var date = new DateTime(2026, 10, 15);
            var store = new MemoryStore();
            var source = new Source { Work = t => Task.FromResult(Release("v2.5.1")) };
            using (var service = new UpdateCheckService("2.5.0", source, store, () => date))
            {
                var first = Run(service, true);
                Check(first.Outcome == UpdateCheckOutcome.Available && first.Notify,
                    "a newer stable version requests one automatic notice");
                Check(store.State.LastAttemptDay == "2026-10-15",
                    "automatic attempt is persisted before networking");
                Check(Run(service, true).Outcome == UpdateCheckOutcome.NotDue && source.Calls == 1,
                    "automatic checks run only once per local day");
                Check(service.KnownUpdate.VersionText == "2.5.1",
                    "a discovered update is cached for the main-window banner");
                Check(Run(service, false).Outcome == UpdateCheckOutcome.Available && source.Calls == 2,
                    "manual checking remains available on the same day");
                date = date.AddDays(1);
                Check(!Run(service, true).Notify && source.Calls == 3,
                    "the same version does not repeat the automatic notice tomorrow");
                service.Dismiss(Release("v2.5.1"));
                Check(service.KnownUpdate == null, "dismissal hides the pending banner");
            }
            using (var restarted = new UpdateCheckService("2.5.0", source, store, () => date))
            {
                Check(Run(restarted, true).Outcome == UpdateCheckOutcome.NotDue,
                    "same-day app restart does not repeat the request");
                Check(restarted.KnownUpdate == null, "banner dismissal survives app restart");
                date = date.AddDays(1);
                source.Work = t => Task.FromResult(Release("v2.5.2"));
                Check(Run(restarted, true).Notify && restarted.KnownUpdate.VersionText == "2.5.2",
                    "a later new version can notify again");
            }

            var offlineStore = new MemoryStore();
            var offline = new Source { Work = t => Task.FromException<ReleaseUpdate>(
                new IOException("offline fixture")) };
            using (var service = new UpdateCheckService("2.5.0", offline, offlineStore, () => date))
            {
                Check(Run(service, true).Outcome == UpdateCheckOutcome.Failed,
                    "offline automatic check returns failure without a retry loop");
                Check(offline.Calls == 1 && Run(service, true).Outcome == UpdateCheckOutcome.NotDue,
                    "offline failure still consumes the daily automatic attempt");
            }
            using (var restarted = new UpdateCheckService("2.5.0", offline, offlineStore, () => date))
            {
                Check(Run(restarted, true).Outcome == UpdateCheckOutcome.NotDue && offline.Calls == 1,
                    "offline app restart does not retry");
                Check(Run(restarted, false).Outcome == UpdateCheckOutcome.Failed && offline.Calls == 2,
                    "offline manual check is one explicit additional attempt");
            }
            var denied = new MemoryStore { Writable = false };
            var deniedSource = new Source { Work = t => Task.FromResult(Release("v2.5.1")) };
            using (var service = new UpdateCheckService("2.5.0", deniedSource, denied, () => date))
            {
                Check(Run(service, true).Outcome == UpdateCheckOutcome.NotDue && deniedSource.Calls == 0,
                    "failed daily-state persistence prevents automatic network requests");
                Check(Run(service, false).Outcome == UpdateCheckOutcome.Available,
                    "manual checking works if automatic-state storage is unavailable");
            }

            var completion = new TaskCompletionSource<ReleaseUpdate>();
            var entered = new ManualResetEventSlim();
            var cancelled = false;
            var slow = new Source { Work = t => {
                t.Register(delegate { cancelled = true; });
                entered.Set();
                return completion.Task;
            }};
            var slowStore = new MemoryStore();
            using (var service = new UpdateCheckService("2.5.0", slow, slowStore, () => date, null, 150))
            {
                var watch = Stopwatch.StartNew();
                var pending = service.CheckAsync(true);
                Check(entered.Wait(2000), "network fixture begins on a background worker");
                Check(Run(service, false).Outcome == UpdateCheckOutcome.Busy && slow.Calls == 1,
                    "overlapping manual checks do not start another request");
                Check(pending.GetAwaiter().GetResult().Outcome == UpdateCheckOutcome.Failed &&
                    watch.ElapsedMilliseconds < 2000, "a stalled response is bounded by the overall deadline");
                Check(SpinWait.SpinUntil(() => cancelled, 1000), "the timed-out network request is asked to abort asynchronously");
                completion.SetResult(Release("v9.0.0"));
                Thread.Sleep(40);
                Check(service.KnownUpdate == null && slowStore.State.KnownVersion == null,
                    "a late response cannot turn a failed check into an update notice");
                Check(Run(service, true).Outcome == UpdateCheckOutcome.NotDue,
                    "a timeout does not schedule another automatic request");
            }
            using (var service = new UpdateCheckService("2.5.0",
                new Source { Work = t => Task.FromResult(Release("v2.0.1")) },
                new MemoryStore(), () => date))
                Check(Run(service, true).Outcome == UpdateCheckOutcome.Current,
                    "an older GitHub release does not trigger an update");
            using (var service = new UpdateCheckService("2.5.0",
                new Source { Work = t => Task.FromResult(Release("v2.5.0")) },
                new MemoryStore(), () => date))
                Check(Run(service, true).Outcome == UpdateCheckOutcome.Current,
                    "an equal release does not trigger an update");

            var abortEntered = new ManualResetEventSlim();
            var unblockAbort = new ManualResetEventSlim();
            var abortResponse = new TaskCompletionSource<ReleaseUpdate>();
            var abortSource = new Source { Work = token => {
                token.Register(delegate { unblockAbort.Wait(1500); });
                abortEntered.Set();
                return abortResponse.Task;
            }};
            using (var service = new UpdateCheckService("2.5.0", abortSource,
                new MemoryStore(), () => date, null, 80))
            {
                var pending = service.CheckAsync(true);
                Check(abortEntered.Wait(2000), "deadline fixture starts a request with slow abort");
                var ended = pending.Wait(500);
                unblockAbort.Set();
                abortResponse.SetResult(Release("v9.0.0"));
                Check(ended && pending.GetAwaiter().GetResult().Outcome == UpdateCheckOutcome.Failed,
                    "overall deadline does not wait for a slow network-abort callback");
                Check(service.KnownUpdate == null, "slow-abort late response remains ignored");
            }
            var closeEntered = new ManualResetEventSlim();
            var releaseAbort = new ManualResetEventSlim();
            var closingResponse = new TaskCompletionSource<ReleaseUpdate>();
            var closingSource = new Source { Work = token => {
                token.Register(delegate { releaseAbort.Wait(2000); });
                closeEntered.Set();
                return closingResponse.Task;
            }};
            var closingService = new UpdateCheckService("2.5.0", closingSource,
                new MemoryStore(), () => date, null, 3000);
            var closingTask = closingService.CheckAsync(true);
            Check(closeEntered.Wait(2000), "shutdown fixture has an outstanding request");
            var closeWatch = Stopwatch.StartNew();
            closingService.Dispose();
            Check(closeWatch.ElapsedMilliseconds < 250,
                "app shutdown does not wait for a slow network-abort callback");
            releaseAbort.Set();
            closingResponse.SetResult(Release("v9.0.0"));
            Check(closingTask.GetAwaiter().GetResult().Outcome == UpdateCheckOutcome.Cancelled,
                "shutdown prevents a late available-update result");
            var diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "update-state-fixture", "update-check.json");
            if (File.Exists(diskPath)) File.Delete(diskPath);
            var diskStore = new FileUpdateCheckStateStore(diskPath, message => Console.WriteLine("STATE: " + message));
            using (var service = new UpdateCheckService("2.5.0",
                new Source { Work = t => Task.FromResult(Release("v2.5.1")) },
                diskStore, () => date))
                Run(service, true);
            var reloaded = new FileUpdateCheckStateStore(diskPath, null).Load();
            Check(reloaded.LastAttemptDay == date.ToString("yyyy-MM-dd") &&
                reloaded.KnownVersion == "v2.5.1",
                "actual atomic state file survives a new store instance");
            Check(!File.Exists(diskPath + ".tmp"), "successful state writes leave no temporary file");

            var product = typeof(UpdateCheckService).Assembly;
            var settings = DexManager.Models.AppSettings.CreateDefault();
            Check(settings.KeyMappings.ConvertEnterToShiftEnter && !settings.KeyMappings.AutoAppEnterEnabled,
                "v3 uses manual Enter switching for fresh settings with AI opt-in");
            var protocol = product.GetType("DexManager.Services.CompanionGuardianProtocol");
            Check((int)protocol.GetField("Version").GetRawConstantValue() == 3,
                "Companion guardian uses the foreground event protocol");
            Check((int)product.GetType("DexManager.Services.DisplayCleanupPermissionService").GetField("BundledVersionCode").GetRawConstantValue() == 16 &&
                (string)product.GetType("DexManager.Services.DisplayCleanupPermissionService").GetField("BundledVersionName").GetRawConstantValue() == "3.0.0",
                "official assembly uses shared Companion 3.0.0/code16");

            Console.WriteLine("PASS update integration: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
