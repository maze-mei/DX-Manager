using System;

namespace DexManager.Models
{
    public sealed class ProcessResult
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public bool TimedOut { get; set; }
        public TimeSpan Duration { get; set; }

        public bool IsSuccess
        {
            get { return !TimedOut && ExitCode == 0; }
        }
    }
}
