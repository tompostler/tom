using ScottPlot;
using ScottPlot.Plottables;
using System.CommandLine;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal static class ChartLineCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-chart-line", "Test generating a ScottPlot line chart and saving it as an image headlessly.")
            {
                Hidden = true
            };
            command.SetAction(_ => Handle());
            return command;
        }

        public static void Handle()
        {
            Plot plot = new();

            double[] primeXs = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
            double[] primeYs = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29];
            Scatter primeSeries = plot.Add.Scatter(primeXs, primeYs);
            primeSeries.LegendText = "PrimeNumbers";

            double[] compositeXs = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
            double[] compositeYs = [4, 6, 9, 10, 12, 14, 15, 16, 18, 20, 21, 22];
            Scatter compositeSeries = plot.Add.Scatter(compositeXs, compositeYs);
            compositeSeries.LegendText = "CompositeNumbers";

            double[] tickPositions = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
            string[] tickLabels = ["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"];
            plot.Axes.Bottom.SetTicks(tickPositions, tickLabels);

            plot.Title("Sample chart title");
            _ = plot.ShowLegend(Edge.Bottom);
            plot.Axes.TightMargins();
            _ = plot.Add.Annotation($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}", Alignment.UpperRight);

            File.Delete("t.png");
            _ = plot.SavePng("t.png", 1920, 1080);
        }
    }
}
