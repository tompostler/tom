using System.CommandLine;

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

        }
    }
}
