using Microsoft.Extensions.Logging;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities.Logging
{
    /// <summary>
    /// Log the progress of a long-running operation.
    /// </summary>
    public sealed class ProgressLogger : IProgress<long>
    {
        private readonly long expectedLength;
        private readonly ILogger logger;

        private readonly object reportingLock = new();
        private readonly Queue<(DateTimeOffset, long)> byteProgress = new(3);
        private readonly DateTimeOffset started = DateTimeOffset.UtcNow;

        private DateTimeOffset? lastReported = default;

        /// <inheritdoc/>
        public ProgressLogger(long expectedLength, ILogger logger)
        {
            this.expectedLength = expectedLength;
            this.logger = logger;
        }

        /// <summary>
        /// Log the current progress. Will only emit values once every 3 seconds regardless of the number of calls.
        /// </summary>
        public void Report(long value)
        {
            const int intervalSecondsToReport = 3;
            const int lookbackSliceCount = 5;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // If we've just started or once every couple of seconds, then continue with reporting
            if (!this.lastReported.HasValue || (now - this.lastReported.Value).TotalSeconds > intervalSecondsToReport)
            {
                // Locks during the calculation/generation of the actual report in order to ensure only one thread is reporting on the object at a time.
                // If multiple thread reports came in here, they'll just log a couple times which is fine. So we're not re-checking the condition to log.
                lock (this.reportingLock)
                {
                    if (this.byteProgress.Count >= lookbackSliceCount)
                    {
                        (DateTimeOffset, long) ago = this.byteProgress.Dequeue();
                        long bytesProgress = value - ago.Item2;
                        decimal durationSeconds = (decimal)(now - ago.Item1).TotalSeconds;
                        decimal durationDisplay = durationSeconds;
                        string durationUnit = "seconds";
                        if (durationDisplay > 100)
                        {
                            durationDisplay /= 60;
                            durationUnit = "minutes";
                        }
                        this.logger.LogInformation($"Progress: {value.AsBytesToFriendlyString(),9}/{this.expectedLength.AsBytesToFriendlyString()} ({1.0 * value / this.expectedLength:p}). Average over last {durationDisplay:0.0} {durationUnit}: {BytesToFriendlyBitString(bytesProgress / durationSeconds)}ps");
                    }
                    else
                    {
                        this.logger.LogInformation($"Progress: {value.AsBytesToFriendlyString(),9}/{this.expectedLength.AsBytesToFriendlyString()} ({1.0 * value / this.expectedLength:p})");
                    }
                    this.lastReported = now;
                    this.byteProgress.Enqueue((this.lastReported.Value, value));
                }
            }
        }

        /// <summary>
        /// Report 100% based on expected length.
        /// </summary>
        public void ReportComplete()
        {
            TimeSpan duration = DateTimeOffset.UtcNow - this.started;
            decimal durationSeconds = (decimal)duration.TotalSeconds;
            this.logger.LogInformation($"Progress: {this.expectedLength.AsBytesToFriendlyString(),9}/{this.expectedLength.AsBytesToFriendlyString()} ({1.0:p}). Averaged {BytesToFriendlyBitString(this.expectedLength / durationSeconds)}ps over {duration:mm\\:ss}.");
        }

        private static string BytesToFriendlyBitString(decimal bytes)
        {
            decimal bits = bytes * 8;
            if (bits < 1_000_000)
            {
                return $"{bits / 1_000:0.00}Kb";
            }
            else if (bits >= 1_000_000 && bits < 1_000_000_000)
            {
                return $"{bits / 1_000_000:0.00}Mb";
            }
            else if (bits >= 1_000_000_000 && bits < 1_000_000_000_000)
            {
                return $"{bits / 1_000_000_000:0.00}Gb";
            }
            else
            {
                return $"{bits / 1_000_000_000_000:0.00}Tb";
            }
        }
    }
}
