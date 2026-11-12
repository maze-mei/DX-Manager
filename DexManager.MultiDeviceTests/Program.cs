using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Services;
using DexManager.Utils;

namespace DexManager.MultiDeviceTests
{
    internal static partial class Program
    {
        private static int _passed;

        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--spawn-output-holder")
            {
                using (var holder = Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = "--hold-output",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    Console.WriteLine("holder-pid=" + holder.Id);
                }
                return 0;
            }
            if (args.Length == 1 && args[0] == "--hold-output")
            {
                Thread.Sleep(5000);
                return 0;
            }

            var tests = new Action[]
            {
                MergesUsbAndWirelessForSamePhysicalDevice,
                HonorsExplicitAuthorizedTransport,
                NewLaunchPrefersUsbWithoutMigratingRunningWireless,
                NewLaunchUsesOnlyAuthorizedTransportOfThatPhone,
                UsbPolicyDoesNotFallBackToWireless,
                WirelessPolicyDoesNotFallBackToUsb,
                KeepsDifferentPhysicalDevicesSeparate,
                OrdersStartupDevicesByModelGeneration,
                PreservesSequentialConnectionOrder,
                CreatesTemporaryIdentityWhenStableIdentityIsMissing,
                IgnoresTimestampOnlyRefreshes,
                PublishesMeaningfulStatusChanges,
                RemovesMissingTransportsOnReconcile,
                ReturnsDefensiveSnapshots,
                PrefersStableAuthorizedDuplicateObservation,
                PreservesKnownDisplayName,
                PreservesKnownIdentityWhenTransportCannotBeQueried,
                RequiresExplicitSerialForDeviceCommands,
                KeepsCleanupCommandsScopedToRequestedDevice,
                CombinesWindowsShutdownCleanupIntoOneDeviceCommand,
                InterleavedDeviceCommandsDoNotShareTarget,
                DeviceCancellationMatchesOnlyRequestedSerial,
                CreatesIndependentRuntimeSessions,
                KeepsRuntimeUpdatesScopedToOnePhysicalDevice,
                SharesRuntimeAcrossUsbAndWirelessTransports,
                MigratesTemporaryRuntimeIdentity,
                PreservesRuntimeStateAcrossDisconnect,
                IgnoresUnchangedRuntimeReconciles,
                BindsOneServiceInstancePerPhysicalDevice,
                KeepsBoundRuntimeWhenPreferredTransportChanges,
                KeepsRunSettingsIndependentPerPhysicalDevice,
                SeedsNewDeviceSettingsFromLegacyTemplate,
                PersistsDeviceRunSettingsProfiles,
                KeepsWirelessSettingsIndependentPerPhysicalDevice,
                SeedsSelectedWirelessDeviceFromLegacyConnection,
                PersistsDeviceWirelessConnectionProfiles,
                RoundTripsCompanionGuardianProtocolPayload,
                ParsesAndroidMdnsConnectAndPairingServices,
                ResolvesMdnsEndpointOnlyForTheMatchingPhone,
                RejectsStaleWirelessTransportOwnership,
                KeepsChangedWirelessPolicyDuringDiscovery,
                SerializesWirelessPolicyEditsWithDiscoverySave,
                ResolvesDiagnosticTargetsByActivePhysicalDevice,
                DiagnosticReportsIncludeAllPhonesWithoutPrivateIdentifiers,
                DiscardsAppListsLoadedForAnotherPhone,
                KeepsPhoneAppCataloguesIndependent,
                PersistsPhoneRecentAppsIndependently,
                BlocksNewProcessesAfterShutdown,
                TerminatesActiveProcessOnShutdown,
                KeepsProcessOutputDrainBounded,
                KeepsTransferOutputDrainBounded,
                TerminatesOnlyConfiguredBundledExecutablePath,
                SerializesConcurrentSettingsSaves,
                PreservesSharedStorageRootTransferTargets,
                KeepsSanitizedSiblingNamesDistinct,
                UsesShortPartialNameForLongFinalFile,
                EncodesCompanionTransferHistoryCommand,
                RemembersOnlyNewestCustomResolutions,
                AppliesSavedSingleWindowAppProfile,
                KeepsOnboardingAcknowledgementOutsideSettingsReset,
                ShowsConsentOnlyUntilAccepted,
                RejectsCorruptOrUnwritableOnboardingState,
                AiProfilesTargetOnlyExplicitNativeApps,
                AiProfilesKeepModifiedEnterUnchanged,
                AiProfilesRespectDisabledAndDefaultRules,
                AiCacheSeparatesDisplaysAndPhysicalDevices,
                AiCacheRejectsOldConnectionMessages,
                AiCacheClearsOnServiceAndTransportLoss,
                AiCacheExpiresWithoutLiveness,
                AiCacheKeepsUnchangedAppsWithHeartbeats,
                AiCacheRejectsOutOfOrderMessages,
                AiCacheRejectsInvalidPackages,
                AiProfilesSurviveSettingsRoundTrip,
                AiManualModeKeepsScrollLockPriority,
                AiGeminiWebRuleRequiresVerifiedWindowIdentity,
                AiWebProfilesKeepMultipleTargetsAndExplicitPriority,
                AiGeminiWebProfileMigrationPreservesCustomSettings,
                AiWebProfileSelectorsSurviveSettingsRoundTrip,
                AiCacheTracksWindowSwitchesWithinChrome,
                AiCacheUsesFocusedWindowInsteadOfEventSource,
                AiCacheWindowDetailsNeverReviveStaleState,
                AiWindowDiagnosticsPreserveBrowserIdentity,
                AiWindowDiagnosticsStayOnOneLogLine,
                AiWindowDiagnosticsRejectOversizedAndTruncatedFrames,
                AiGuardianHandlesLongValuesAndTruncation
            };

            try
            {
                foreach (var test in tests)
                {
                    test();
                    _passed++;
                    Console.WriteLine("PASS " + test.Method.Name);
                }

                Console.WriteLine(
                    "All multi-device foundation tests passed: " + _passed);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "FAIL after " + _passed + " tests: " + ex.Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void KeepsOnboardingAcknowledgementOutsideSettingsReset()
        {
            var folder = Path.Combine(Path.GetTempPath(), "dx-onboarding-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var service = new PortableOnboardingService(folder);
            True(service.RequiresNotice(), "first launch must require acknowledgement");
            string error;
            True(service.TryAccept(out error), "acknowledgement must be saved");
            True(!service.RequiresNotice(), "accepted notice must not repeat");
            File.WriteAllText(Path.Combine(folder, "settings.json"), "{}");
            File.Delete(Path.Combine(folder, "settings.json"));
            True(!new PortableOnboardingService(folder).RequiresNotice(), "settings reset cannot remove acknowledgement");
        }
        private static void ShowsConsentOnlyUntilAccepted()
        {
            var folder=Path.Combine(Path.GetTempPath(),"dx-onboarding-final-"+Guid.NewGuid().ToString("N"));
            var service=new PortableOnboardingService(folder);
            True(service.ShouldShowNoticeForLaunch(),"fresh profile must show consent");
            True(new PortableOnboardingService(folder).ShouldShowNoticeForLaunch(),"declining without acknowledgement must not bypass consent");
            string error;
            True(service.TryAccept(out error),"acknowledgement must be saved");
            var path=Path.Combine(folder,"onboarding.json");
            var accepted=File.ReadAllText(path);
            True(!new PortableOnboardingService(folder).ShouldShowNoticeForLaunch(),"second launch must respect acknowledgement");
            True(!new PortableOnboardingService(folder).ShouldShowNoticeForLaunch(),"later launches must not repeat consent");
            Equal(accepted,File.ReadAllText(path),"checking first-run state must not change acknowledgement data");
        }
        private static void RejectsCorruptOrUnwritableOnboardingState()
        {
            var folder = Path.Combine(Path.GetTempPath(), "dx-onboarding-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "onboarding.json"), "broken");
            True(new PortableOnboardingService(folder).RequiresNotice(), "corruption must show the notice again");
            var blocker = Path.Combine(folder, "file-instead-of-directory");
            File.WriteAllText(blocker, "block");
            string error;
            True(!new PortableOnboardingService(blocker).TryAccept(out error), "unwritable state must report failure");
            True(!string.IsNullOrWhiteSpace(error), "save failure must have an explanation");
        }
        private static void MergesUsbAndWirelessForSamePhysicalDevice()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(1, snapshot.Devices.Count, "same identity must merge");
            Equal(2, snapshot.Devices[0].Transports.Count, "both transports must remain");
            Equal(
                "USB-A",
                snapshot.Devices[0].SelectPreferredTransport(null).Serial,
                "USB must be the default transport");
        }

        private static void NewLaunchPrefersUsbWithoutMigratingRunningWireless()
        {
            var device = new PhysicalDeviceRegistry().Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "wifi-a:5555", DeviceTransportKind.Wireless)
            }).Devices[0];
            Equal("USB-A", device.SelectLaunchTransport("wifi-a:5555").Serial, "new launch must prefer USB");
            Equal("wifi-a:5555", device.SelectRuntimeTransport("wifi-a:5555", true).Serial, "plugging USB must not migrate a running Wi-Fi session");
            Equal("USB-A", device.SelectRuntimeTransport("wifi-a:5555", false).Serial, "idle context must use USB");
            device.FindTransport("USB-A").Status = AdbDeviceStatus.Offline;
            Equal("wifi-a:5555", device.SelectLaunchTransport("USB-A").Serial, "next manual launch must use remaining Wi-Fi");
        }

