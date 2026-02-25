using ScottPlot;
using ScottPlot.Plottables;
using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ChartSystemStatsCommand
    {
        private const long OneGiB = 1024 * 1024 * 1024;
        private const long OneTiB = OneGiB * 1024;

        public static Command Create()
        {
            Argument<DirectoryInfo> dataDirArgument = new Argument<DirectoryInfo>("data-dir")
            {
                Description = "The directory containing one or many files of data to use for generating the charts."
            }.AcceptExistingOnly();
            Option<bool> displayScriptOption = new("--script")
            {
                Description = "Instead of generating charts, display the script that should be installed to generate the data files."
            };
            Option<bool> mergeOption = new("--merge")
            {
                Description = "Instead of generating charts, merge any single line files into a single file."
            };
            Command command = new("chart-system-stats", "Given an input of system stat file(s), generate png charts for them.")
            {
                dataDirArgument,
                displayScriptOption,
                mergeOption,
            };
            command.SetAction(parseResult =>
            {
                DirectoryInfo dataDir = parseResult.GetRequiredValue(dataDirArgument);
                bool displayScript = parseResult.GetValue(displayScriptOption);
                bool merge = parseResult.GetValue(mergeOption);

                Handle(dataDir, displayScript, merge);
            });
            return command;
        }

        private static void Handle(DirectoryInfo dataDir, bool displayScript, bool merge)
        {
            if (displayScript)
            {
                Console.WriteLine(@"
$os = Get-CimInstance Win32_OperatingSystem;
$bootTimeSpan = (New-TimeSpan -Start $os.LastBootUpTime -End (Get-Date));
$memUsed = $os.TotalVisibleMemorySize - $os.FreePhysicalMemory;

$o = [PSCustomObject]@{
    Timestamp       = (Get-Date).ToString('o');
    UptimeHours     = $bootTimeSpan.TotalHours;
    MemoryUsedBytes = $memUsed * 1KB;
    Disks           = [PSCustomObject]@{};
};

foreach ($disk in (Get-WmiObject Win32_LogicalDisk)) {
    $val = [PSCustomObject]@{
        UsedBytes  = $disk.Size - $disk.FreeSpace;
        TotalBytes = $disk.Size;
    };
    Add-Member -MemberType NoteProperty -Name $disk.VolumeName -Value $val -InputObject $o.Disks;
}

Set-Content -Path ""Z:\system-stats\data\$((Get-Date).ToString('yyyy-MM-dd_HH-mm-ss')).json"" -Value ($o | ConvertTo-Json -Compress);
");
                return;
            }

            var sw = Stopwatch.StartNew();

            // Enumerate the files first
            var dataFiles = dataDir.EnumerateFiles().Where(x => x.Extension != ".png").ToList();
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Found {dataFiles.Count} data files to enumerate");

            // If we're going to merge, set up for that
            FileInfo mergedFile = default;
            StreamWriter mergedFileWriter = default;
            int mergedLineCount = 0;
            if (merge)
            {
                mergedFile = new(Path.Combine(dataDir.FullName, "merged_" + Guid.NewGuid().ToString().Split('-').First() + ".txt"));
                Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Merging existing single line data files into {mergedFile.FullName}");
                mergedFile.Delete();
                mergedFileWriter = new(mergedFile.OpenWrite());
            }

            // Build up all the rows of all the files in memory while keeping track of the disk max size
            // Unless we're merging, then instead write them to the merged file
            List<SystemStatDataRow> dataRows = [];
            Dictionary<string, long> diskMaxSize = [];
            foreach (FileInfo dataFile in dataFiles)
            {
                using StreamReader sr = new(dataFile.OpenRead());
                string[] fileLines = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (merge)
                {
                    if (fileLines.Length == 0)
                    {
                        Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Had 0 lines, so skipping: {dataFile.FullName}");
                    }
                    else if (fileLines.Length > 1)
                    {
                        Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Had {fileLines.Length} lines, so skipping: {dataFile.FullName}");
                    }
                    else
                    {
                        mergedFileWriter.WriteLine(fileLines.Single());
                        mergedLineCount++;
                    }
                    continue;
                }

                foreach (string fileLine in fileLines)
                {
                    try
                    {
                        SystemStatDataRow dataRow = JsonSerializer.Deserialize<SystemStatDataRow>(fileLine);
                        dataRows.Add(dataRow);
                        foreach (KeyValuePair<string, SystemStatDataRow.DiskStats> disk in dataRow.Disks)
                        {
                            if (diskMaxSize.TryGetValue(disk.Key, out long value))
                            {
                                diskMaxSize[disk.Key] = Math.Max(value, disk.Value.UsedBytes);
                            }
                            else
                            {
                                diskMaxSize.Add(disk.Key, disk.Value.UsedBytes);
                            }
                        }
                    }
                    catch
                    {
                        Console.Error.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Could not parse the following as a {nameof(SystemStatDataRow)}:\n{fileLine}");
                        throw;
                    }
                }
            }
            if (merge)
            {
                Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Found {mergedLineCount} rows of data in {dataFiles.Count} files");
                mergedFileWriter.Dispose();
                return;
            }
            else
            {
                Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Found {dataRows.Count} rows of data to use");
            }

            // Sort the rows by the timestamp
            dataRows.Sort((l, r) => l.Timestamp.CompareTo(r.Timestamp));
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Finished sorting data");

            // While running through the data, only sample one value every so often to keep the number of plotted
            // points to 1 every ~5 pixels per series. Pick the smallest "decent" interval that achieves this; if the
            // dataset is already small enough, use every point. ScottPlot handles tick label density automatically.
            const int targetMaxPoints = 500;
            TimeSpan dataRange = dataRows.Last().Timestamp.Subtract(dataRows.First().Timestamp);
            TimeSpan[] candidateIntervals =
            [
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(15),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(2),
                TimeSpan.FromHours(4),
                TimeSpan.FromHours(6),
                TimeSpan.FromHours(12),
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2),
                TimeSpan.FromDays(3),
                TimeSpan.FromDays(7),
                TimeSpan.FromDays(14),
                TimeSpan.FromDays(30),
            ];
            TimeSpan minimumInterval = dataRange / targetMaxPoints;
            TimeSpan sampleSize = dataRows.Count <= targetMaxPoints
                ? TimeSpan.Zero
                : candidateIntervals.FirstOrDefault(interval => interval >= minimumInterval, candidateIntervals[^1]);
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Using a sampling interval of {sampleSize}");

            // Run through the data and create three charts:

            // 1. UptimeDays and MemoryUsedGiB
            List<DateTime> xValues = [];
            List<double> uptimeDaysValues = [];
            List<double> memoryGiBValues = [];

            // 2. Disk.UsedGiB (where max <= 1TiB)
            var smallDiskValues = diskMaxSize.Where(x => x.Value <= OneTiB).ToDictionary(x => x.Key, x => new List<double>());

            // 3. Disk.UsedTiB (where max > 1TiB)
            var largeDiskValues = diskMaxSize.Where(x => x.Value > OneTiB).ToDictionary(x => x.Key, x => new List<double>());

            // Actually run through the data
            DateTimeOffset previousRowTimestamp = DateTimeOffset.MinValue;
            foreach (SystemStatDataRow dataRow in dataRows)
            {
                // If it's too many data points, then skip to the next one
                if (dataRow.Timestamp.Subtract(previousRowTimestamp) < sampleSize)
                {
                    continue;
                }
                previousRowTimestamp = dataRow.Timestamp;

                xValues.Add(dataRow.Timestamp.LocalDateTime);

                // 1. UptimeDays and MemoryUsedGiB
                uptimeDaysValues.Add(dataRow.UptimeHours / 24.0);
                memoryGiBValues.Add(dataRow.MemoryUsedBytes * 1.0 / OneGiB);

                foreach (KeyValuePair<string, SystemStatDataRow.DiskStats> disk in dataRow.Disks)
                {
                    // TODO: Need to figure out how to handle series that don't have the same number of x-values

                    // 2. Disk.UsedGiB (where max <= 1TiB)
                    if (smallDiskValues.TryGetValue(disk.Key, out List<double> value))
                    {
                        value.Add(disk.Value.UsedBytes * 1.0 / OneGiB);
                    }

                    // 3. Disk.UsedTiB (where max > 1TiB)
                    else
                    {
                        largeDiskValues[disk.Key].Add(disk.Value.UsedBytes * 1.0 / OneTiB);
                    }
                }

                // 4. Add empty entries for any disks we didn't see
                var disksNotSeen = diskMaxSize.Keys.Except(dataRow.Disks.Keys).ToList();
                foreach (string diskNotSeen in disksNotSeen)
                {
                    // 2. Disk.UsedGiB (where max <= 1TiB)
                    if (smallDiskValues.TryGetValue(diskNotSeen, out List<double> value))
                    {
                        value.Add(double.NaN);
                    }

                    // 3. Disk.UsedTiB (where max > 1TiB)
                    else
                    {
                        largeDiskValues[diskNotSeen].Add(double.NaN);
                    }
                }
            }
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Used {xValues.Count}/{dataRows.Count} data rows for the charts");

            // Add a summation series for any groups of disks sharing a common prefix with a numeric suffix (e.g. jbod1, jbod2 -> jbod#)
            AddGroupSummationSeries(smallDiskValues);
            AddGroupSummationSeries(largeDiskValues);

            // 1. UptimeDays and MemoryUsedGiB
            SaveChart(
                dataDir,
                BuildChart(xValues,
                [
                    ("UptimeDays", uptimeDaysValues),
                    ("MemoryUsedGiB", memoryGiBValues),
                ]),
                "Uptime and Memory",
                sw);

            // 2. Disk.UsedGiB (where max <= 1TiB)
            SaveChart(
                dataDir,
                BuildChart(xValues,
                smallDiskValues.Select(x => (x.Key, x.Value))),
                "Disks smaller than 1TiB",
                sw);

            // 3. Disk.UsedTiB (where max > 1TiB)
            SaveChart(
                dataDir,
                BuildChart(xValues,
                largeDiskValues.Select(x => (x.Key, x.Value))),
                "Disks larger than 1TiB",
                sw);
        }

        private static Plot BuildChart(List<DateTime> xs, IEnumerable<(string name, List<double> values)> series)
        {
            Plot plot = new();

            foreach ((string name, List<double> values) in series)
            {
                Scatter scatter = plot.Add.Scatter(xs, values);
                scatter.LegendText = name;
            }

            _ = plot.Axes.DateTimeTicksBottom();
            _ = plot.ShowLegend(Edge.Bottom);
            plot.Axes.TightMargins();

            return plot;
        }

        private static void SaveChart(DirectoryInfo dataDir, Plot plot, string title, Stopwatch sw)
        {
            plot.Title(title);
            _ = plot.Add.Annotation($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}", Alignment.UpperLeft);

            FileInfo chartFile = new(Path.Combine(dataDir.FullName, title.Replace(' ', '-') + ".png"));
            chartFile.Delete();
            _ = plot.SavePng(chartFile.FullName, 2560, 1440);
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Wrote {chartFile.FullName}");
        }

        private static void AddGroupSummationSeries(Dictionary<string, List<double>> diskValues)
        {
            IEnumerable<IGrouping<string, string>> groups = diskValues.Keys
                .Where(name => name.Length > 0 && char.IsDigit(name[^1]))
                .GroupBy(name => name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
                .Where(g => g.Count() > 1);

            foreach (IGrouping<string, string> group in groups)
            {
                var seriesInGroup = group.Select(name => diskValues[name]).ToList();
                int length = seriesInGroup[0].Count;
                diskValues[group.Key + "#"] = Enumerable.Range(0, length)
                    .Select(i => seriesInGroup.All(s => double.IsNaN(s[i]))
                        ? double.NaN
                        : seriesInGroup.Sum(s => double.IsNaN(s[i]) ? 0 : s[i]))
                    .ToList();
            }
        }

        private sealed class SystemStatDataRow
        {
            public DateTimeOffset Timestamp { get; set; }
            public float UptimeHours { get; set; }
            public long MemoryUsedBytes { get; set; }
            public Dictionary<string, DiskStats> Disks { get; set; }

            public sealed class DiskStats
            {
                public long UsedBytes { get; set; }
                public long TotalBytes { get; set; }
            }
        }
    }
}
