using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

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
        private readonly object estimatedTimeLoggerLock = new();

        /// <summary>
        /// An optimization on <see cref="MonitorAsync{T}(Task[], ConcurrentQueue{T}, double, CancellationToken, int)"/> so you don't have to set up workers yourself.
        /// </summary>
        /// <typeparam name="T">A typed unit of work.</typeparam>
        /// <param name="workItems">The work to complete.</param>
        /// <param name="workItemHandler">Takes as input a single work item and the originally provided cancellation token, the output is expected to be a single line summary of the completed work.</param>
        /// <param name="workerCount">Number of worker tasks to spin up in parallel. Generally assumed to be less than 100.</param>
        /// <param name="cancellationToken"></param>
        /// <param name="reportingIntervalSeconds">How often to report the progress.</param>
        /// <param name="includeEstimateToCompletion">If true, will add an additional line with the time remaining estimate.</param>
        public async Task WorkAndMonitorAsync<T>(
            ConcurrentQueue<T> workItems,
            Func<T, CancellationToken, Task<string>> workItemHandler,
            int workerCount,
            CancellationToken cancellationToken,
            int reportingIntervalSeconds = 7,
            bool includeEstimateToCompletion = true)
        {
            int workItemOriginalCount = workItems.Count;

            async Task worker(int id)
            {
                while (workItems.TryDequeue(out T workItem))
                {
                    string workResult = await workItemHandler(workItem, cancellationToken);
                    lock (this.estimatedTimeLoggerLock)
                    {
                        this.logger.LogInformation($"[{id:D2}] {workResult}");
                    }
                }
            }

            var workers = new Task[workerCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = worker(i);
            }

            if (includeEstimateToCompletion)
            {
                await this.MonitorWithEstimatedCompletionAsync(
                    workers,
                    workItems,
                    workItemOriginalCount,
                    cancellationToken,
                    reportingIntervalSeconds);
            }
            else
            {
                await this.MonitorAsync(
                    workers,
                    workItems,
                    workItemOriginalCount,
                    cancellationToken,
                    reportingIntervalSeconds);
            }
        }

        /// <summary>
        /// Given an array of worker tasks, a queue of work items, and the original count of work items, monitor the progress of the workers.
        /// </summary>
        public async Task MonitorAsync<T>(
            Task[] workers,
            ConcurrentQueue<T> workItems,
            double workItemOriginalCount,
            CancellationToken cancellationToken,
            int reportingIntervalSeconds = 7)
        {
            var duration = Stopwatch.StartNew();
            var sleepTime = TimeSpan.FromSeconds(reportingIntervalSeconds);
            while (!workItems.IsEmpty)
            {
                await Task.Delay(sleepTime, cancellationToken);

                this.logger.LogInformation(
                    "---> {itemCompleteCount,5} ({itemCompletePercent,6:p}) work items complete ({itemRemainingCount,5} ({itemRemainingPercent,6:p}) work items remaining). {timeElapsed:hh\\:mm\\:ss} elapsed.",
                    workItemOriginalCount - workItems.Count,
                    (workItemOriginalCount - workItems.Count) / workItemOriginalCount,
                    workItems.Count,
                    workItems.Count / workItemOriginalCount,
                    duration.Elapsed);
            }

            await this.MonitorToFinishAsync(workers, sleepTime, cancellationToken);
        }

        /// <summary>
        /// Given an array of worker tasks, a queue of work items, and the original count of work items, monitor the progress of the workers and the queue with time estimates based on recently completed work.
        /// </summary>
        public async Task MonitorWithEstimatedCompletionAsync<T>(
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

                    lock (this.estimatedTimeLoggerLock)
                    {
                        this.logger.LogInformation(
                        "---> {itemCompleteCount,5} ({itemCompletePercent,6:p}) work items complete ({itemRemainingCount,5} ({itemRemainingPercent,6:p}) work items remaining).",
                        workItemOriginalCount - workItems.Count,
                        (workItemOriginalCount - workItems.Count) / workItemOriginalCount,
                        workItems.Count,
                        workItems.Count / workItemOriginalCount);
                        this.logger.LogInformation(
                        "---> Estimated {timeRemaining:hh\\:mm\\:ss} remaining ({timeElapsed:hh\\:mm\\:ss} elapsed, {rateCompletion,5:0.00} items/sec for last {historyCheckpointCount} checkpoints).",
                        TimeSpan.FromSeconds(workItems.Count / perSec),
                        duration.Elapsed,
                        perSec,
                        historyBuffer.Count);
                    }
                }
                else
                {
                    this.logger.LogInformation(
                        "---> {itemCompleteCount,5} ({itemCompletePercent,6:p}) work items complete ({itemRemainingCount,5} ({itemRemainingPercent,6:p}) work items remaining). {timeElapsed:hh\\:mm\\:ss} elapsed, and {pollsToHistory} polls until there is enough history for estimation.",
                        workItemOriginalCount - workItems.Count,
                        (workItemOriginalCount - workItems.Count) / workItemOriginalCount,
                        workItems.Count,
                        workItems.Count / workItemOriginalCount,
                        duration.Elapsed,
                        threshold - historyBuffer.Count + 1);
                }
            }

            await this.MonitorToFinishAsync(workers, sleepTime, cancellationToken);
        }

        private async Task MonitorToFinishAsync(
            Task[] workers,
            TimeSpan sleepTime,
            CancellationToken cancellationToken)
        {
            this.logger.LogInformation("---> Scanning all workers....");
            while (workers.Any(t => !t.IsCompleted))
            {
                this.logger.LogInformation("---> {workerCount} workers still working....", workers.Count(t => !t.IsCompleted));
                await Task.Delay(sleepTime, cancellationToken);
            }

            this.logger.LogInformation("---> Awaiting all workers....");
            await Task.WhenAll(workers);
            this.logger.LogInformation("---> Workers are done working.");
        }
    }
}
