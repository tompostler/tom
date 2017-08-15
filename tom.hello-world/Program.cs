using System;
using System.Threading.Tasks;
using Unlimitedinf.Tools;

namespace Unlimitedinf.Tom.HelloWorld
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "hello-world";

        /// <inheritdoc/>
        public string Description => "The standard Hello World program with some date awareness using the Unlimitedinf.Tools logger.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            Log.ProgramName = "HELWRLD";
            Log.Inf($"Hello world on this fine {DateTime.Now.DayOfWeek.ToString()}!");
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();
    }
}
