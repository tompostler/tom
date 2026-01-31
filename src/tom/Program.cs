using System.CommandLine;

namespace Unlimitedinf.Tom
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            await VersionProvider.TryReportIfUpdateIsRequiredAsync();

            RootCommand rootCommand = new("Various tools and utilities that I've needed or found useful.")
            {
                Commands.ChartSystemStatsCommand.Create(),
                Commands.ConvertCommand.Create(),
                Commands.HashCommand.Create(),
                Commands.HashRenameCommand.Create(),
                Commands.ImageDimensionFilterCommand.Create(),
                Commands.ImageDuplicateBlockhashCommand.Create(),
                Commands.RandomCommand.Create(),
                Commands.WebSocketClientCommand.Create(),
                Commands.WebSocketServerCommand.Create(),

                Commands.ZzTest.ChartLineCommand.Create(),
                Commands.ZzTest.ConsoleColorsCommand.Create(),
                Commands.ZzTest.ConsoleProgressLoggerCommand.Create(),
                Commands.ZzTest.ConsoleWriteTableCommand.Create(),
            };

            return await rootCommand.Parse(args).InvokeAsync();
        }
    }
}
