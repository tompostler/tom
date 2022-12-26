using System;
using System.CommandLine;

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
            Console.WriteLine("Not yet implemented.");
            Console.WriteLine();
        }
    }
}
