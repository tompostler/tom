using System.Diagnostics;
using System.Runtime.InteropServices;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities.Logging
{
    /// <summary>
    /// A similar style to spectre.console's progress logger, but with the nuances that I prefer.
    /// </summary>
    public sealed class ConsoleFileProgressLogger
    {
        /// <summary>
        /// The interval at which to update the progress logger. Defaults to 2s.
        /// </summary>
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(2);

        private string currentFileName;
        private long currentFileExpectedLength;
        private readonly Stopwatch currentFileInterval;
        private readonly Queue<(DateTimeOffset, long)> currentFileByteProgress = new(5);

        private long currentFileBytes = 0;
        private long currentFileNumber = 0;
        private long totalFileBytes = 0;
#if NET10_0_OR_GREATER
        private readonly Lock consoleOutputLock = new();
#else
        private readonly object consoleOutputLock = new();
#endif

        private readonly long totalFileCount;
        private readonly long totalFileExpectedLength;
        private readonly Stopwatch totalFileInterval;
        private readonly Queue<(DateTimeOffset, long)> totalFileByteProgress = new(5);

        private readonly Stopwatch updateInterval;

        /// <inheritdoc/>
        public ConsoleFileProgressLogger(
            string currentFileName,
            long currentFileExpectedLength,
            long totalFileCount,
            long totalFileExpectedLength)
        {
            this.currentFileName = currentFileName;
            this.currentFileExpectedLength = currentFileExpectedLength;
            this.currentFileInterval = Stopwatch.StartNew();
            this.totalFileCount = totalFileCount;
            this.totalFileExpectedLength = totalFileExpectedLength;
            this.totalFileInterval = Stopwatch.StartNew();
            this.updateInterval = Stopwatch.StartNew();
        }

        /// <summary>
        /// Add more bytes to the current progress.
        /// </summary>
        public void AddProgress(long value)
        {
            this.currentFileBytes += value;
            this.totalFileBytes += value;

            this.UpdateConsole();
        }

        /// <summary>
        /// Add a new file.
        /// </summary>
        public void ResetCurrentFile(string newFileName, long newExpectedBytesLength)
        {
            if (this.currentFileName != default)
            {
                this.currentFileNumber += 1;
            }
            this.currentFileName = newFileName;
            this.currentFileBytes = 0;
            this.currentFileExpectedLength = newExpectedBytesLength;
            this.currentFileInterval.Restart();
            this.currentFileByteProgress.Clear();
        }

        /// <summary>
        /// Mark all bars as complete.
        /// </summary>
        public void MarkComplete()
        {
            this.currentFileBytes = this.currentFileExpectedLength;
            this.totalFileBytes = this.totalFileExpectedLength;
            this.currentFileNumber = this.totalFileCount;

            this.UpdateConsole(isFinalUpdate: true);
        }

        private void UpdateConsole(bool isFinalUpdate = false)
        {
            if (!isFinalUpdate && this.updateInterval.Elapsed < this.UpdateInterval)
            {
                return;
            }

            lock (this.consoleOutputLock)
            {
                int startingLeft = Console.CursorLeft;
                int startingTop = Console.CursorTop;

                DateTimeOffset now = DateTimeOffset.UtcNow;
                Console.WriteLine();

                // First line is the current file progress
                if (!isFinalUpdate)
                {
                    const int currentFileLookbackSliceCount = 3;
                    long? currentFileBytesPerSecond = default;
                    if (this.currentFileByteProgress.Count >= currentFileLookbackSliceCount)
                    {
                        (DateTimeOffset, long) ago = this.currentFileByteProgress.Dequeue();
                        long bytesProgress = this.currentFileBytes - ago.Item2;
                        long durationSeconds = (long)(now - ago.Item1).TotalSeconds;
                        currentFileBytesPerSecond = bytesProgress / durationSeconds;
                    }
                    AddLine(
                        this.currentFileName,
                        this.currentFileName.Length,
                        this.currentFileBytes,
                        this.currentFileExpectedLength,
                        this.currentFileInterval.Elapsed,
                        currentFileBytesPerSecond);
                    this.currentFileByteProgress.Enqueue((now, this.currentFileBytes));
                }

                // Second line is the file count progress
                AddLine(
                    "Total file count",
                    isFinalUpdate ? 0 : this.currentFileName.Length,
                    this.currentFileNumber,
                    this.totalFileCount,
                    elapsedTime: default,
                    bytesPerSecond: default);

                // Third line is the total file progress
                const int totalFileLookbackSliceCount = 10;
                long? totalFileBytesPerSecond = default;
                if (isFinalUpdate)
                {
                    totalFileBytesPerSecond = this.totalFileBytes / (long)this.totalFileInterval.Elapsed.TotalSeconds;
                }
                else if (this.totalFileByteProgress.Count >= totalFileLookbackSliceCount)
                {
                    (DateTimeOffset, long) ago = this.totalFileByteProgress.Dequeue();
                    long bytesProgress = this.totalFileBytes - ago.Item2;
                    long durationSeconds = (long)(now - ago.Item1).TotalSeconds;
                    totalFileBytesPerSecond = bytesProgress / durationSeconds;
                }
                AddLine(
                    "Total file bytes",
                    isFinalUpdate ? 0 : this.currentFileName.Length,
                    this.totalFileBytes,
                    this.totalFileExpectedLength,
                    this.totalFileInterval.Elapsed,
                    totalFileBytesPerSecond);
                this.totalFileByteProgress.Enqueue((now, this.totalFileBytes));

                if (isFinalUpdate)
                {
                    // Need to overwrite the skipped first line with spaces
                    // Max line length is about 128 when accounting for columns and spaces
                    Console.WriteLine(new string(' ', 128));
                }
                else
                {
                    // Otherwise set the position back to the top of the section
                    Console.SetCursorPosition(startingLeft, startingTop);
                }
                Console.CursorVisible = isFinalUpdate;
            }

            this.updateInterval.Restart();
        }

        private static void AddLine(
            string currentLineName,
            int currentLineNameLength,
            long progressCount,
            long totalCount,
            TimeSpan? elapsedTime,
            long? bytesPerSecond)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            // On the linux terminal I normally use, DarkGray translates to black. So use Gray instead.
            ConsoleColor miscColor = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ConsoleColor.DarkGray : ConsoleColor.Gray;

            // First column is the row name. 16-42 characters
            const int rowNameLength = 42;
            if (currentLineName.Length > rowNameLength)
            {
                currentLineName = string.Concat("...", currentLineName.AsSpan(currentLineName.Length - rowNameLength + 3));
            }
            if (currentLineNameLength > rowNameLength)
            {
                currentLineNameLength = rowNameLength;
            }
            if (currentLineNameLength < 16)
            {
                currentLineNameLength = 16;
            }
            Console.Write(currentLineName.PadLeft(currentLineNameLength));
            Console.Write(' ');

            // Second column is the progress bar. 42 characters
            const int progressBarLength = 42;
            int countCompleteDashes = (int)(1.0 * progressBarLength * Math.Min(progressCount, totalCount) / totalCount);
            if (countCompleteDashes < progressBarLength)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            else if (countCompleteDashes == progressBarLength)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            Console.Write(new string('-', countCompleteDashes));
            int countIncompleteDashes = progressBarLength - countCompleteDashes;
            if (countIncompleteDashes > 0)
            {
                Console.ForegroundColor = miscColor;
                Console.Write(new string('-', countIncompleteDashes));
            }
            Console.ForegroundColor = originalColor;
            Console.Write(' ');

            // Determine the scaling factor for the numerical progress output
            double scalingFactor;
            string units;
            if (totalCount < 1_024)
            {
                scalingFactor = 1;
                units = "B";
            }
            else if (totalCount < 1_048_576)
            {
                scalingFactor = 1_024d;
                units = "KiB";
            }
            else if (totalCount >= 1_048_576 && totalCount < 1_073_741_824)
            {
                scalingFactor = 1_048_576d;
                units = "MiB";
            }
            else if (totalCount >= 1_073_741_824 && totalCount < 1_099_511_627_776)
            {
                scalingFactor = 1_073_741_824d;
                units = "GiB";
            }
            else
            {
                scalingFactor = 1_099_511_627_776d;
                units = "TiB";
            }

            // If there's no elapsed time specified, then we're on the file count line and we can skip a bunch of stuff
            string numericProgressFormat = "0.00";
            if (!elapsedTime.HasValue)
            {
                scalingFactor = 1;
                units = string.Empty;
                numericProgressFormat = "0";
            }

            // Third column is the numerical progress.
            string progressCountString = (progressCount / scalingFactor).ToString(numericProgressFormat);
            if (progressCount == totalCount)
            {
                // 11 characters when complete (but add one for the end of the column)
                const int numericalProgressLength = 12;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(progressCountString);
                Console.Write(' ');
                Console.Write(units);
                Console.ForegroundColor = originalColor;

                Console.Write(
                    new string(
                        ' ',
                        numericalProgressLength
                        - progressCountString.Length
                        - 1
                        - units.Length));
            }
            else
            {
                // 18 characters (but add one for the end of the column)
                const int numericalProgressLength = 19;

                Console.Write(progressCountString);

                Console.ForegroundColor = miscColor;
                Console.Write('/');
                Console.ForegroundColor = originalColor;

                string totalCountString = (totalCount / scalingFactor).ToString(numericProgressFormat);
                Console.Write(totalCountString);

                Console.ForegroundColor = miscColor;
                Console.Write(' ');
                Console.Write(units);
                Console.ForegroundColor = originalColor;

                Console.Write(
                    new string(
                        ' ',
                        numericalProgressLength
                        - progressCountString.Length
                        - 1
                        - totalCountString.Length
                        - 1
                        - units.Length));
            }

            // Fourth column is percent progress. 6 characters
            const int percentProgressLength = 6;
            string percentProgressString = (100.0 * progressCount / totalCount).ToString("0.0") + '%';
            if (progressCount == totalCount)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            Console.Write(percentProgressString.PadLeft(percentProgressLength));
            Console.ForegroundColor = originalColor;
            Console.Write(' ');

            // Fifth column is elapsed time. 8 characters, unless it's longer than a day, but probably not...
            if (elapsedTime.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(elapsedTime.Value.ToString("hh\\:mm\\:ss"));
                Console.ForegroundColor = originalColor;
            }
            else
            {
                Console.Write(new string(' ', 8));
            }
            Console.Write(' ');

            // Sixth column is transfer rate. 10 characters
            if (bytesPerSecond.HasValue)
            {
                Console.Write(bytesPerSecond.Value.AsBytesToFriendlyBitString().PadLeft(8));
                Console.Write("ps");
            }
            else
            {
                Console.Write(new string(' ', 10));
            }

            // And that's it
            Console.WriteLine(new string(' ', rowNameLength - currentLineName.Length));
        }
    }
}
