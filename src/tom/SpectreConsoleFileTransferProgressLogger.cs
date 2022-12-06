using Spectre.Console;
using System;

namespace Unlimitedinf.Tom
{
    /// <summary>
    /// Note: Naively designed to only run one file transfer at a time.
    /// </summary>
    internal sealed class SpectreConsoleFileTransferProgressLogger
    {
        private long currentFileSeenLength;
        private long currentFileExpectedLength;

        private long totalFileSeenLength;
        private readonly long totalFileExpectedLength;

        private ProgressContext context;
        private readonly ProgressTask currentFileBytesTask;
        private readonly ProgressTask totalFileCountTask;
        private readonly ProgressTask totalFileBytesTask;

        /// <inheritdoc/>
        public SpectreConsoleFileTransferProgressLogger(
            string currentFileName,
            long currentFileExpectedLength,
            long totalFileCount,
            long totalFileExpectedLength)
        {
            this.currentFileExpectedLength = currentFileExpectedLength;
            this.totalFileExpectedLength = totalFileExpectedLength;

            Progress progress = AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new DownloadedColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new RemainingTimeColumn(),
                    new TransferSpeedColumn(),
                });
            progress.RefreshRate = TimeSpan.FromSeconds(0.5);
            progress.Start(context => { this.context = context; });

            this.currentFileBytesTask = this.context.AddTask(currentFileName);
            this.totalFileCountTask = this.context.AddTask("Total file count", maxValue: totalFileCount);
            this.totalFileBytesTask = this.context.AddTask("Total file bytes");
        }

        /// <summary>
        /// Add more bytes to the current progress.
        /// </summary>
        public void AddProgress(long value)
        {
            this.currentFileSeenLength += value;
            this.currentFileBytesTask.Value = this.currentFileSeenLength * 100d / this.currentFileExpectedLength;

            this.totalFileSeenLength += value;
            this.totalFileBytesTask.Value = this.totalFileSeenLength * 100d / this.totalFileExpectedLength;
        }

        /// <summary>
        /// Add a new file.
        /// </summary>
        public void ResetCurrentFile(string newFileName, long newExpectedBytesLength)
        {
            this.currentFileBytesTask.Description = newFileName;
            this.currentFileBytesTask.Value = 0;
            this.currentFileSeenLength = 0;
            this.currentFileExpectedLength = newExpectedBytesLength;

            this.totalFileCountTask.Increment(1);
        }

        /// <summary>
        /// Mark all bars as complete.
        /// </summary>
        public void MarkComplete()
        {
            this.currentFileBytesTask.Value = this.currentFileBytesTask.MaxValue;
            this.totalFileCountTask.Value = this.totalFileCountTask.MaxValue;
            this.totalFileBytesTask.Value = this.totalFileBytesTask.MaxValue;
        }
    }
}
