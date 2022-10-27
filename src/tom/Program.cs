using System.CommandLine;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            await VersionProvider.TryReportIfUpdateIsRequiredAsync();

            RootCommand rootCommand = new("Various tools and utilities that I've needed or found useful.");

            rootCommand.AddCommand(Commands.ImageDimensionFilter.Create());
            rootCommand.AddCommand(Commands.ImageDuplicateBlockhash.Create());
            rootCommand.AddCommand(Commands.Hash.Create());
            rootCommand.AddCommand(Commands.HashRename.Create());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
