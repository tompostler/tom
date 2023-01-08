using System.CommandLine;
using System.Threading;
using Unlimitedinf.Utilities.Logging;

namespace Unlimitedinf.Tom.Commands.ZzTest
{
    internal sealed class ConsoleProgressLoggerCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-console-progress", "Test the ConsoleFileProgressLogger to validate functionality.")
            {
                IsHidden = true
            };
            command.SetHandler(Handle);
            return command;
        }

        private static void Handle()
        {
            // Simulate copying three files
            // Copy progress happens every 0.1s

            ConsoleFileProgressLogger progressLogger = new("File1.zip", 123_456, 3, 1_234_567_890);

            for (int i = 0; i < 123_456; i += 1_000)
            {
                progressLogger.AddProgress(1_000);
                Thread.Sleep(100);
            }

            progressLogger.ResetCurrentFile("a/much/longer/file/path/that/exceeds/the/length/limit.extension", 123_456_789);
            for (int i = 0; i < 123_456_789; i += 500_000)
            {
                progressLogger.AddProgress(500_000);
                Thread.Sleep(100);
            }

            progressLogger.ResetCurrentFile("short-name.txt", 1_234_567_890 - 123_456_789 - 123_456);
            for (int i = 0; i < 1_234_567_890 - 123_456_789 - 123_456; i += 10_000_000)
            {
                progressLogger.AddProgress(10_000_000);
                Thread.Sleep(100);
            }

            progressLogger.MarkComplete();
        }
    }
}
