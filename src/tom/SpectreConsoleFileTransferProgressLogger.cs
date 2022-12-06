using Spectre.Console;
using System;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom
{
    /// <summary>
    /// Note: Naively designed to only run one file transfer at a time.
    /// </summary>
    internal sealed class SpectreConsoleFileTransferProgressLogger
    {
        private readonly Task progressTask;
        private ProgressTask currentFileBytesTask;
        private ProgressTask totalFileCountTask;
        private ProgressTask totalFileBytesTask;

        /// <inheritdoc/>
        public SpectreConsoleFileTransferProgressLogger(
            string currentFileName,
            long currentFileExpectedLength,
            long totalFileCount,
            long totalFileExpectedLength)
        {
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
            this.progressTask = progress.StartAsync(
                async context =>
                {
                    this.currentFileBytesTask = context.AddTask(currentFileName ?? "TBD", maxValue: currentFileExpectedLength);
                    this.currentFileBytesTask.StartTask();

                    this.totalFileCountTask = context.AddTask("Total file count", maxValue: totalFileCount);
                    this.totalFileCountTask.StartTask();

                    this.totalFileBytesTask = context.AddTask("Total file bytes", maxValue: totalFileExpectedLength);
                    this.totalFileBytesTask.StartTask();

                    while (!context.IsFinished)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.1));
                    }
                });
        }

        /// <summary>
        /// Add more bytes to the current progress.
        /// </summary>
        public void AddProgress(long value)
        {
            this.currentFileBytesTask.Increment(value);
            this.totalFileBytesTask.Increment(value);
        }

        /// <summary>
        /// Add a new file.
        /// </summary>
        public void ResetCurrentFile(string newFileName, long newExpectedBytesLength)
        {
            this.currentFileBytesTask.Description = newFileName;
            this.currentFileBytesTask.Value = 0;
            this.currentFileBytesTask.MaxValue = newExpectedBytesLength;

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

            this.progressTask.Wait();
        }
    }
}
