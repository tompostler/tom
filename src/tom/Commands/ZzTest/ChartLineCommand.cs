using Microcharts;
using SkiaSharp;
using System.CommandLine;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal static class ChartLineCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-chart-line", "Test generating a Microcharts line chart and saving it as an image headlessly.")
            {
                Hidden = true
            };
            command.SetAction(_ => Handle());
            return command;
        }

        public static void Handle()
        {
            LineChart lineChart = new()
            {
                LabelOrientation = Orientation.Horizontal,
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
                        Name = "PrimeNumbers",
                        Color = SKColor.Parse("#005f73"),
                        Entries = new[]
                        {
                            new ChartEntry(2)
                            {
                                Label = "01"
                            },
                            new ChartEntry(3)
                            {
                                Label = "02"
                            },
                            new ChartEntry(5)
                            {
                                Label = "03"
                            },
                            new ChartEntry(7)
                            {
                                Label = "04"
                            },
                            new ChartEntry(11)
                            {
                                Label = "05"
                            },
                            new ChartEntry(13)
                            {
                                Label = "06"
                            },
                            new ChartEntry(17)
                            {
                                Label = "07"
                            },
                            new ChartEntry(19)
                            {
                                Label = "08"
                            },
                            new ChartEntry(23)
                            {
                                Label = "09"
                            },
                            new ChartEntry(29)
                            {
                                Label = "10"
                            },
                        }
                    },
                    new ChartSerie
                    {
                        Name = "CompositeNumbers",
                        Color = SKColor.Parse("#ae2012"),
                        Entries = new[]
                        {
                            new ChartEntry(4)
                            {
                                Label = "01"
                            },
                            new ChartEntry(6)
                            {
                                Label = "02"
                            },
                            new ChartEntry(9)
                            {
                                Label = "03"
                            },
                            new ChartEntry(10)
                            {
                                Label = "04"
                            },
                            new ChartEntry(12)
                            {
                                Label = "05"
                            },
                            new ChartEntry(14)
                            {
                                Label = "06"
                            },
                            new ChartEntry(15)
                            {
                                Label = "07"
                            },
                            new ChartEntry(16)
                            {
                                Label = "08"
                            },
                            new ChartEntry(18)
                            {
                                Label = "09"
                            },
                            new ChartEntry(20)
                            {
                                Label = "10"
                            },
                            new ChartEntry(21)
                            {
                                Label = "11"
                            },
                            new ChartEntry(22)
                            {
                                Label = "12"
                            },
                        }
                    },
                }
            };

            SKBitmap bitmap = new(1920, 1080);
            SKCanvas canvas = new(bitmap);
            lineChart.Draw(canvas, bitmap.Width, bitmap.Height);

            // Add a chart title
            var title = SKTextBlob.Create("Sample chart title", new SKFont());
            canvas.DrawText(title, (bitmap.Width / 2) - (title.Bounds.Width / 2), title.Bounds.Height * .75f, new SKPaint());

            // Add a generated footer
            var genText = SKTextBlob.Create($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}", new SKFont());
            canvas.DrawText(genText, bitmap.Width - genText.Bounds.Width, bitmap.Height - genText.Bounds.Height, new SKPaint());

            _ = canvas.Save();

            File.Delete("t.png");

            using FileStream fs = File.Create("t.png");
            using var image = SKImage.FromPixels(bitmap.PeekPixels());
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            data.SaveTo(fs);
        }
    }
}
