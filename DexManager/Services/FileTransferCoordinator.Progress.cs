using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DexManager.FileTransfer;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed partial class FileTransferCoordinator : IDisposable
    {
        private const string CompanionHistoryComponent =
            "io.github.mazemei.dxdisplaycleanup/io.github.mazemei.dxdisplaycleanup.TransferHistoryReceiver";
        private const string CompanionHistoryAction =
            "io.github.mazemei.dxdisplaycleanup.RECORD_TRANSFER_HISTORY";

        private void CompleteSuccess(TransferWorkItem item)
        {
            MarkTerminalAndUnregister(item);
            Publish(item, FileTransferStage.Completed, -1, string.Empty);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = true,
                ExitCode = 0,
                FinalFileName = item.FinalFileName,
                Message = string.Empty
            });
            _logService.Info(DeviceLogFormatter.ForSerial(
                item.Session.Serial,
                LocalizationService.Format(
                    "Log.FileTransfer.Completed",
                    item.FileName,
                    item.FinalFileName)));
            NotifyCompanionHistory(item, "completed");
        }

        private void CompleteFailed(TransferWorkItem item, string message)
        {
            if (item == null || item.IsTerminal) return;
            item.CompletedEntryCount = 0;
            item.CurrentEntryIndex = item.Entries.Count;
            lock (_syncRoot) item.Session.FailedCount++;
            MarkTerminalAndUnregister(item);
            Publish(item, FileTransferStage.Failed, -1, message);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = false,
                ExitCode = 1,
                Message = message ?? string.Empty
            });
            _logService.Warning(DeviceLogFormatter.ForSerial(
                item.Session.Serial,
                LocalizationService.Format(
                    "Log.FileTransfer.Failed",
                    item.FileName,
                    message)));
            NotifyCompanionHistory(item, "failed");
        }

        private void CompleteCanceled(TransferWorkItem item)
        {
            if (item == null || item.IsTerminal) return;
            item.CompletedEntryCount = 0;
            item.CurrentEntryIndex = item.Entries.Count;
            MarkTerminalAndUnregister(item);
            var message = LocalizationService.Get(
                "FileTransfer.CanceledByUser");
            Publish(item, FileTransferStage.Canceled, -1, message);
            SendResponse(item, new FileTransferResponseMessage
            {
                Version = FileTransferEnvironment.ProtocolVersion,
                Success = false,
                Canceled = true,
                ExitCode = 1,
                Message = message
            });
            _logService.Info(DeviceLogFormatter.ForSerial(
                item.Session.Serial,
                LocalizationService.Format(
                    "Log.FileTransfer.Canceled",
                    item.FileName)));
            NotifyCompanionHistory(item, "canceled");
        }

        private void NotifyCompanionHistory(
            TransferWorkItem item,
            string status)
        {
            if (item == null || item.Session == null ||
                string.IsNullOrWhiteSpace(item.Session.Serial))
            {
                return;
            }
            try
            {
                var id = string.IsNullOrWhiteSpace(item.Request.RequestId)
                    ? Guid.NewGuid().ToString("N")
                    : item.Request.RequestId;
                var name = string.IsNullOrWhiteSpace(item.FinalFileName)
                    ? item.FileName
                    : item.FinalFileName;
                var count = item.DirectoryTransfer
                    ? Math.Max(1, item.Entries.Count(entry =>
                        !entry.IsDirectory))
                    : Math.Max(1, item.Entries.Count);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var script = BuildCompanionHistoryCommand(
                    id,
                    name ?? string.Empty,
                    status,
                    count,
                    item.DirectoryTransfer,
                    timestamp);

                // This helper uses the requested serial and the existing
                // shutdown gate. History is optional and must not alter the
                // transfer result.
                RunCleanupScript(item.Session.Serial, script);
            }
            catch
            {
                // Transfer completion must not fail if Companion history is
                // unavailable.
            }
        }

        private static string BuildCompanionHistoryCommand(
            string id,
            string name,
            string status,
            int count,
            bool folder,
            long timestamp)
        {
            if (!string.Equals(status, "completed", StringComparison.Ordinal) &&
                !string.Equals(status, "failed", StringComparison.Ordinal) &&
                !string.Equals(status, "canceled", StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException("status");
            }

            return new StringBuilder()
                    .Append("am broadcast -n ")
                    .Append(CompanionHistoryComponent)
                    .Append(" -a ")
                    .Append(CompanionHistoryAction)
                    .Append(" --es id64 '")
                    .Append(ToBase64(id))
                    .Append("' --es name64 '")
                    .Append(ToBase64(name))
                    .Append("' --es status '")
                    .Append(status)
                    .Append("' --ei count ")
                    .Append(Math.Max(1, count).ToString(
                        CultureInfo.InvariantCulture))
                    .Append(" --ez folder ")
                    .Append(folder ? "true" : "false")
                    .Append(" --el time ")
                    .Append(timestamp.ToString(
                        CultureInfo.InvariantCulture))
                    .ToString();
        }

        private void MarkTerminalAndUnregister(TransferWorkItem item)
        {
            var serial = item.Session.Serial;
            lock (_syncRoot)
            {
                item.MarkTerminal();
                _requests.Remove(item.Request.RequestId);
            }
            PublishTransferState(serial);
        }

        private void SendResponse(
            TransferWorkItem item,
            FileTransferResponseMessage response)
        {
            try { FileTransferWire.Write(item.Pipe, response); }
            catch { }
            finally
            {
                DisposeClientPipe(item.Pipe);
            }
        }

        private static void SendImmediateFailure(
            NamedPipeServerStream pipe,
            string message,
            bool canceled)
        {
            if (pipe == null) return;
            try
            {
                FileTransferWire.Write(pipe, new FileTransferResponseMessage
                {
                    Version = FileTransferEnvironment.ProtocolVersion,
                    Success = false,
                    Canceled = canceled,
                    ExitCode = 1,
                    Message = message ?? string.Empty
                });
            }
            catch { }
        }

        private void Publish(
            TransferWorkItem item,
            FileTransferStage stage,
            int percent,
            string message)
        {
            TransferWorkItem primary;
            int completed;
            int failed;
            int queued;
            List<FileTransferQueueEntry> visibleQueue;
            long sequence;
            lock (_syncRoot)
            {
                sequence = ++_progressSequence;
                item.CurrentStage = stage;
                item.CurrentPercent = percent;
                item.CurrentMessage = message ?? string.Empty;
                primary = _activeItem != null &&
                    !_activeItem.IsTerminal
                    ? _activeItem
                    : item;
                completed = primary.Session.CompletedCount +
                    primary.CompletedEntryCount;
                failed = primary.Session.FailedCount;
                visibleQueue = BuildVisibleQueue(primary);
                queued = CountQueuedItems(primary);
            }
            var progress = new FileTransferProgress(
                sequence,
                primary.Request.RequestId,
                primary.Request.SessionId,
                primary.CurrentStage,
                GetDisplayName(primary),
                primary.FinalFileName,
                primary.FileSize,
                primary.CurrentPercent,
                completed,
                failed,
                queued,
                visibleQueue,
                primary.StartedUtc,
                primary.DirectoryTransfer,
                Math.Max(
                    1,
                    primary.Entries.Count(entry => !entry.IsDirectory)),
                primary.CurrentMessage);
            var handler = ProgressChanged;
            if (handler == null) return;
            try
            {
                handler(this, new FileTransferProgressEventArgs(progress));
            }
            catch (Exception ex)
            {
                _logService.Warning(LocalizationService.Format(
                    "Log.FileTransfer.ProgressSubscriberFailed",
                    ex.Message));
            }
        }

        private List<FileTransferQueueEntry> BuildVisibleQueue(
            TransferWorkItem primary)
        {
            var result = new List<FileTransferQueueEntry>(
                MaximumVisibleQueueItems);
            if (primary.Entries != null && primary.Entries.Count > 0 &&
                primary.CurrentEntryIndex < primary.Entries.Count)
            {
                for (var index = Math.Max(primary.CurrentEntryIndex, 0);
                    index < primary.Entries.Count &&
                    result.Count < MaximumVisibleQueueItems;
                    index++)
                {
                    var entry = primary.Entries[index];
                    if (entry.IsDirectory) continue;
                    result.Add(new FileTransferQueueEntry(
                        entry.DisplayName,
                        entry.FileSize,
                        result.Count == 0));
                }
            }
            if (result.Count == 0)
            {
                result.Add(new FileTransferQueueEntry(
                    GetDisplayName(primary),
                    primary.FileSize,
                    true));
            }

            foreach (var queuedItem in _queue.ToArray())
            {
                if (result.Count >= MaximumVisibleQueueItems) break;
                if (ReferenceEquals(queuedItem, primary) ||
                    queuedItem.IsCanceled ||
                    queuedItem.IsTerminal)
                {
                    continue;
                }
                result.Add(new FileTransferQueueEntry(
                    GetSourceDisplayName(queuedItem.Request.LocalPath),
                    TryGetSourceSize(queuedItem.Request.LocalPath),
                    false));
            }
            return result;
        }

        private int CountQueuedItems(TransferWorkItem primary)
        {
            var count = 0;
            if (!primary.IsTerminal && primary.Entries != null)
            {
                var skippedActive = false;
                for (var index = Math.Max(primary.CurrentEntryIndex, 0);
                    index < primary.Entries.Count;
                    index++)
                {
                    if (primary.Entries[index].IsDirectory) continue;
                    if (!skippedActive)
                    {
                        skippedActive = true;
                        continue;
                    }
                    count++;
                }
            }
            foreach (var queuedItem in _queue.ToArray())
            {
                if (ReferenceEquals(queuedItem, primary) ||
                    queuedItem.IsCanceled ||
                    queuedItem.IsTerminal)
                {
                    continue;
                }
                count++;
            }
            return count;
        }

        private static string GetDisplayName(TransferWorkItem item)
        {
            return string.IsNullOrWhiteSpace(item.FileName)
                ? GetSourceDisplayName(item.Request.LocalPath)
                : item.FileName;
        }

        private static string GetSourceDisplayName(string path)
        {
            var trimmed = (path ?? string.Empty).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? trimmed : name;
        }

        private static long TryGetSourceSize(string path)
        {
            try
            {
                return File.Exists(path)
                    ? new FileInfo(path).Length
                    : 0L;
            }
            catch
            {
                return 0L;
            }
        }

    }
}
