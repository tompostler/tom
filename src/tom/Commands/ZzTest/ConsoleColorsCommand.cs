using System.CommandLine;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal static class ConsoleColorsCommand
    {
        public static Command Create()
        {
            Option<ConsoleColor> backgroundColorOption = new("--background-color")
            {
                Description = "The background color to display against.",
                DefaultValueFactory = _ => Console.BackgroundColor
            };

            Command command = new("zz-test-console-colors", "Test the output of the ConsoleColor enum to see what the terminal supports.")
            {
                backgroundColorOption,
            };
            command.Hidden = true;

            command.SetAction(parseResult =>
            {
                ConsoleColor backgroundColor = parseResult.GetValue(backgroundColorOption);
                Handle(backgroundColor);
            });

            return command;
        }

        public static void Handle(ConsoleColor backgroundColor)
        {
            ConsoleColor currentForeground = Console.ForegroundColor;
            ConsoleColor currentBackground = Console.BackgroundColor;

            Console.BackgroundColor = backgroundColor;
            foreach (ConsoleColor foregroundColor in Enum.GetValues<ConsoleColor>())
            {
                Console.ForegroundColor = foregroundColor;
                Console.WriteLine($"Foreground: {foregroundColor}, Background: {backgroundColor}");
            }

            Console.ForegroundColor = currentForeground;
            Console.BackgroundColor = currentBackground;

            Console.WriteLine();
        }
    }
}
