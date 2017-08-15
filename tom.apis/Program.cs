using System;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Apis
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "apis";

        /// <inheritdoc/>
        public string Description => "A way to talk with my online apis. Makes use of the Unlimitedinf.Apis.Client executable.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            Unlimitedinf.Apis.Client.Program.App.Main(args);
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();
    }
}
