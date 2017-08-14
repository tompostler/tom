using System;
using System.Threading.Tasks;

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
        public string Description => "The standard Hello World program with some date awareness.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            Console.WriteLine($"Hello world on this fine {DateTime.Now.DayOfWeek.ToString()}!");
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();
    }
}
