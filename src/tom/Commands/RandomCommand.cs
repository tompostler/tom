using System.CommandLine;
using Unlimitedinf.Utilities;

namespace Unlimitedinf.Tom.Commands
{
    /// <remarks>
    /// Most of this is sourced from number-sequence\src\number-sequence\Controllers\RandomController.cs
    /// </remarks>
    internal static class RandomCommand
    {
        public static Command Create()
        {
            Command command = new("random", "Get random data.");

            Argument<Rando.RandomType> randomTypeArg = new(
                "type",
                "The type of random to get. Pick from the supported values.");
            command.AddArgument(randomTypeArg);

            command.SetHandler(Handle, randomTypeArg);
            return command;
        }

        private static void Handle(Rando.RandomType type) => Console.WriteLine(Rando.GetString(type));
    }
}
