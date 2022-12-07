using System;
using System.CommandLine;
using System.Threading;

namespace Unlimitedinf.Tom.Commands
{
    internal sealed class ZzTestSpectreConsoleProgressLoggerCommand
    {
        public static Command Create()
        {
            Command command = new("zz-test-spectre-console-progress", "Test the SpectreConsoleFileTransferProgressLogger to validate functionality.");
            command.SetHandler(Handle);
            return command;
        }

        private static void Handle()
        {
            // Simulate copying three files
            // Copy progress happens every 0.1s

            SpectreConsoleFileTransferProgressLogger progressLogger = new("File1.zip", 123_456, 3, 1_234_567_890);

            for (int i = 0; i < 123_456; i += 1_000)
            {
                progressLogger.AddProgress(1_000);
                Thread.Sleep(100);
            }

            progressLogger.ResetCurrentFile("a/much/longer/file/path.extension", 123_456_789);
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
