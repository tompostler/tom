using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Unlimitedinf.Utilities.Concurrency
{
    /// <summary>
    /// Monitors the progress of worker tasks and a queue of work items, providing periodic updates on the remaining
    /// work and estimated completion time.
    /// </summary>
    /// <remarks>
    /// This class is designed to track the progress of concurrent worker tasks processing a queue of work items.
    /// It periodically logs information about the remaining work items, estimated time to completion, and processing rate based on recent history.
    /// The monitoring continues until all work items are processed and all worker tasks are completed.
    /// </remarks>
    public sealed class WorkQueueMonitor(ILogger logger)
    {
        private readonly ILogger logger = logger;

        /// <summary>
        /// Given an array of worker tasks, a queue of work items, and the original count of work items, monitor the progress of the workers and the queue with time estimates based on recently completed work.
        /// </summary>
        public async Task MonitorAsync<T>(
            Task[] workers,
            ConcurrentQueue<T> workItems,
            double workItemOriginalCount,
            CancellationToken cancellationToken,
            int reportingIntervalSeconds = 7)
        {
            var duration = Stopwatch.StartNew();
            int threshold = 10;
            int bufferLen = 25;
            var historyBuffer = new List<int> { (int)workItemOriginalCount };
            var sleepTime = TimeSpan.FromSeconds(reportingIntervalSeconds);
            while (!workItems.IsEmpty)
            {
                await Task.Delay(sleepTime, cancellationToken);

                historyBuffer.Add(workItems.Count);
                if (historyBuffer.Count > threshold)
                {
                    if (historyBuffer.Count > bufferLen)
                    {
                        historyBuffer.RemoveAt(0);
                    }

                    int[] diffs = new int[historyBuffer.Count - 1];
                    for (int i = 0; i < diffs.Length; i++)
                    {
                        diffs[i] = historyBuffer[i + 1] - historyBuffer[i];
                    }

                    double avgSlice = diffs.Average();
                    double perSec = Math.Max(Math.Abs(avgSlice) / sleepTime.TotalSeconds, 0.00001);

                    this.logger.LogInformation(
                        "---> {workRemainingCount,5} ({workRemainingPercent,6:p}) work items remaining. Estimated {timeRemaining:hh\\:mm\\:ss} remaining ({timeElapsed:hh\\:mm\\:ss} elapsed, {rateCompletion,5:0.00} items/sec for last {previousItemCount} items).",
                        workItems.Count,
                        workItems.Count / workItemOriginalCount,
                        TimeSpan.FromSeconds(workItems.Count / perSec),
                        duration.Elapsed,
                        perSec,
                        historyBuffer.Count);
                }
                else
                {
                    this.logger.LogInformation(
                        "---> {workRemainingCount,5} ({workRemainingPercent,6:p}) work items remaining ({pollsToHistory} polls until there is enough history for estimation).",
                        workItems.Count,
                        workItems.Count / workItemOriginalCount,
                        threshold - historyBuffer.Count);
                }
            }
            this.logger.LogInformation("Scanning all workers....");
            while (workers.Any(t => !t.IsCompleted))
            {
                this.logger.LogInformation("---> {workerCount} workers still working....", workers.Count(t => !t.IsCompleted));
                await Task.Delay(sleepTime, cancellationToken);
            }
            this.logger.LogInformation("Awaiting all workers....");
            await Task.WhenAll(workers);
            this.logger.LogInformation("Workers are done working.");
        }
    }
}
