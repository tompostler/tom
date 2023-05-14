using System.CommandLine;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal static class ConsoleColorsCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-console-colors", "Test the output of the ConsoleColor enum to see what the terminal supports.")
            {
                IsHidden = true
            };

            Option<ConsoleColor> backgroundColorOpt = new(
                "--background-color",
                () => Console.BackgroundColor,
                "The background color to display against.");
            command.AddOption(backgroundColorOpt);

            command.SetHandler(Handle, backgroundColorOpt);
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
