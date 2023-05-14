using System.CommandLine;

namespace Unlimitedinf.Tom
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            await VersionProvider.TryReportIfUpdateIsRequiredAsync();

            RootCommand rootCommand = new("Various tools and utilities that I've needed or found useful.");

            rootCommand.AddCommand(Commands.ChartSystemStatsCommand.Create());
            rootCommand.AddCommand(Commands.ConvertCommand.Create());
            rootCommand.AddCommand(Commands.HashCommand.Create());
            rootCommand.AddCommand(Commands.HashRenameCommand.Create());
            rootCommand.AddCommand(Commands.ImageDimensionFilterCommand.Create());
            rootCommand.AddCommand(Commands.ImageDuplicateBlockhashCommand.Create());
            rootCommand.AddCommand(Commands.RandomCommand.Create());
            rootCommand.AddCommand(Commands.WebSocketClientCommand.Create());
            rootCommand.AddCommand(Commands.WebSocketServerCommand.Create());

            rootCommand.AddCommand(Commands.ZzTest.ChartLineCommand.Create());
            rootCommand.AddCommand(Commands.ZzTest.ConsoleColorsCommand.Create());
            rootCommand.AddCommand(Commands.ZzTest.ConsoleProgressLoggerCommand.Create());
            rootCommand.AddCommand(Commands.ZzTest.ConsoleWriteTableCommand.Create());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
