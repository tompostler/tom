using System.CommandLine;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            RootCommand rootCommand = new("Various tools and utilities that I've needed or found useful.");

            rootCommand.AddCommand(Commands.Hash.Create());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
