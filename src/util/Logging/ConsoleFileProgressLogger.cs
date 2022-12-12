using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities.Logging
{
    /// <summary>
    /// A similar style to spectre.console's progress logger, but with the nuances that I prefer.
    /// </summary>
    public sealed class ConsoleFileProgressLogger
    {
        private string currentFileName;
        private long currentFileExpectedLength;
        private readonly Stopwatch currentFileInterval;
        private readonly Queue<(DateTimeOffset, long)> currentFileByteProgress = new(5);

        private long currentFileBytes = 0;
        private long currentFileNumber = 1;
        private long totalFileBytes = 0;
        private readonly object consoleOutputLock = new();

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
            this.currentFileName = newFileName;
            this.currentFileExpectedLength = newExpectedBytesLength;
            this.currentFileInterval.Restart();
            this.currentFileByteProgress.Clear();
            this.currentFileNumber += 1;
        }

        /// <summary>
        /// Mark all bars as complete.
        /// </summary>
        public void MarkComplete()
        {
            this.currentFileBytes = this.currentFileExpectedLength;
            this.totalFileBytes = this.totalFileExpectedLength;
            this.currentFileNumber = this.totalFileCount;

            this.UpdateConsole();
        }

        private void UpdateConsole(bool ignoreUpdateInterval = false)
        {
            if (!ignoreUpdateInterval && this.updateInterval.Elapsed.TotalSeconds < 1)
            {
                return;
            }

            int startingLeft = Console.CursorLeft;
            int startingTop = Console.CursorTop;

            lock (this.consoleOutputLock)
            {
                const int lookbackSliceCount = 5;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Console.WriteLine();

                // First line is the current file progress
                long? currentFileBytesPerSecond = default;
                if (this.currentFileByteProgress.Count >= lookbackSliceCount)
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

                // Second line is the file count progress
                AddLine(
                    "Total file count",
                    this.currentFileName.Length,
                    this.currentFileNumber,
                    this.totalFileCount,
                    elapsedTime: default,
                    bytesPerSecond: default);

                // Third line is the total file progress
                long? totalFileBytesPerSecond = default;
                if (this.totalFileByteProgress.Count >= lookbackSliceCount)
                {
                    (DateTimeOffset, long) ago = this.totalFileByteProgress.Dequeue();
                    long bytesProgress = this.totalFileBytes - ago.Item2;
                    long durationSeconds = (long)(now - ago.Item1).TotalSeconds;
                    currentFileBytesPerSecond = bytesProgress / durationSeconds;
                }
                AddLine(
                    "Total file bytes",
                    this.currentFileName.Length,
                    this.totalFileBytes,
                    this.totalFileExpectedLength,
                    this.totalFileInterval.Elapsed,
                    totalFileBytesPerSecond);
            }

            Console.SetCursorPosition(startingLeft, startingTop);
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

            // First column is the row name. 16-64 characters
            if (currentLineName.Length > 64)
            {
                currentLineName = string.Concat("...", currentLineName.AsSpan(3, 61));
            }
            if (currentLineNameLength > 64)
            {
                currentLineNameLength = 64;
            }
            if (currentLineNameLength < 16)
            {
                currentLineNameLength = 16;
            }
            Console.Write(currentLineName.PadLeft(currentLineNameLength));
            Console.Write(' ');

            // Second column is the progress bar. 42 characters
            const int progressBarLength = 42;
            int countCompleteDashes = (int)(1.0 * progressBarLength * progressCount / totalCount);
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
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('-', countIncompleteDashes));
            Console.ForegroundColor = originalColor;
            Console.Write(' ');

            // Determine the scaling factor for the numerical progress output
            double scalingFactor;
            string units;
            if (totalCount < 1_048_576)
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

            // Third column is the numerical progress. 15 characters (but add one for the end of the column)
            const int numericalProgressLength = 16;
            string progressCountString = (progressCount / scalingFactor).ToString("0.0");
            if (progressCount == totalCount)
            {
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
                Console.Write(progressCountString);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write('/');
                Console.ForegroundColor = originalColor;

                string totalCountString = (totalCount / scalingFactor).ToString("0.0");
                Console.Write(totalCountString);

                Console.ForegroundColor = ConsoleColor.DarkGray;
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
                Console.Write(elapsedTime.Value.ToString());
                Console.ForegroundColor = originalColor;
                Console.Write(' ');
            }

            // Sixth column is transfer rate. 11 characters
            if (bytesPerSecond.HasValue)
            {
                Console.Write(bytesPerSecond.Value.AsBytesToFriendlyBitString().PadLeft(9));
                Console.Write("ps");
            }

            // And that's it
            Console.WriteLine();
        }
    }
}
