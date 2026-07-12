using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class CaptureService
    {
        private readonly AdbService _adbService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;
        private readonly LogService _logService;

        public CaptureService(
            AdbService adbService,
            SettingsService settingsService,
            AppSettings settings,
            LogService logService)
        {
            _adbService = adbService;
            _settingsService = settingsService;
            _settings = settings;
            _logService = logService;
        }

        public CaptureResult CaptureWindow(IntPtr windowHandle)
        {
            return CaptureWindow(windowHandle, _adbService.TargetSerial);
        }

        public CaptureResult CaptureWindow(
            IntPtr windowHandle,
            string serial)
        {
            if (windowHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Capture.ScrcpyWindowNotFound"));

            NativeRect clientRect;
            var clientOrigin = new NativePoint();
            if (!NativeMethods.GetClientRect(windowHandle, out clientRect) ||
                !NativeMethods.ClientToScreen(windowHandle, ref clientOrigin))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Capture.WindowAreaUnavailable"));
            }

            var rectangle = new Rectangle(
                clientOrigin.X,
                clientOrigin.Y,
                clientRect.Right - clientRect.Left,
                clientRect.Bottom - clientRect.Top);
            return CaptureRectangle(rectangle, "DeX_Full", serial);
        }

        public CaptureResult CaptureRectangle(Rectangle rectangle, string prefix)
        {
            return CaptureRectangle(
                rectangle,
                prefix,
                _adbService.TargetSerial);
        }

        public CaptureResult CaptureRectangle(
            Rectangle rectangle,
            string prefix,
            string serial)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
                throw new ArgumentException(
                    LocalizationService.Get(
                        "Error.Capture.InvalidArea"),
                    "rectangle");

            var folder = _settingsService.ResolvePath(
                _settings.Paths.ScreenshotFolder);
            Directory.CreateDirectory(folder);

            var fileName = string.Format(
                "{0}_{1}.png",
                prefix,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            var localPath = Path.Combine(folder, fileName);

            using (var bitmap = new Bitmap(
                rectangle.Width,
                rectangle.Height,
                PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    rectangle.Location,
                    Point.Empty,
                    rectangle.Size,
                    CopyPixelOperation.SourceCopy);
                bitmap.Save(localPath, ImageFormat.Png);
            }

            _logService.Info(LocalizationService.Format(
                "Log.Capture.Saved",
                localPath));
            var transferred = false;
            var remotePath = string.Empty;

            if (_settings.Features.PushCaptureToDevice &&
                !string.IsNullOrWhiteSpace(serial))
            {
                remotePath = CombineDevicePath(
                    _settings.Paths.DeviceScreenshotFolder,
                    fileName);
                TransferToDevice(localPath, remotePath, serial);
                transferred = true;
            }
            else if (_settings.Features.PushCaptureToDevice)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Capture.PushSkippedNoTarget"));
            }

            return new CaptureResult(localPath, remotePath, transferred);
        }

        private void TransferToDevice(
            string localPath,
            string remotePath,
            string serial)
        {
            var remoteFolder = _settings.Paths.DeviceScreenshotFolder.TrimEnd('/');
            var pushResult = Push(serial, localPath, remotePath);
            if (!pushResult.IsSuccess)
            {
                var mkdirResult = Shell(serial,
                    "mkdir -p " + ShellQuote(remoteFolder));
                if (!mkdirResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        LocalizationService.Format(
                            "Error.Capture.DeviceFolderFailed",
                            GetCommandError(mkdirResult)));
                }

                pushResult = Push(serial, localPath, remotePath);
                if (!pushResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        LocalizationService.Format(
                            "Error.Capture.PushFailed",
                            GetCommandError(pushResult)));
                }
            }

            var mediaUri = "file://" + remotePath;
            var scanResult = Shell(serial,
                "am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d " +
                ShellQuote(mediaUri));
            if (!scanResult.IsSuccess)
                _logService.Warning(LocalizationService.Get(
                    "Log.Capture.MediaScanFailed"));

            _logService.Info(LocalizationService.Format(
                "Log.Capture.Pushed",
                remotePath));
        }

        private ProcessResult Push(
            string serial,
            string localPath,
            string remotePath)
        {
            return _adbService.PushForSerial(
                serial,
                localPath,
                remotePath);
        }

        private ProcessResult Shell(string serial, string command)
        {
            return _adbService.ShellForSerial(serial, command, true);
        }

        private static string CombineDevicePath(string folder, string fileName)
        {
            return folder.TrimEnd('/') + "/" + fileName;
        }

        private static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private static string GetCommandError(ProcessResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardError))
                return result.StandardError;
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                return result.StandardOutput;
            return "ExitCode=" + result.ExitCode;
        }
    }

    public sealed class CaptureResult
    {
        public CaptureResult(
            string localPath,
            string remotePath,
            bool transferredToDevice)
        {
            LocalPath = localPath;
            RemotePath = remotePath;
            TransferredToDevice = transferredToDevice;
        }

        public string LocalPath { get; private set; }
        public string RemotePath { get; private set; }
        public bool TransferredToDevice { get; private set; }
    }
}
