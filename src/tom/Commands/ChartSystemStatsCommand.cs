﻿using Microcharts;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ChartSystemStatsCommand
    {
        public static Command Create()
        {
            Command command = new("chart-system-stats", "Given an input of system stat file(s), generate png charts for them.");

            Argument<DirectoryInfo> dataDirArg = new(
                "data-dir",
                "The directory containing one or many files of data to use for generating the charts.");
            command.AddArgument(dataDirArg);

            Option<bool> displayScriptOpt = new(
                "--script",
                "Instead of generating charts, display the script that should be installed to generate the data files.");
            command.AddOption(displayScriptOpt);

            command.SetHandler(Handle, dataDirArg, displayScriptOpt);
            return command;
        }

        private static void Handle(DirectoryInfo dataDir, bool displayScript)
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

            // Build up all the rows of all the files in memory while keeping track of the disk max size
            var dataFiles = dataDir.EnumerateFiles().ToList();
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Found {dataFiles.Count} data files to enumerate");
            List<SystemStatDataRow> dataRows = new();
            Dictionary<string, long> diskMaxSize = new();
            foreach (FileInfo dataFile in dataFiles)
            {
                using StreamReader sr = new(dataFile.OpenRead());
                string[] fileLines = sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string fileLine in fileLines)
                {
                    try
                    {
                        SystemStatDataRow dataRow = JsonSerializer.Deserialize<SystemStatDataRow>(fileLine);
                        dataRows.Add(dataRow);
                        foreach (KeyValuePair<string, SystemStatDataRow.DiskStats> disk in dataRow.Disks)
                        {
                            if (diskMaxSize.ContainsKey(disk.Key))
                            {
                                diskMaxSize[disk.Key] = Math.Max(diskMaxSize[disk.Key], disk.Value.UsedBytes);
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
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Found {dataRows.Count} rows of data to use");

            // Sort the rows by the timestamp

            dataRows.Sort((l, r) => l.Timestamp.CompareTo(r.Timestamp));
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Finished sorting data");

            // While running through the data, only sample one value every so often based on the total range of data:
            //  Data range  Sampling size
            //  > 1 month   Daily
            //  > 1 week    Hourly
            //  > 1 day     Minutely
            //  *           Every data point
            // If there are more than 1k data points, throw. This may change in the future based on how well it is deemed to handle larger data.
            TimeSpan sampleSize = TimeSpan.Zero;
            string timestampFormat = "yyyy-MM-dd HH:mm:ss";
            TimeSpan dataRange = dataRows.Last().Timestamp.Subtract(dataRows.First().Timestamp);
            switch (dataRange.TotalDays)
            {
                case > 30:
                    sampleSize = TimeSpan.FromDays(1);
                    timestampFormat = "yyyy-MM-dd";
                    break;
                case > 7:
                    sampleSize = TimeSpan.FromHours(1);
                    timestampFormat = "yyyy-MM-dd HH";
                    break;
                case > 1:
                    sampleSize = TimeSpan.FromMinutes(1);
                    timestampFormat = "yyyy-MM-dd HH:mm";
                    break;
            }
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Using a sampling interval of {sampleSize}");

            // Run through the data and create three charts:

            // 1. UptimeDays and MemoryUsedGiB
            List<ChartEntry> uptimeDaysEntries = new();
            List<ChartEntry> memoryGiBEntries = new();

            // 2. Disk.UsedGiB (where max <= 1TiB)
            var smallDiskEntries = diskMaxSize.Where(x => x.Value <= 1099511627776).ToDictionary(x => x.Key, x => new List<ChartEntry>());

            // 3. Disk.UsedTiB (where max > 1TiB)
            var largeDiskEntries = diskMaxSize.Where(x => x.Value > 1099511627776).ToDictionary(x => x.Key, x => new List<ChartEntry>());

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

                string dataRowLabel = dataRow.Timestamp.ToString(timestampFormat);

                // 1. UptimeDays and MemoryUsedGiB
                uptimeDaysEntries.Add(new ChartEntry(dataRow.UptimeHours / 24) { Label = dataRowLabel });
                memoryGiBEntries.Add(new ChartEntry(dataRow.MemoryUsedBytes * 1f / 1073741824) { Label = dataRowLabel });

                foreach (KeyValuePair<string, SystemStatDataRow.DiskStats> disk in dataRow.Disks)
                {
                    // TODO: Need to figure out how to handle series that don't have the same number of x-values

                    // 2. Disk.UsedGiB (where max <= 1TiB)
                    if (smallDiskEntries.ContainsKey(disk.Key))
                    {
                        smallDiskEntries[disk.Key].Add(new ChartEntry(disk.Value.UsedBytes * 1f / 1073741824) { Label = dataRowLabel });
                    }

                    // 3. Disk.UsedTiB (where max > 1TiB)
                    else
                    {
                        largeDiskEntries[disk.Key].Add(new ChartEntry(disk.Value.UsedBytes * 1f / 1099511627776) { Label = dataRowLabel });
                    }
                }
            }
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Used {uptimeDaysEntries.Count}/{dataRows.Count} data rows for the charts");

            // Desired colors (skipping the less desirable ones):
            // https://coolors.co/palette/001219-005f73-0a9396-94d2bd-e9d8a6-ee9b00-ca6702-bb3e03-ae2012-9b2226
            string colorString = "005f73-0a9396-94d2bd-ee9b00-ca6702-bb3e03-ae2012-9b2226";
            Queue<SKColor> colors = new(colorString.Split('-').Select(x => SKColor.Parse(x)));

            // 1. UptimeDays and MemoryUsedGiB
            LineChart uptimeChart = new()
            {
                LabelOrientation = Orientation.Horizontal, // TODO: doesn't put the labels veritically, so they're all 2s
                LegendOption = SeriesLegendOption.Bottom,
                IsAnimated = false,

                MinValue = 0,
                MaxValue = 30,
                YAxisMaxTicks = 31,

                ShowYAxisLines = true,
                ShowYAxisText = true,

                LineMode = LineMode.Straight,
                LineSize = 1,
                PointSize = 3,

                Series = new[]
                {
                    new ChartSerie
                    {
                        Name = "UptimeDays",
                        Color = colors.Dequeue(),
                        Entries = uptimeDaysEntries
                    },
                    new ChartSerie
                    {
                        Name = "MemoryUsedGiB",
                        Color = colors.Dequeue(),
                        Entries = memoryGiBEntries
                    },
                }
            };
            SaveChart(uptimeChart, "Uptime and Memory", sw);

            // Reset the colors
            colors = new(colorString.Split('-').Select(x => SKColor.Parse(x)));

            // 2. Disk.UsedGiB (where max <= 1TiB)
            LineChart smallDiskChart = new()
            {
                LabelOrientation = Orientation.Horizontal,
                LegendOption = SeriesLegendOption.Bottom,
                IsAnimated = false,

                MinValue = 0,
                MaxValue = 1024,
                YAxisMaxTicks = 33,

                ShowYAxisLines = true,
                ShowYAxisText = true,

                LineMode = LineMode.Straight,
                LineSize = 1,
                PointSize = 3,

                Series = smallDiskEntries.Select(x => new ChartSerie { Name = x.Key, Color = colors.Dequeue(), Entries = x.Value }).ToList()
            };
            SaveChart(smallDiskChart, "Disks smaller than 1TiB", sw);

            // Reset the colors
            colors = new(colorString.Split('-').Select(x => SKColor.Parse(x)));

            // 3. Disk.UsedTiB (where max > 1TiB)
            LineChart largeDiskChart = new()
            {
                LabelOrientation = Orientation.Vertical,
                LegendOption = SeriesLegendOption.Bottom,
                IsAnimated = false,

                MinValue = 0,
                MaxValue = diskMaxSize.Values.Max() * 1.1f / 1099511627776,
                YAxisMaxTicks = 31,

                ShowYAxisLines = true,
                ShowYAxisText = true,

                LineMode = LineMode.Straight,
                LineSize = 1,
                PointSize = 3,

                Series = largeDiskEntries.Select(x => new ChartSerie { Name = x.Key, Color = colors.Dequeue(), Entries = x.Value }).ToList()
            };
            SaveChart(largeDiskChart, "Disks larger than 1TiB", sw);
        }

        private static void SaveChart(LineChart lineChart, string title, Stopwatch sw)
        {
            SKBitmap bitmap = new(2560, 1440);
            SKCanvas canvas = new(bitmap);
            lineChart.Draw(canvas, bitmap.Width, bitmap.Height);

            // Add a chart title
            var titleText = SKTextBlob.Create(title, new SKFont());
            canvas.DrawText(titleText, (bitmap.Width / 2) - (titleText.Bounds.Width / 2), titleText.Bounds.Height * .75f, new SKPaint());

            // Add a generated footer
            var genText = SKTextBlob.Create($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}", new SKFont());
            canvas.DrawText(genText, bitmap.Width - genText.Bounds.Width, bitmap.Height - genText.Bounds.Height, new SKPaint());

            _ = canvas.Save();

            FileInfo chartFile = new(title + ".png");
            chartFile.Delete();

            using FileStream fs = chartFile.OpenWrite();
            using var image = SKImage.FromPixels(bitmap.PeekPixels());
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            data.SaveTo(fs);
            Console.WriteLine($"[{sw.Elapsed:mm\\:ss\\.ffff}] Wrote {chartFile.FullName}");
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
