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
            Argument<Rando.RandomType> randomTypeArgument = new("type")
            {
                Description = "The type of random to get. Pick from the supported values."
            };
            Command command = new("random", "Get random data.")
            {
                randomTypeArgument,
            };
            command.SetAction(parseResult =>
            {
                Rando.RandomType type = parseResult.GetRequiredValue(randomTypeArgument);
                Handle(type);
            });
            return command;
        }

        private static void Handle(Rando.RandomType type) => Console.WriteLine(Rando.GetString(type));
    }
}
