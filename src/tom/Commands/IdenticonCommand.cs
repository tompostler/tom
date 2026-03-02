using System.CommandLine;
using Unlimitedinf.Utilities;

namespace Unlimitedinf.Tom.Commands
{
    internal static class IdenticonCommand
    {
        public static Command Create()
        {
            Argument<string> seedArgument = new("seed")
            {
                Description = "The input string (e.g. an email address) to generate the identicon from.",
            };
            Argument<FileInfo> outputArgument = new("output")
            {
                Description = "The output file path. Extension determines format: .svg or .png.",
            };
            Option<int> sizeOption = new("--size", "-s")
            {
                Description = "Output image size in pixels (square).",
                DefaultValueFactory = _ => 64,
            };
            Option<int> blocksOption = new("--blocks", "-b")
            {
                Description = "Number of cells along each side of the grid.",
                DefaultValueFactory = _ => 4,
            };
            Option<bool> grayscaleOption = new("--grayscale", "-g")
            {
                Description = "Use only the red channel value for all three channels.",
            };
            Command command = new("identicon", "Generates a deterministic geometric identicon image from a string seed.")
            {
                seedArgument,
                outputArgument,
                sizeOption,
                blocksOption,
                grayscaleOption,
            };
            command.SetAction(parseResult =>
            {
                string seed = parseResult.GetRequiredValue(seedArgument);
                FileInfo output = parseResult.GetRequiredValue(outputArgument);
                int size = parseResult.GetValue(sizeOption);
                int blocks = parseResult.GetValue(blocksOption);
                bool grayscale = parseResult.GetValue(grayscaleOption);
                Handle(seed, output, size, blocks, grayscale);
            });
            return command;
        }

        private static void Handle(string seed, FileInfo output, int size, int blocks, bool grayscale)
        {
            IdenticonGenerator generator = new()
            {
                BlockCount = blocks,
                Grayscale = grayscale,
            };

            if (output.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(output.FullName, generator.GeneratePng(seed, size));
            }
            else
            {
                File.WriteAllText(output.FullName, generator.GenerateSvg(seed, size));
            }

            Console.WriteLine(output.FullName);
        }
    }
}
