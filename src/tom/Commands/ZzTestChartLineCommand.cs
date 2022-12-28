using Microcharts;
using SkiaSharp;
using System;
using System.CommandLine;
using System.IO;

namespace Unlimitedinf.Tom.Commands
{
    internal sealed class ZzTestChartLineCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-chart-line", "Test generating a Microcharts line chart and saving it as an image headlessly.");

            command.SetHandler(Handle);
            return command;
        }

        public static void Handle()
        {
            LineChart lineChart = new()
            {
                LineMode = LineMode.Straight,
                Series = new[]
                {
                    new ChartSerie
                    {
                        Name = "PrimeNumbers",
                        Entries = new[]
                        {
                            new ChartEntry(2)
                            {
                                ValueLabel = "01"
                            },
                            new ChartEntry(3)
                            {
                                ValueLabel = "02"
                            },
                            new ChartEntry(5)
                            {
                                ValueLabel = "03"
                            },
                            new ChartEntry(7)
                            {
                                ValueLabel = "04"
                            },
                            new ChartEntry(11)
                            {
                                ValueLabel = "05"
                            },
                            new ChartEntry(13)
                            {
                                ValueLabel = "06"
                            },
                            new ChartEntry(17)
                            {
                                ValueLabel = "07"
                            },
                            new ChartEntry(19)
                            {
                                ValueLabel = "08"
                            },
                            new ChartEntry(23)
                            {
                                ValueLabel = "09"
                            },
                            new ChartEntry(29)
                            {
                                ValueLabel = "10"
                            },
                        }
                    },
                    new ChartSerie
                    {
                        Name = "CompositeNumbers",
                        Entries = new[]
                        {
                            new ChartEntry(4)
                            {
                                ValueLabel = "01"
                            },
                            new ChartEntry(6)
                            {
                                ValueLabel = "02"
                            },
                            new ChartEntry(9)
                            {
                                ValueLabel = "03"
                            },
                            new ChartEntry(10)
                            {
                                ValueLabel = "04"
                            },
                            new ChartEntry(12)
                            {
                                ValueLabel = "05"
                            },
                            new ChartEntry(14)
                            {
                                ValueLabel = "06"
                            },
                            new ChartEntry(15)
                            {
                                ValueLabel = "07"
                            },
                            new ChartEntry(16)
                            {
                                ValueLabel = "08"
                            },
                            new ChartEntry(18)
                            {
                                ValueLabel = "09"
                            },
                            new ChartEntry(20)
                            {
                                ValueLabel = "10"
                            },
                            new ChartEntry(21)
                            {
                                ValueLabel = "11"
                            },
                            new ChartEntry(22)
                            {
                                ValueLabel = "12"
                            },
                        }
                    },
                }
            };

            SKBitmap bitmap = new(1920, 1080);
            bitmap.Erase(SKColor.Empty);
            SKCanvas canvas = new(bitmap);
            lineChart.DrawContent(canvas, 1920, 1080);
            _ = canvas.Save();

            File.Delete("t.png");
            using FileStream fs = File.Create("t.png");
            using var image = SKImage.FromPixels(bitmap.PeekPixels());
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            {
                data.SaveTo(fs);
            }
        }
    }
}
