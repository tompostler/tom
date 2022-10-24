using System.CommandLine;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            RootCommand rootCommand = new("Various tools and utilities that I've needed or found useful.");
            Option<Verbosity> verbosityOption = new("--verbosity", () => Verbosity.Warn, "Verbosity level for logging.");
            verbosityOption.AddAlias("-v");
            rootCommand.AddGlobalOption(verbosityOption);

            return await rootCommand.InvokeAsync(args);
        }
    }
}