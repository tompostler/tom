using System;
using System.CommandLine;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ConvertCommand
    {
        public static Command Create()
        {
            Command rootCommand = new("convert", "Convert one thing to another.");

            Argument<long> sourceLongArg = new(
                "source",
                "The source value.");


            Command baseCommand = new("base", "Convert from base10 to baseX.");
            baseCommand.AddArgument(sourceLongArg);

            Argument<byte> targetBaseArg = new(
                "base",
                "BaseX, where X is [2,36].");
            baseCommand.AddArgument(targetBaseArg);

            baseCommand.SetHandler(HandleBase, sourceLongArg, targetBaseArg);


            rootCommand.AddCommand(baseCommand);
            return rootCommand;
        }

        private static void HandleBase(long source, byte @base) => Console.WriteLine(source.ToBaseX(@base));
    }
}
