using System.CommandLine;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ConvertCommand
    {
        public static Command Create()
        {
            Command rootCommand = new("convert", "Convert one thing to another.");

            Argument<long> sourceLongArgument = new("source")
            {
                Description = "The source value."
            };
            Argument<byte> targetBaseArgument = new("base")
            {
                Description = "BaseX, where X is [2,36]."
            };
            Command baseCommand = new("base", "Convert from base10 to baseX.")
            {
                sourceLongArgument,
                targetBaseArgument,
            };
            baseCommand.SetAction(parseResult =>
            {
                long source = parseResult.GetRequiredValue(sourceLongArgument);
                byte targetBase = parseResult.GetRequiredValue(targetBaseArgument);
                HandleBase(source, targetBase);
            });

            rootCommand.Subcommands.Add(baseCommand);
            return rootCommand;
        }

        private static void HandleBase(long source, byte @base) => Console.WriteLine(source.ToBaseX(@base));
    }
}
