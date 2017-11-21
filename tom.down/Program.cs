using System;
using System.Threading.Tasks;
using Unlimitedinf.Tools;

namespace Unlimitedinf.Tom.Down
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "down";

        /// <inheritdoc/>
        public string Description => "Downloads files from the internet.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            Log.ProgramName = "DOWN";
            Log.ConfigureDefaultConsoleApp();
            Log.Inf($"Hello world on this fine {DateTime.Now.DayOfWeek.ToString()}!");
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();
    }
}