        private static void NewLaunchUsesOnlyAuthorizedTransportOfThatPhone()
        {
            var snapshot = new PhysicalDeviceRegistry().Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Unauthorized),
                Device("phone-a", "Galaxy A", "wifi-a:5555", DeviceTransportKind.Wireless),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            });
            var phone = snapshot.FindByIdentity("phone-a");
            Equal("wifi-a:5555", phone.SelectLaunchTransport("USB-B").Serial, "other phone's USB cannot win");
            phone.FindTransport("wifi-a:5555").Status = AdbDeviceStatus.Offline;
            True(phone.SelectLaunchTransport(null) == null, "unauthorized/offline endpoints cannot launch");
            var usb = snapshot.FindByIdentity("phone-b");
            Equal("USB-B", usb.SelectLaunchTransport(null).Serial, "USB-only phone must launch");
        }

        private static void HonorsExplicitAuthorizedTransport()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(
                "192.168.0.2:5555",
                snapshot.Devices[0]
                    .SelectPreferredTransport("192.168.0.2:5555").Serial,
                "explicit authorized transport must win");
        }

        private static void UsbPolicyDoesNotFallBackToWireless()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "192.168.0.2:5555",
                    DeviceTransportKind.Wireless)
            });

            True(
                snapshot.Devices[0].SelectAuthorizedTransport(
                    DeviceTransportKind.Usb,
                    string.Empty) == null,
                "USB policy must wait for USB instead of using wireless");
        }

        private static void WirelessPolicyDoesNotFallBackToUsb()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "USB-A",
                    DeviceTransportKind.Usb)
            });

            True(
                snapshot.Devices[0].SelectAuthorizedTransport(
                    DeviceTransportKind.Wireless,
                    "192.168.0.2:5555") == null,
                "wireless policy must wait instead of using USB");
        }

        private static void KeepsDifferentPhysicalDevicesSeparate()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy", "USB-A", DeviceTransportKind.Usb),
                Device("phone-b", "Galaxy", "USB-B", DeviceTransportKind.Usb)
            });

            Equal(2, snapshot.Devices.Count, "display name must not merge devices");
            NotNull(snapshot.FindByIdentity("phone-a"), "phone-a missing");
            NotNull(snapshot.FindByIdentity("phone-b"), "phone-b missing");
        }

        private static void OrdersStartupDevicesByModelGeneration()
        {
            var order = new DevicePresentationOrder();
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(
                    "phone-old",
                    "현호의 Galaxy S20 FE 5G",
                    "USB-OLD",
                    DeviceTransportKind.Usb),
                Device(
                    "phone-new",
                    "현호의 S26 Ultra",
                    "USB-NEW",
                    DeviceTransportKind.Usb)
            });
            order.Reconcile(snapshot.Devices);

            var identities = order.GetIdentities();
            Equal(2, identities.Count, "both startup devices must be ordered");
            Equal(
                "phone-new",
                identities[0],
                "newer Galaxy generation must be shown first at startup");
        }

        private static void PreservesSequentialConnectionOrder()
        {
            var order = new DevicePresentationOrder();
            var registry = new PhysicalDeviceRegistry();
            var first = registry.Reconcile(new[]
            {
                Device(
                    "phone-old",
                    "현호의 Galaxy S20 FE 5G",
                    "USB-OLD",
                    DeviceTransportKind.Usb)
            });
            order.Reconcile(first.Devices);
            var second = registry.Reconcile(new[]
            {
                Device(
                    "phone-new",
                    "현호의 S26 Ultra",
                    "USB-NEW",
                    DeviceTransportKind.Usb),
                Device(
                    "phone-old",
                    "현호의 Galaxy S20 FE 5G",
                    "USB-OLD",
                    DeviceTransportKind.Usb)
            });
            order.Reconcile(second.Devices);

            var identities = order.GetIdentities();
            Equal(
                "phone-old",
                identities[0],
                "first connected phone must keep the first position");
            Equal(
                "phone-new",
                identities[1],
                "later connection must be appended");
        }

        private static void CreatesTemporaryIdentityWhenStableIdentityIsMissing()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(null, null, "USB-PENDING", DeviceTransportKind.Usb)
            });

            Equal(
                "transport:USB-PENDING",
                snapshot.Devices[0].Identity,
                "temporary identity must be transport-scoped");
            True(
                PhysicalDeviceRegistry.IsTemporaryIdentity(
                    snapshot.Devices[0].Identity),
                "temporary identity must be recognizable");
        }

        private static void IgnoresTimestampOnlyRefreshes()
        {
            var registry = new PhysicalDeviceRegistry();
            var eventCount = 0;
            registry.SnapshotChanged += delegate { eventCount++; };
            var first = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var second = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });

            Equal(1L, first.Generation, "first discovery must increment generation");
            Equal(first.Generation, second.Generation, "refresh must not increment generation");
            Equal(1, eventCount, "refresh must not publish a duplicate event");
        }

        private static void PublishesMeaningfulStatusChanges()
        {
            var registry = new PhysicalDeviceRegistry();
            var eventCount = 0;
            registry.SnapshotChanged += delegate { eventCount++; };
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var changed = registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "USB-A",
                    DeviceTransportKind.Usb,
                    AdbDeviceStatus.Offline)
            });

            Equal(2L, changed.Generation, "status change must increment generation");
            Equal(2, eventCount, "status change must publish an event");
            True(!changed.Devices[0].IsConnected, "offline device must not be connected");
        }

        private static void RemovesMissingTransportsOnReconcile()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "192.168.0.2:5555", DeviceTransportKind.Wireless)
            });

            Equal(1, snapshot.Devices[0].Transports.Count, "missing transport must be removed");
            Equal(
                "192.168.0.2:5555",
                snapshot.Devices[0].Transports[0].Serial,
                "remaining transport is wrong");
        }

        private static void ReturnsDefensiveSnapshots()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            snapshot.Devices.Clear();

            Equal(1, registry.Current.Devices.Count, "caller must not mutate registry state");
        }

        private static void PrefersStableAuthorizedDuplicateObservation()
        {
            var registry = new PhysicalDeviceRegistry();
            var snapshot = registry.Reconcile(new[]
            {
                Device(null, null, "USB-A", DeviceTransportKind.Usb, AdbDeviceStatus.Offline),
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });

            Equal(1, snapshot.Devices.Count, "duplicate serial must collapse");
            Equal("phone-a", snapshot.Devices[0].Identity, "stable identity must win");
            True(snapshot.Devices[0].IsConnected, "authorized observation must win");
        }

        private static void PreservesKnownDisplayName()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device("phone-a", null, "USB-A", DeviceTransportKind.Usb)
            });

            Equal("Galaxy A", snapshot.Devices[0].DisplayName, "known name must be cached");
        }

        private static void PreservesKnownIdentityWhenTransportCannotBeQueried()
        {
            var registry = new PhysicalDeviceRegistry();
            registry.Reconcile(new[]
            {
                Device(
                    "phone-a",
                    "Galaxy A",
                    "192.168.0.2:5555",
                    DeviceTransportKind.Wireless)
            });
            var snapshot = registry.Reconcile(new[]
            {
                Device(
                    null,
                    null,
                    "192.168.0.2:5555",
                    DeviceTransportKind.Wireless,
                    AdbDeviceStatus.Offline)
            });

            Equal(
                "phone-a",
                snapshot.Devices[0].Identity,
                "known transport must remain attached to its physical device");
            Equal(
                "Galaxy A",
                snapshot.Devices[0].DisplayName,
                "known device name must remain available while offline");
        }

        private static void RequiresExplicitSerialForDeviceCommands()
        {
            Throws<ArgumentException>(delegate
            {
                AdbCommandBuilder.ForDevice(
                    string.Empty,
                    "shell get-state");
            }, "device command without a serial must be rejected");
        }

        private static void KeepsCleanupCommandsScopedToRequestedDevice()
        {
            var first = AdbCommandBuilder.ForDevice(
                "PHONE-A",
                "shell settings delete global overlay_display_devices");
            var second = AdbCommandBuilder.ForDevice(
                "PHONE-B",
                "shell settings delete global overlay_display_devices");

            True(first.StartsWith("-s \"PHONE-A\" "),
                "first cleanup must target PHONE-A");
            True(first.IndexOf("PHONE-B", StringComparison.Ordinal) < 0,
                "first cleanup must not contain PHONE-B");
            True(second.StartsWith("-s \"PHONE-B\" "),
                "second cleanup must target PHONE-B");
            True(second.IndexOf("PHONE-A", StringComparison.Ordinal) < 0,
                "second cleanup must not contain PHONE-A");
        }

        private static void CombinesWindowsShutdownCleanupIntoOneDeviceCommand()
        {
            var command = AdbCommandBuilder.ForShellCommands(
                "PHONE-A",
                "settings delete global overlay_display_devices",
                "settings put global stay_on_while_plugged_in 3");

            True(command.StartsWith("-s \"PHONE-A\" shell \""),
                "shutdown cleanup must remain scoped to PHONE-A");
            True(command.IndexOf(
                    "overlay_display_devices; settings put global " +
                    "stay_on_while_plugged_in 3",
                    StringComparison.Ordinal) >= 0,
                "shutdown cleanup must use one combined shell command");
            True(command.IndexOf("PHONE-B", StringComparison.Ordinal) < 0,
                "shutdown cleanup must not leak another target");
        }

        private static void InterleavedDeviceCommandsDoNotShareTarget()
        {
            for (var index = 0; index < 1000; index++)
            {
                var serial = index % 2 == 0 ? "PHONE-A" : "PHONE-B";
                var other = index % 2 == 0 ? "PHONE-B" : "PHONE-A";
                var command = AdbCommandBuilder.ForDevice(
                    serial,
                    "shell echo " + index);
                True(command.StartsWith("-s \"" + serial + "\" "),
                    "interleaved command changed its requested target");
                True(command.IndexOf(other, StringComparison.Ordinal) < 0,
                    "interleaved command leaked another target");
            }
        }

        private static void DeviceCancellationMatchesOnlyRequestedSerial()
        {
            True(
                DeviceSerialScope.Matches("PHONE-A", "phone-a"),
                "requested device cancellation must match its own serial");
            True(
                !DeviceSerialScope.Matches("PHONE-A", "PHONE-B"),
                "requested device cancellation must not match another serial");
            True(
                !DeviceSerialScope.Matches(string.Empty, "PHONE-A"),
                "empty cancellation scope must not match a device");
        }

        private static void CreatesIndependentRuntimeSessions()
        {
            var registry = CreateRuntimeRegistry();
            var snapshot = registry.Current;
            Equal(2, snapshot.Sessions.Count,
                "each physical device needs its own runtime session");
            NotNull(snapshot.FindByIdentity("phone-a"),
                "phone-a runtime missing");
            NotNull(snapshot.FindByIdentity("phone-b"),
                "phone-b runtime missing");
        }

        private static void KeepsRuntimeUpdatesScopedToOnePhysicalDevice()
        {
            var registry = CreateRuntimeRegistry();
            registry.SetDexSession("USB-A", new ManagedDisplaySession
            {
                Serial = "USB-A",
                DisplayId = 12,
                ScrcpyProcessId = 101,
                DisplayLease = new VirtualDisplayLease
                {
                    Serial = "USB-A",
                    DisplayId = 12,
                    OwnsOverlaySetting = true
                }
            });
            registry.SetSingleWindow(
                "USB-B", 1, 21, 202, new IntPtr(303), true, false);
            registry.SetCompanionAttached("USB-B", true);
            registry.SetPcToPhoneTransferState("USB-B", 1, 4);
            registry.SetPhonePowerState(
                "USB-A", true, true, "0", true);

            var snapshot = registry.Current;
            var first = snapshot.FindByIdentity("phone-a");
            var second = snapshot.FindByIdentity("phone-b");
            True(first.Dex.IsRunning, "phone-a DeX state missing");
            Equal(0, first.SingleWindows.Count,
                "phone-b single window leaked into phone-a");
            True(first.PhonePower.ScreenOffRequested,
                "phone-a power state missing");
            True(!second.Dex.IsRunning,
                "phone-a DeX state leaked into phone-b");
            Equal(1, second.SingleWindows.Count,
                "phone-b single window state missing");
            True(second.Companion.IsAttached,
                "phone-b companion state missing");
            Equal(4, second.Transfers.QueuedPcToPhoneItems,
                "phone-b transfer queue state missing");
        }

        private static void SharesRuntimeAcrossUsbAndWirelessTransports()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless)
            }));
            runtime.SetCompanionAttached("10.0.0.2:5555", true);

            var snapshot = runtime.Current;
            Equal(1, snapshot.Sessions.Count,
                "USB and wireless transports must share one runtime");
            True(snapshot.FindByTransportSerial("USB-A")
                    .Companion.IsAttached,
                "wireless update must be visible through USB alias");
        }

        private static void MigratesTemporaryRuntimeIdentity()
        {
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.SetPhonePowerState(
                "USB-A", true, false, string.Empty, true);
            var physical = new PhysicalDeviceRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            }));

            var snapshot = runtime.Current;
            Equal(1, snapshot.Sessions.Count,
                "temporary runtime must migrate, not duplicate");
            var migrated = snapshot.FindByIdentity("phone-a");
            NotNull(migrated, "stable runtime identity missing");
            True(migrated.PhonePower.ScreenOffRequested,
                "temporary runtime state was lost during migration");
        }

        private static void PreservesRuntimeStateAcrossDisconnect()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            }));
            runtime.SetPcToPhoneTransferState("USB-A", 1, 2);
            runtime.Reconcile(physical.Reset());

            var session = runtime.Current.FindByIdentity("phone-a");
            NotNull(session, "disconnected runtime must remain for cleanup");
            True(!session.IsConnected,
                "disconnected runtime must be marked offline");
            Equal(2, session.Transfers.QueuedPcToPhoneItems,
                "disconnect must not erase cleanup evidence");
        }

        private static void IgnoresUnchangedRuntimeReconciles()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            var devices = new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb)
            };
            runtime.Reconcile(physical.Reconcile(devices));
            var before = runtime.Current;
            var eventCount = 0;
            runtime.Changed += delegate { eventCount++; };

            runtime.Reconcile(physical.Reconcile(devices));

            var after = runtime.Current;
            Equal(0, eventCount,
                "timestamp-only runtime refresh must not publish an event");
            Equal(before.Generation, after.Generation,
                "timestamp-only runtime refresh must not change generation");
            Equal(
                before.Sessions[0].Revision,
                after.Sessions[0].Revision,
                "timestamp-only runtime refresh must not change revision");
        }

        private static void BindsOneServiceInstancePerPhysicalDevice()
        {
            var registry = CreateRuntimeRegistry();
            var firstServices = Guid.NewGuid();
            var secondServices = Guid.NewGuid();
            var eventCount = 0;
            registry.Changed += delegate { eventCount++; };

            registry.BindServiceInstance("USB-A", firstServices);
            registry.BindServiceInstance("USB-A", firstServices);
            registry.BindServiceInstance("USB-B", secondServices);

            var snapshot = registry.Current;
            Equal(
                firstServices,
                snapshot.FindByIdentity("phone-a").ServiceInstanceId,
                "phone-a service binding missing");
            Equal(
                secondServices,
                snapshot.FindByIdentity("phone-b").ServiceInstanceId,
                "phone-b service binding missing");
            Equal(2, eventCount,
                "rebinding the same service must not publish an event");
            Throws<InvalidOperationException>(delegate
            {
                registry.BindServiceInstance("USB-A", secondServices);
            }, "a physical device must not be rebound to another service set");
        }

        private static void KeepsBoundRuntimeWhenPreferredTransportChanges()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));
            var phoneAService = Guid.NewGuid();
            var phoneBService = Guid.NewGuid();
            runtime.BindServiceInstance("USB-A", phoneAService);
            runtime.BindServiceInstance("USB-B", phoneBService);
            runtime.SetDexSession("USB-A", new ManagedDisplaySession
            {
                Serial = "USB-A",
                DisplayId = 21,
                ScrcpyProcessId = 101
            });

            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "10.0.0.2:5555", DeviceTransportKind.Wireless),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));

            var snapshot = runtime.Current;
            var phoneA = snapshot.FindByIdentity("phone-a");
            var phoneB = snapshot.FindByIdentity("phone-b");
            Equal(phoneAService, phoneA.ServiceInstanceId,
                "phone-a tab must retain its runtime after transport change");
            Equal("10.0.0.2:5555", phoneA.ActiveTransportSerial,
                "phone-a tab must select the remaining wireless transport");
            True(phoneA.Dex.IsRunning,
                "phone-a runtime evidence must survive transport change");
            Equal(phoneBService, phoneB.ServiceInstanceId,
                "phone-b runtime binding must remain isolated");
            True(!phoneB.Dex.IsRunning,
                "phone-a session must not leak into phone-b tab");
        }

        private static DeviceRuntimeSessionRegistry CreateRuntimeRegistry()
        {
            var physical = new PhysicalDeviceRegistry();
            var runtime = new DeviceRuntimeSessionRegistry();
            runtime.Reconcile(physical.Reconcile(new[]
            {
                Device("phone-a", "Galaxy A", "USB-A", DeviceTransportKind.Usb),
                Device("phone-b", "Galaxy B", "USB-B", DeviceTransportKind.Usb)
            }));
            return runtime;
        }

        private static void KeepsRunSettingsIndependentPerPhysicalDevice()
        {
            var settings = AppSettings.CreateDefault();
            var phoneA = settings.GetOrCreateDeviceRunSettings("phone-a");
            var phoneB = settings.GetOrCreateDeviceRunSettings("phone-b");

            phoneA.VirtualDisplay.Width = 1920;
            phoneA.VirtualDisplay.Height = 1080;
            phoneA.VirtualDisplay.Dpi = 240;
            phoneA.SingleWindowSlots[0].Width = 1280;
            phoneA.SingleWindowSlots[0].StartAppPackage = "app.phone.a";
            phoneA.Scrcpy.BitRate = "20M";

            Equal(1600, phoneB.VirtualDisplay.Width,
                "phone-b DeX resolution must not follow phone-a");
            Equal(900, phoneB.VirtualDisplay.Height,
                "phone-b DeX height must remain independent");
            Equal(150, phoneB.VirtualDisplay.Dpi,
                "phone-b DeX DPI must remain independent");
            Equal(1600, phoneB.SingleWindowSlots[0].Width,
                "phone-b single-window resolution must remain independent");
            True(string.IsNullOrEmpty(
                    phoneB.SingleWindowSlots[0].StartAppPackage),
                "phone-b selected app must not follow phone-a");
            Equal("8M", phoneB.Scrcpy.BitRate,
                "phone-b scrcpy bitrate must remain independent");
        }

        private static void SeedsNewDeviceSettingsFromLegacyTemplate()
        {
            var settings = AppSettings.CreateDefault();
            settings.VirtualDisplay.Width = 2560;
            settings.VirtualDisplay.Height = 1440;
            settings.VirtualDisplay.Dpi = 180;
            settings.Scrcpy.BitRate = "30M";
            settings.SingleWindowSlots[1].Width = 1200;
            settings.SingleWindowSlots[1].Height = 800;

            var migrated = settings.GetOrCreateDeviceRunSettings(
                "existing-phone");

            Equal(2560, migrated.VirtualDisplay.Width,
                "first device profile must preserve existing DeX width");
            Equal(1440, migrated.VirtualDisplay.Height,
                "first device profile must preserve existing DeX height");
            Equal(180, migrated.VirtualDisplay.Dpi,
                "first device profile must preserve existing DPI");
            Equal("30M", migrated.Scrcpy.BitRate,
                "first device profile must preserve existing bitrate");
            Equal(1200, migrated.SingleWindowSlots[1].Width,
                "first device profile must preserve existing slot width");
            Equal(800, migrated.SingleWindowSlots[1].Height,
                "first device profile must preserve existing slot height");
        }

        private static void PersistsDeviceRunSettingsProfiles()
        {
            var settings = AppSettings.CreateDefault();
            var phoneA = settings.GetOrCreateDeviceRunSettings("phone-a");
            var phoneB = settings.GetOrCreateDeviceRunSettings("phone-b");
            phoneA.VirtualDisplay.Width = 1920;
            phoneB.VirtualDisplay.Width = 1280;
            phoneA.SingleWindowSlots[2].StartAppPackage = "app.a";
            phoneB.SingleWindowSlots[2].StartAppPackage = "app.b";

            var serializer = new DataContractJsonSerializer(
                typeof(AppSettings));
            AppSettings loaded;
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                stream.Position = 0;
                loaded = (AppSettings)serializer.ReadObject(stream);
            }
            loaded.EnsureDefaults();

            var loadedA = loaded.GetOrCreateDeviceRunSettings("phone-a");
            var loadedB = loaded.GetOrCreateDeviceRunSettings("phone-b");
            Equal(1920, loadedA.VirtualDisplay.Width,
                "phone-a profile must survive settings serialization");
            Equal(1280, loadedB.VirtualDisplay.Width,
                "phone-b profile must survive settings serialization");
            Equal("app.a", loadedA.SingleWindowSlots[2].StartAppPackage,
                "phone-a app selection must survive serialization");
            Equal("app.b", loadedB.SingleWindowSlots[2].StartAppPackage,
                "phone-b app selection must survive serialization");
        }

        private static void KeepsWirelessSettingsIndependentPerPhysicalDevice()
        {
            var settings = AppSettings.CreateDefault();
            var phoneA = settings.GetOrCreateDeviceWirelessConnection(
                "phone-a",
                false);
            var phoneB = settings.GetOrCreateDeviceWirelessConnection(
                "phone-b",
                false);
            phoneA.Mode = AdbConnectionMode.Wireless;
            phoneA.WirelessHost = "192.168.50.82";
            phoneA.WirelessPort = 5555;
            phoneA.AutoReconnect = true;

            Equal(AdbConnectionMode.Usb, phoneB.Mode,
                "phone-b mode must not follow phone-a");
            True(string.IsNullOrEmpty(phoneB.WirelessHost),
                "phone-b address must not follow phone-a");
            Equal(5555, phoneB.WirelessPort,
                "phone-b must retain the default wireless port");
        }

        private static void SeedsSelectedWirelessDeviceFromLegacyConnection()
        {
            var settings = AppSettings.CreateDefault();
            settings.Connection.Mode = AdbConnectionMode.Wireless;
            settings.Connection.WirelessHost = "192.168.50.83";
            settings.Connection.WirelessPort = 5566;
            settings.Connection.AutoReconnect = false;

            var migrated = settings.GetOrCreateDeviceWirelessConnection(
                "existing-phone",
                true);
            var other = settings.GetOrCreateDeviceWirelessConnection(
                "other-phone",
                false);

            Equal(AdbConnectionMode.Wireless, migrated.Mode,
                "selected legacy device must preserve wireless mode");
            Equal("192.168.50.83", migrated.WirelessHost,
                "selected legacy device must preserve its address");
            Equal(5566, migrated.WirelessPort,
                "selected legacy device must preserve its port");
            True(!migrated.AutoReconnect,
                "selected legacy device must preserve reconnect setting");
            True(string.IsNullOrEmpty(other.WirelessHost),
                "another physical device must not inherit legacy address");
        }

        private static void PersistsDeviceWirelessConnectionProfiles()
        {
            var settings = AppSettings.CreateDefault();
            var phoneA = settings.GetOrCreateDeviceWirelessConnection(
                "phone-a",
                false);
            var phoneB = settings.GetOrCreateDeviceWirelessConnection(
                "phone-b",
                false);
            phoneA.Mode = AdbConnectionMode.Wireless;
            phoneA.WirelessHost = "10.0.0.2";
            phoneA.WirelessPort = 5555;
            phoneB.Mode = AdbConnectionMode.Wireless;
            phoneB.WirelessHost = "10.0.0.3";
            phoneB.WirelessPort = 5566;

            var serializer = new DataContractJsonSerializer(
                typeof(AppSettings));
            AppSettings loaded;
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                stream.Position = 0;
                loaded = (AppSettings)serializer.ReadObject(stream);
            }
            loaded.EnsureDefaults();

            var loadedA = loaded.FindDeviceWirelessConnection("phone-a");
            var loadedB = loaded.FindDeviceWirelessConnection("phone-b");
            NotNull(loadedA,
                "phone-a wireless profile must survive serialization");
            NotNull(loadedB,
                "phone-b wireless profile must survive serialization");
            Equal("10.0.0.2", loadedA.WirelessHost,
                "phone-a address must survive serialization");
            Equal(5566, loadedB.WirelessPort,
                "phone-b port must survive serialization");
        }

        private static void ResolvesDiagnosticTargetsByActivePhysicalDevice()
        {
            var device = new PhysicalDeviceInfo
            {
                Identity = "phone-a",
                DisplayName = "Galaxy A"
            };
            device.Transports.Add(new DeviceTransportInfo
            {
                Serial = "USB-A",
                Kind = DeviceTransportKind.Usb,
                Status = AdbDeviceStatus.Device
            });
            device.Transports.Add(new DeviceTransportInfo
            {
                Serial = "192.0.2.10:6000",
                Kind = DeviceTransportKind.Wireless,
                Status = AdbDeviceStatus.Device
            });
            var runtime = new DeviceRuntimeSessionSnapshot
            {
                Identity = "phone-a",
                IsConnected = true,
                ActiveTransportSerial = "192.0.2.10:6000"
            };

            Equal(
                "192.0.2.10:6000",
                DiagnosticDeviceTargetResolver.Resolve(
                    device, runtime, false, string.Empty),
                "nonselected diagnostics must use that phone's active transport");
            Equal(
                "USB-A",
                DiagnosticDeviceTargetResolver.Resolve(
                    device, runtime, true, "USB-A"),
                "selected diagnostics must use the selected phone's chosen serial");
            runtime.ActiveTransportSerial = "USB-MISSING";
            Equal(
                string.Empty,
                DiagnosticDeviceTargetResolver.Resolve(
                    device, runtime, false, string.Empty),
                "diagnostics must not fall back to another transport serial");
            Equal(
                string.Empty,
                DiagnosticDeviceTargetResolver.Resolve(
                    device, null, false, string.Empty),
                "diagnostics must not guess a target without a runtime session");
        }

        private static void ParsesAndroidMdnsConnectAndPairingServices()
        {
            var services = AdbMdnsOutputParser.Parse(
                "List of discovered mdns services\n" +
                "adb-TESTPHONEA-bAViOE _adb-tls-connect._tcp " +
                "192.0.2.83:38041\n" +
                "adb-TESTPHONEA-bAViOE _adb-tls-pairing._tcp. " +
                "192.0.2.83:37124\n" +
                "adb-phone-v6 _adb-tls-connect._tcp [fe80::20]:40000\n" +
                "adb-invalid-zero _adb-tls-connect._tcp 0:44065\n" +
                "adb-invalid-any _adb-tls-connect._tcp 0.0.0.0:44066\n" +
                "adb-invalid-host _adb-tls-connect._tcp bad'host:44067\n" +
                "malformed row\n");

            Equal(3, services.Count,
                "the parser must keep valid connect and pairing services only");
            Equal("adb-TESTPHONEA-bAViOE", services[0].InstanceName,
                "the mDNS instance name must be retained");
            Equal("_adb-tls-connect._tcp", services[0].ServiceType,
                "the connect endpoint must remain distinct from pairing");
            Equal("192.0.2.83", services[0].Host,
                "the IPv4 service host must be parsed");
            Equal(38041, services[0].Port,
                "the dynamic connect port must be parsed");
            Equal("_adb-tls-pairing._tcp", services[1].ServiceType,
                "a trailing service dot must be normalized");
            Equal("fe80::20", services[2].Host,
                "bracketed IPv6 hosts must be parsed without brackets");
            Equal(40000, services[2].Port,
                "the IPv6 dynamic port must be parsed");
        }

        private static void ResolvesMdnsEndpointOnlyForTheMatchingPhone()
        {
            var services = new[]
            {
                new AdbMdnsServiceRecord(
                    "adb-TESTPHONEA-bAViOE",
                    "_adb-tls-connect._tcp",
                    "192.0.2.83",
                    38041),
                new AdbMdnsServiceRecord(
                    "adb-TESTPHONEA-bAViOE",
                    "_adb-tls-pairing._tcp",
                    "192.0.2.83",
                    37124),
                new AdbMdnsServiceRecord(
                    "adb-OTHER-phone",
                    "_adb-tls-connect._tcp",
                    "192.0.2.84",
                    39000)
            };

            var matching = WirelessMdnsResolver.ResolveEndpointForIdentity(
                "serial:TESTPHONEA",
                "192.0.2.83:5555",
                services);
            Equal("192.0.2.83:38041",
                WirelessMdnsResolver.BuildEndpoint(matching),
                "the matching connect service must supply its dynamic port");
            Equal(null,
                WirelessMdnsResolver.ResolveEndpointForIdentity(
                    "serial:UNKNOWN",
                    "192.0.2.83:5555",
                    services),
                "another phone's advertised endpoint must never be selected");
            var ambiguousServices = new[]
            {
                services[0],
                new AdbMdnsServiceRecord(
                    "adb-TESTPHONEA-second",
                    "_adb-tls-connect._tcp",
                    "192.0.2.84",
                    38042)
            };
            Equal(null,
                WirelessMdnsResolver.ResolveEndpointForIdentity(
                    "serial:TESTPHONEA",
                    "192.0.2.99:5555",
                    ambiguousServices),
                "ambiguous mDNS hosts must not be resolved by enumeration order");
            var sameHostAmbiguousServices = new[]
            {
                services[0],
                new AdbMdnsServiceRecord(
                    "adb-TESTPHONEA-second",
                    "_adb-tls-connect._tcp",
                    "192.0.2.83",
                    38042)
            };
            Equal(null,
                WirelessMdnsResolver.ResolveEndpointForIdentity(
                    "serial:TESTPHONEA",
                    "192.0.2.83:5555",
                    sameHostAmbiguousServices),
                "multiple connect ports on a saved host must remain ambiguous");
            Equal("192.0.2.83:38041",
                WirelessMdnsResolver.BuildEndpoint(
                    WirelessMdnsResolver.ResolveEndpointForPairingHost(
                        "192.0.2.83", services)),
                "pairing endpoint resolution must ignore the pairing service port");
            Equal(null,
                WirelessMdnsResolver.ResolveConnectedService(
                    "adb-OTHER-phone._adb-tls-pairing._tcp",
                    services),
                "pairing aliases must not be accepted as connected transports");

            True((bool)InvokeDxManagerStatic(
                    "DexManager.Services.AdbService",
                    "IsWirelessSerial",
                    "adb-TESTPHONEA-bAViOE._adb-tls-connect._tcp"),
                "mDNS connect aliases must be classified as wireless");
            True((bool)InvokeDxManagerStatic(
                    "DexManager.Services.AdbService",
                    "IsWirelessSerial",
                    "192.0.2.10:5555"),
                "traditional TCP/IP endpoints must remain wireless");
            True(!(bool)InvokeDxManagerStatic(
                    "DexManager.Services.AdbService",
                    "IsWirelessSerial",
                    "adb-TESTPHONEA-bAViOE._adb-tls-pairing._tcp"),
                "mDNS pairing aliases must not be classified as connected Wi-Fi transports");
        }

        private static void DiagnosticReportsIncludeAllPhonesWithoutPrivateIdentifiers()
        {
            var configuration = new DirectoryInfo(
                AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar)).Name;
            var assemblyPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "DexManager", "bin",
                configuration, "DXManager.exe"));
            var assembly = Assembly.LoadFrom(assemblyPath);
            var snapshot = CreateDxManagerObject(
                assembly, "DexManager.Models.DeviceRegistrySnapshot");
            var devices = (IList)snapshot.GetType()
                .GetProperty("Devices").GetValue(snapshot, null);
            var deviceA = CreateDiagnosticDevice(
                assembly,
                "serial:PRIVATE-PHONE-A",
                "Secret display name A",
                "SERIAL-A-PRIVATE",
                "Usb");
            var deviceB = CreateDiagnosticDevice(
                assembly,
                "serial:PRIVATE-PHONE-B",
                "Secret display name B",
                "192.0.2.83:5555",
                "Wireless");
            devices.Add(deviceA);
            devices.Add(deviceB);

            var diagnosticA = CreateDiagnosticVersion(
                assembly,
                "serial:PRIVATE-PHONE-A",
                "SERIAL-A-PRIVATE",
                "SM-S921B",
                "Android 16");
            var diagnosticB = CreateDiagnosticVersion(
                assembly,
                "serial:PRIVATE-PHONE-B",
                "192.0.2.83:5555",
                "SM-S926B",
                "Android 15");
            var diagnostics = Array.CreateInstance(
                assembly.GetType(
                    "DexManager.Models.DeviceVersionDiagnostic", true), 2);
            diagnostics.SetValue(diagnosticA, 0);
            diagnostics.SetValue(diagnosticB, 1);

            var reportType = assembly.GetType(
                "DexManager.Services.DiagnosticReportService", true);
            var reportService = Activator.CreateInstance(reportType, true);
            var createReport = reportType.GetMethod(
                "CreateReport",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            var report = (string)createReport.Invoke(reportService, new[]
            {
                (object)"2.0.1",
                "C:\\private\\adb.exe",
                "Android Debug Bridge version 1.0.41",
                "C:\\private\\scrcpy.exe",
                "serial:PRIVATE-PHONE-A",
                snapshot,
                null,
                diagnosticA,
                null,
                new[]
                {
                    "[WARN] wireless=[fe80::20]:40000 " +
                    "host=phone.local alias=adb-TESTPHONEA._adb-tls-connect._tcp " +
                    "peer=192.0.2.83:5555 scoped=[fe80::abc%12]:40000 " +
                    "raw=2001:db8::42 owner=serial:PRIVATE-PHONE-B name=Secret display name B"
                },
                diagnostics
            });

            True(report.Contains("[Connected device versions]"),
                "the report must include the connected-device version section");
            True(report.Contains("Android 16"),
                "the report must include the selected phone version");
            True(report.Contains("Android 15"),
                "the report must include the other connected phone version");
            True(!report.Contains("Secret display name"),
                "device display names must remain masked in the report");
            True(!report.Contains("PRIVATE-PHONE-A") &&
                 !report.Contains("PRIVATE-PHONE-B"),
                "stable device identities must remain masked in the report");
            True(!report.Contains("SERIAL-A-PRIVATE") &&
                 !report.Contains("192.0.2.83"),
                "all connected transport serials and IP addresses must be masked");
            True(!report.Contains("fe80::20") &&
                 !report.Contains("fe80::abc") && !report.Contains("2001:db8::42") &&
                 !report.Contains("phone.local") &&
                 !report.Contains("_adb-tls-connect._tcp"),
                "IPv6 endpoints and mDNS host/aliases must be masked in logs");
        }

        private static void DiscardsAppListsLoadedForAnotherPhone()
        {
            var samePhoneAfterTransportSwitch = (bool)InvokeDxManagerStatic(
                "DexManager.Forms.MainForm",
                "AppListResultMatchesSelection",
                "serial:phone-a",
                "USB-A",
                "serial:phone-a",
                "192.0.2.10:5555");
            var differentPhone = (bool)InvokeDxManagerStatic(
                "DexManager.Forms.MainForm",
                "AppListResultMatchesSelection",
                "serial:phone-a",
                "USB-A",
                "serial:phone-b",
                "USB-A");
            var temporaryIdentitySameTransport = (bool)InvokeDxManagerStatic(
                "DexManager.Forms.MainForm",
                "AppListResultMatchesSelection",
                string.Empty,
                "USB-A",
                string.Empty,
                "USB-A");

            True(samePhoneAfterTransportSwitch,
                "the same phone remains a valid app-list target after transport switching");
            True(!differentPhone,
                "a late app-list response must not replace another phone's list");
            True(temporaryIdentitySameTransport,
                "without stable identity, only the exact captured serial can validate a result");
        }

        private static void KeepsPhoneAppCataloguesIndependent()
        {
            var catalog = new DeviceAppCatalog();
            var appA = new ScrcpyAppInfo { Name = "A app", PackageName = "a.app" };
            catalog.Replace("phone-a", "USB-A", new[] { appA });
            catalog.Replace("phone-b", "USB-B", new[] {
                new ScrcpyAppInfo { Name = "B app", PackageName = "b.app" } });
            // A late A response must update A without altering the selected B projection.
            catalog.Replace("phone-a", "USB-A", new[] {
                new ScrcpyAppInfo { Name = "A refreshed", PackageName = "a.new" }, null });
            var b = catalog.CreateItems("phone-b", "USB-B", null, "", "", "None");
            True(b.Count == 2 && b[1].PackageName == "b.app", "B retains its own catalogue after a late A response");
            var a = catalog.CreateItems("phone-a", "192.0.2.1:1234", null, "a.saved", "Saved A", "None");
            True(a.Count == 3 && a[1].PackageName == "a.new" && a[2].PackageName == "a.saved",
                "transport switching restores A and retains only A's saved selection");
            a[1].Name = "UI mutation";
            True(catalog.CreateItems("phone-a", "USB-A", null, "", "", "None")[1].Name == "A refreshed",
                "UI projections cannot mutate cached names");
            var unqueried = catalog.CreateItems("phone-c", "USB-C", null, "c.saved", "Saved C", "None");
            True(unqueried.Count == 2 && unqueried[1].PackageName == "c.saved", "an unqueried phone has no other phone's apps");
            catalog.Replace("phone-a", "USB-A", new ScrcpyAppInfo[0]);
            True(catalog.CreateItems("phone-a", "USB-A", new[] {
                new RememberedAppSettings { Name = "removed", PackageName = "a.removed" } }, "", "", "None").Count == 1,
                "a successfully empty catalogue cannot resurrect uninstalled recent apps");
        }

        private static void RejectsStaleWirelessTransportOwnership()
        {
            var phone = new PhysicalDeviceInfo { Identity = "serial:A" };
            phone.Transports.Add(new DeviceTransportInfo { Serial = "192.0.2.1:40000", Status = AdbDeviceStatus.Device });
            var snapshot = new DeviceRegistrySnapshot();
            snapshot.Devices.Add(phone);
            var queries = 0;
            var disconnected = WirelessMdnsResolver.FindVerifiedConnectedDevice("serial:A", snapshot,
                new List<AdbDeviceInfo>(), serial => { queries++; return "serial:A"; });
            True(disconnected == null && queries == 0, "a stale registry cannot make an absent transport connected");
            var live = new List<AdbDeviceInfo> {
                new AdbDeviceInfo { Serial = "192.0.2.1:40000", Status = AdbDeviceStatus.Device },
                new AdbDeviceInfo { Serial = "192.0.2.2:41000", Status = AdbDeviceStatus.Device }
            };
            var owner = WirelessMdnsResolver.FindVerifiedConnectedDevice("serial:A", snapshot, live,
                serial => serial == "192.0.2.1:40000" ? "serial:B" : "serial:A");
            True(owner != null && owner.Serial == "192.0.2.2:41000", "an IP reassigned to B cannot update A's profile");
        }

        private static void KeepsChangedWirelessPolicyDuringDiscovery()
        {
            var profile = new DeviceWirelessConnectionProfile {
                Mode = AdbConnectionMode.Wireless, WirelessHost = "192.0.2.1", WirelessPort = 40000 };
            True(WirelessMdnsResolver.CanRefreshProfile(profile, "192.0.2.1", 40000), "unchanged wireless profiles can refresh");
            profile.Mode = AdbConnectionMode.Usb;
            True(!WirelessMdnsResolver.CanRefreshProfile(profile, "192.0.2.1", 40000), "mDNS must not undo a user's switch to USB");
            profile.Mode = AdbConnectionMode.Wireless;
            profile.WirelessPort = 42000;
            True(!WirelessMdnsResolver.CanRefreshProfile(profile, "192.0.2.1", 40000), "mDNS must not overwrite an edited endpoint");
            var selectedConnection = new ConnectionSettings {
                Mode = AdbConnectionMode.Wireless, WirelessHost = "192.0.2.2", WirelessPort = 43000 };
            True(!WirelessMdnsResolver.CanRefreshConnection(selectedConnection, "192.0.2.1", 40000),
                "refreshing A must not overwrite a current connection switched to B");
        }

        private static void PersistsPhoneRecentAppsIndependently()
        {
            var settings = AppSettings.CreateDefault();
            settings.RememberedApps.Add(new RememberedAppSettings { Name = "Legacy", PackageName = "legacy.app" });
            var a = settings.GetOrCreateDeviceRunSettings("phone-a");
            var b = settings.GetOrCreateDeviceRunSettings("phone-b");
            settings.SchemaVersion = 26;
            a.RememberedApps = null;
            b.RememberedApps = null;
            a.VirtualDisplay.Width = 1800;
            settings.EnsureDefaults();
            True(settings.SchemaVersion == AppSettings.CurrentSchemaVersion && a.VirtualDisplay.Width == 1800,
                "schema 26 upgrades phone recents without resetting field-configured settings");
            True(a.RememberedApps.Count == 0 && b.RememberedApps.Count == 0,
                "ambiguous legacy global recents are not assigned to every phone");
            a.RememberedApps.Add(new RememberedAppSettings { Name = "A", PackageName = "a.app" });
            var folder = Path.Combine(Path.GetTempPath(), "dxm-recent-apps-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new SettingsService(new LogService(), folder);
                service.Save(settings);
                var restored = service.Load();
                True(restored.GetOrCreateDeviceRunSettings("phone-a").RememberedApps.Count == 1 &&
                    restored.GetOrCreateDeviceRunSettings("phone-b").RememberedApps.Count == 0,
                    "phone-specific recent apps survive settings save and reload without crossing devices");
                True(restored.RememberedApps.Count == 1, "legacy recent data is preserved in storage");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        private static void SerializesWirelessPolicyEditsWithDiscoverySave()
        {
            var root = Path.Combine(Path.GetTempPath(), "dxm-wireless-policy-" + Guid.NewGuid().ToString("N"));
            var settings = AppSettings.CreateDefault();
            settings.GetOrCreateDeviceWirelessConnection("phone-a", false).Mode = AdbConnectionMode.Wireless;
            var service = new SettingsService(new LogService(), root);
            // Prepare the fixture directory before workers test policy serialization.
            Directory.CreateDirectory(Path.Combine(root, "config"));
            using (var saveEntered = new ManualResetEventSlim())
            using (var releaseSave = new ManualResetEventSlim())
            using (var editEntered = new ManualResetEventSlim())
            {
                Task save = null;
                Task edit = null;
                try
                {
                    save = Task.Run(() => service.UpdateAndSave(settings, candidate => {
                        saveEntered.Set();
                        if (!releaseSave.Wait(5000)) throw new TimeoutException("policy test save did not release");
                    }));
                    True(saveEntered.Wait(5000), "discovery must capture its settings snapshot");
                    edit = Task.Run(() => {
                        editEntered.Set();
                        service.UpdateInMemory(settings, candidate =>
                            candidate.GetOrCreateDeviceWirelessConnection("phone-a", false).Mode = AdbConnectionMode.Usb);
                    });
                    True(editEntered.Wait(5000), "the UI policy edit must enter its update");
                    True(!edit.Wait(100), "UI policy edits must wait for an in-flight discovery save");
                    releaseSave.Set();
                    Task.WaitAll(save, edit);
                    Equal(AdbConnectionMode.Usb, settings.FindDeviceWirelessConnection("phone-a").Mode,
                        "the later USB edit must survive copying discovery's saved candidate to live settings");
                }
                finally
                {
                    releaseSave.Set();
                    if (save != null) save.Wait(5000);
                    if (edit != null) edit.Wait(5000);
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
        }

        private static object CreateDiagnosticDevice(
            Assembly assembly,
            string identity,
            string displayName,
            string serial,
            string transportKind)
        {
            var device = CreateDxManagerObject(
                assembly, "DexManager.Models.PhysicalDeviceInfo");
            SetDxManagerProperty(device, "Identity", identity);
            SetDxManagerProperty(device, "DisplayName", displayName);
            var transports = (IList)device.GetType()
                .GetProperty("Transports").GetValue(device, null);
            var transport = CreateDxManagerObject(
                assembly, "DexManager.Models.DeviceTransportInfo");
            SetDxManagerProperty(transport, "Serial", serial);
            SetDxManagerProperty(transport, "Kind", Enum.Parse(
                assembly.GetType("DexManager.Models.DeviceTransportKind"),
                transportKind));
            SetDxManagerProperty(transport, "Status", Enum.Parse(
                assembly.GetType("DexManager.Models.AdbDeviceStatus"),
                "Device"));
            transports.Add(transport);
            return device;
        }

        private static object CreateDiagnosticVersion(
            Assembly assembly,
            string identity,
            string serial,
            string model,
            string androidVersion)
        {
            var diagnostic = CreateDxManagerObject(
                assembly, "DexManager.Models.DeviceVersionDiagnostic");
            SetDxManagerProperty(diagnostic, "DeviceIdentity", identity);
            SetDxManagerProperty(diagnostic, "Serial", serial);
            SetDxManagerProperty(diagnostic, "DisplayName", "Private phone");
            SetDxManagerProperty(diagnostic, "Model", model);
            SetDxManagerProperty(diagnostic, "TransportKind", Enum.Parse(
                assembly.GetType("DexManager.Models.DeviceTransportKind"),
                serial.Contains(":") ? "Wireless" : "Usb"));
            SetDxManagerProperty(diagnostic, "AndroidVersion", androidVersion);
            SetDxManagerProperty(diagnostic, "AndroidSdk", 36);
            SetDxManagerProperty(diagnostic, "OneUiVersion", "8.0");
            SetDxManagerProperty(diagnostic, "SecurityPatch", "2026-08-01");
            SetDxManagerProperty(diagnostic, "QuerySucceeded", true);
            SetDxManagerProperty(diagnostic, "Compatibility", Enum.Parse(
                assembly.GetType(
                    "DexManager.Models.DeviceCompatibilityAssessment"),
                "RecommendedBaseline"));
            return diagnostic;
        }

        private static object CreateDxManagerObject(
            Assembly assembly,
            string typeName)
        {
            return Activator.CreateInstance(
                assembly.GetType(typeName, true), true);
        }

        private static void SetDxManagerProperty(
            object instance,
            string name,
            object value)
        {
            instance.GetType().GetProperty(name).SetValue(
                instance, value, null);
        }

        private static void RoundTripsCompanionGuardianProtocolPayload()
        {
            const string expected = "0|한글·Unicode/150";
            using (var stream = new MemoryStream())
            {
                CompanionGuardianProtocol.WriteInt32(
                    stream, CompanionGuardianProtocol.Magic);
                CompanionGuardianProtocol.WriteString(stream, expected);
                stream.Position = 0;

                Equal(
                    CompanionGuardianProtocol.Magic,
                    CompanionGuardianProtocol.ReadInt32(stream),
                    "guardian protocol must preserve the big-endian magic");
                Equal(
                    expected,
                    CompanionGuardianProtocol.ReadString(stream),
                    "guardian protocol must preserve Unicode payloads");
            }
        }

        private static void BlocksNewProcessesAfterShutdown()
        {
            var runner = new ProcessRunner(new LogService());
            runner.BeginShutdown();

            var result = runner.Run(
                GetCommandProcessorPath(),
                "/d /c exit 0",
                null,
                5000,
                false);

            True(result.Canceled,
                "shutdown gate must cancel new child process launches");
            True(!result.IsSuccess,
                "a shutdown-canceled process must not report success");
        }

        private static void TerminatesActiveProcessOnShutdown()
        {
            var runner = new ProcessRunner(new LogService());
            var task = Task.Run(delegate
            {
                return runner.Run(
                    GetSystemExecutablePath("ping.exe"),
                    "127.0.0.1 -n 30",
                    null,
                    30000,
                    false);
            });

            Thread.Sleep(300);
            runner.BeginShutdown();

            True(task.Wait(5000),
                "active child process must end promptly during shutdown");
            True(task.Result.Canceled,
                "terminated child process must report shutdown cancellation");
        }

        private static void KeepsProcessOutputDrainBounded()
        {
            var runner = new ProcessRunner(new LogService());
            var stopwatch = Stopwatch.StartNew();
            var result = runner.Run(
                Assembly.GetExecutingAssembly().Location,
                "--spawn-output-holder",
                null,
                2000,
                false);
            stopwatch.Stop();

            True(result.IsSuccess,
                "the parent command must finish successfully");
            True(result.StandardOutput.StartsWith("holder-pid="),
                "the parent must report the inherited-pipe holder");
            True(stopwatch.ElapsedMilliseconds < 3500,
                "an inherited output pipe must not keep process completion unbounded");
        }

        private static void KeepsTransferOutputDrainBounded()
        {
            var pending = new TaskCompletionSource<string>();
            var stopwatch = Stopwatch.StartNew();
            var output = (string)InvokeDxManagerStatic(
                "DexManager.Services.FileTransferCoordinator",
                "GetTaskResult",
                pending.Task);
            stopwatch.Stop();

            Equal(string.Empty, output,
                "a pending transfer output reader must return partial output");
            True(stopwatch.ElapsedMilliseconds < 3500,
                "a pending transfer output reader must have a finite wait");
        }

        private static void TerminatesOnlyConfiguredBundledExecutablePath()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "DXManager.ProcessCleanup." + Guid.NewGuid().ToString("N"));
            var firstDirectory = Path.Combine(root, "owned");
            var secondDirectory = Path.Combine(root, "unrelated");
            Directory.CreateDirectory(firstDirectory);
            Directory.CreateDirectory(secondDirectory);

            var executableName = "dxmtest" +
                Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe";
            var firstPath = Path.Combine(firstDirectory, executableName);
            var secondPath = Path.Combine(secondDirectory, executableName);
            File.Copy(GetSystemExecutablePath("ping.exe"), firstPath);
            File.Copy(GetSystemExecutablePath("ping.exe"), secondPath);

            Process first = null;
            Process second = null;
            try
            {
                first = Process.Start(new ProcessStartInfo
                {
                    FileName = firstPath,
                    Arguments = "127.0.0.1 -n 30",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                second = Process.Start(new ProcessStartInfo
                {
                    FileName = secondPath,
                    Arguments = "127.0.0.1 -n 30",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                Thread.Sleep(200);
                var cleanup = new BundledProcessCleanupService(
                    new LogService());
                cleanup.AddExecutablePath(firstPath);
                Equal(1, cleanup.TerminateRemainingProcesses(),
                    "only the configured bundled executable must terminate");

                True(first.WaitForExit(3000),
                    "configured executable must exit during final cleanup");
                True(!second.HasExited,
                    "same-named executable in another path must remain alive");
            }
            finally
            {
                if (first != null)
                {
                    try { if (!first.HasExited) first.Kill(); }
                    catch { }
                    first.Dispose();
                }
                if (second != null)
                {
                    try
                    {
                        if (!second.HasExited)
                        {
                            second.Kill();
                            second.WaitForExit(3000);
                        }
                    }
                    catch { }
                    second.Dispose();
                }
                try { Directory.Delete(root, true); }
                catch { }
            }
        }

        private static void SerializesConcurrentSettingsSaves()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "DXManager.Settings." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                const int writerCount = 8;
                var gate = new ManualResetEventSlim(false);
                var tasks = new Task[writerCount];
                for (var index = 0; index < writerCount; index++)
                {
                    var writer = index;
                    tasks[index] = Task.Run(delegate
                    {
                        var service = new SettingsService(
                            new LogService(),
                            root);
                        var settings = AppSettings.CreateDefault();
                        settings.Connection.WirelessHost =
                            "192.168.0." + (writer + 1);
                        gate.Wait();
                        service.Save(settings);
                    });
                }

                gate.Set();
                True(Task.WaitAll(tasks, 10000),
                    "concurrent settings saves must finish promptly");

                var verifier = new SettingsService(new LogService(), root);
                var loaded = verifier.Load();
                NotNull(loaded,
                    "concurrent settings saves must leave valid settings");
                True(File.Exists(verifier.SettingsFilePath),
                    "concurrent settings saves must create settings.json");
                Equal(
                    0,
                    Directory.GetFiles(
                        Path.GetDirectoryName(verifier.SettingsFilePath),
                        "settings.json.*.tmp").Length,
                    "successful settings saves must remove unique temp files");
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch { }
            }
        }

        private static void PreservesSharedStorageRootTransferTargets()
        {
            var settings = AppSettings.CreateDefault();
            settings.Paths.FileTransferTargetFolder = "/sdcard/";
            settings.EnsureDefaults();
            Equal(
                "/sdcard/",
                settings.Paths.FileTransferTargetFolder,
                "settings normalization must preserve the shared-storage root");
            settings.Paths.FileTransferTargetFolder = "/storage/emulated/0/";
            settings.EnsureDefaults();
            Equal(
                "/storage/emulated/0/",
                settings.Paths.FileTransferTargetFolder,
                "settings normalization must preserve the emulated-storage root");

            Equal(
                "/sdcard",
                (string)InvokeDxManagerStatic(
                    "DexManager.Services.FileTransferCoordinator",
                    "NormalizeRemoteDirectory",
                    "/sdcard/"),
                "the shared-storage root must remain the selected target");
            Equal(
                "/storage/emulated/0",
                (string)InvokeDxManagerStatic(
                    "DexManager.Services.FileTransferCoordinator",
                    "NormalizeRemoteDirectory",
                    "/storage/emulated/0/"),
                "the emulated-storage root must remain the selected target");
        }

        private static void KeepsSanitizedSiblingNamesDistinct()
        {
            var destination = Path.Combine(
                Path.GetTempPath(),
                "DXManager.PhoneTransferCollision." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            try
            {
                var roots = new Dictionary<int, string>();
                var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
                var first = (string)InvokeDxManagerStatic(
                    "DexManager.Services.PhoneTransferReceiver",
                    "ResolveSafePath",
                    destination,
                    1,
                    "folder/report:final.txt",
                    roots,
                    reserved,
                    resolved);
                var second = (string)InvokeDxManagerStatic(
                    "DexManager.Services.PhoneTransferReceiver",
                    "ResolveSafePath",
                    destination,
                    1,
                    "folder/report?final.txt",
                    roots,
                    reserved,
                    resolved);
                var repeated = (string)InvokeDxManagerStatic(
                    "DexManager.Services.PhoneTransferReceiver",
                    "ResolveSafePath",
                    destination,
                    1,
                    "folder/report:final.txt",
                    roots,
                    reserved,
                    resolved);

                True(
                    !string.Equals(first, second, StringComparison.OrdinalIgnoreCase),
                    "sanitized sibling names must not overwrite each other");
                Equal(first, repeated,
                    "the same logical source path must keep its resolved destination");
                Equal("report_final.txt", Path.GetFileName(first),
                    "the first sanitized name must remain stable");
                Equal("report_final (1).txt", Path.GetFileName(second),
                    "the colliding sanitized name must receive a suffix");
            }
            finally
            {
                try { Directory.Delete(destination, true); }
                catch { }
            }
        }

        private static void UsesShortPartialNameForLongFinalFile()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "DXManager.PhoneTransferLongName");
            var finalPath = Path.Combine(
                directory,
                new string('a', 230) + ".txt");
            var partialPath = (string)InvokeDxManagerStatic(
                "DexManager.Services.PhoneTransferReceiver",
                "CreatePartialPath",
                finalPath,
                Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));

            Equal(
                Path.GetFullPath(directory),
                Path.GetDirectoryName(partialPath),
                "the partial file must stay beside the final file");
            Equal(
                ".dxpartial-00112233445566778899aabbccddeeff",
                Path.GetFileName(partialPath),
                "the partial file must use a short independent name");
            True(
                Path.GetFileName(partialPath).Length < Path.GetFileName(finalPath).Length,
                "the partial name must not extend the long final name");
        }

        private static void EncodesCompanionTransferHistoryCommand()
        {
            const string requestId = "id' ; reboot";
            const string displayName = "O'Brien & reports/한글.txt";
            const string status = "completed";
            const long timestamp = 1790000000123L;
            var command = (string)InvokeDxManagerStatic(
                "DexManager.Services.FileTransferCoordinator",
                "BuildCompanionHistoryCommand",
                requestId,
                displayName,
                status,
                4,
                true,
                timestamp);
            var idMatch = Regex.Match(command, @"--es id64 '([^']*)'");
            var nameMatch = Regex.Match(command, @"--es name64 '([^']*)'");

            True(idMatch.Success,
                "the Companion broadcast must contain an encoded request id");
            True(nameMatch.Success,
                "the Companion broadcast must contain an encoded display name");
            Equal(requestId,
                Encoding.UTF8.GetString(Convert.FromBase64String(
                    idMatch.Groups[1].Value)),
                "request ids must survive shell-safe base64 encoding");
            Equal(displayName,
                Encoding.UTF8.GetString(Convert.FromBase64String(
                    nameMatch.Groups[1].Value)),
                "Unicode and shell-sensitive names must survive encoding");
            True(!command.Contains(requestId) && !command.Contains(displayName),
                "untrusted transfer metadata must not appear raw in the shell command");
            True(command.Contains("--es status 'completed' --ei count 4"),
                "history status and count must be included");
            True(command.Contains("--ez folder true --el time 1790000000123"),
                "folder type and timestamp must be included");
            Throws<ArgumentOutOfRangeException>(delegate
            {
                InvokeDxManagerStatic(
                    "DexManager.Services.FileTransferCoordinator",
                    "BuildCompanionHistoryCommand",
                    requestId,
                    displayName,
                    "invalid' --ez enabled true",
                    4,
                    true,
                    timestamp);
            }, "history status must remain a fixed allowlisted value");
        }

        private static void RemembersOnlyNewestCustomResolutions()
        {
            var legacySettings = AppSettings.CreateDefault();
            legacySettings.SchemaVersion = 25;
            legacySettings.RecentCustomResolutions = null;
            legacySettings.EnsureDefaults();
            Equal(
                AppSettings.CurrentSchemaVersion,
                legacySettings.SchemaVersion,
                "legacy settings must upgrade without losing other values");
            Equal(
                0,
                legacySettings.RecentCustomResolutions.Count,
                "legacy settings must start with an empty recent-resolution list");

            var root = Path.Combine(
                Path.GetTempPath(),
                "DXManager.RecentResolutions." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var settings = AppSettings.CreateDefault();
                for (var index = 0; index < 6; index++)
                {
                    settings.RememberCustomResolution(
                        1000 + index * 100,
                        700 + index * 10);
                }
                settings.RememberCustomResolution(1200, 720);
                settings.RememberCustomResolution(1600, 900);
                settings.RememberCustomResolution(200, 200);
                settings.EnsureDefaults();

                Equal(
                    AppSettings.MaximumRecentCustomResolutions,
                    settings.RecentCustomResolutions.Count,
                    "only the five most recently used custom resolutions are retained");
                Equal(1200, settings.RecentCustomResolutions[0].Width,
                    "using a previous resolution moves it to the front");
                Equal(720, settings.RecentCustomResolutions[0].Height,
                    "the most recent resolution keeps its matching height");
                True(settings.RecentCustomResolutions.TrueForAll(
                        item => item.Width != 1600 || item.Height != 900),
                    "a standard preset must not enter recent custom resolutions");

                var service = new SettingsService(new LogService(), root);
                service.Save(settings);
                service.UpdateAndSave(
                    settings,
                    candidate => candidate.RememberCustomResolution(900, 600));
                Equal(900, settings.RecentCustomResolutions[0].Width,
                    "saved recent resolutions must be copied to live settings");
                var reloaded = service.Load();
                Equal(
                    settings.RecentCustomResolutions.Count,
                    reloaded.RecentCustomResolutions.Count,
                    "recent resolutions must survive settings reload");
                Equal(900, reloaded.RecentCustomResolutions[0].Width,
                    "the newest recent resolution must survive settings reload");
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch { }
            }
        }

        private static void AppliesSavedSingleWindowAppProfile()
        {
            var profile = new SingleWindowAppProfile
            {
                PackageName = "com.example.maps",
                AppName = "Maps",
                Width = 1234,
                Height = 987,
                CustomWidth = 1234,
                CustomHeight = 987,
                Dpi = 240,
                BitRate = "12M",
                MaxFps = 30,
                TurnScreenOff = false,
                StayAwake = false,
                UseHidKeyboard = false,
                UseHidMouse = false,
                ForceStopStartApp = true,
                AdditionalArguments = "--no-audio",
                FlexDisplay = true
            };
            var slot = AppSettings.CreateDefault().SingleWindowSlots[0];

            AppSettings.ApplySingleWindowAppProfile(
                profile,
                slot,
                "com.example.maps",
                "Google Maps");

            True(AppSettings.SingleWindowAppProfileMatchesSlot(
                profile,
                slot,
                "com.example.maps",
                "Google Maps"),
                "the saved profile must be fully applied to the selected slot");
            Equal("Google Maps", slot.StartAppName,
                "the current app display name must remain selected");
            slot.Width = 1600;
            True(!AppSettings.SingleWindowAppProfileMatchesSlot(
                profile,
                slot,
                "com.example.maps",
                "Google Maps"),
                "a changed slot value must trigger profile reapplication");
        }

        private static object InvokeDxManagerStatic(
            string typeName,
            string methodName,
            params object[] arguments)
        {
            var configuration = new DirectoryInfo(
                AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar)).Name;
            var assemblyPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "DexManager", "bin",
                configuration, "DXManager.exe"));
            var assembly = Assembly.LoadFrom(assemblyPath);
            var type = assembly.GetType(typeName, true);
            var method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(typeName, methodName);
            }

            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static string GetCommandProcessorPath()
        {
            return GetSystemExecutablePath("cmd.exe");
        }

        private static string GetSystemExecutablePath(string fileName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                fileName);
        }

        private static DiscoveredDeviceTransport Device(
            string identity,
            string name,
            string serial,
            DeviceTransportKind kind,
            AdbDeviceStatus status = AdbDeviceStatus.Device)
        {
            return new DiscoveredDeviceTransport
            {
                DeviceIdentity = identity,
                DisplayName = name,
                Serial = serial,
                Kind = kind,
                Status = status,
                RawStatus = status.ToString().ToLowerInvariant()
            };
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + "; expected=" + expected + ", actual=" + actual);
            }
        }

        private static void NotNull(object value, string message)
        {
            if (value == null) throw new InvalidOperationException(message);
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Throws<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
