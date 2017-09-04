using System;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Hash
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "hash";

        /// <inheritdoc/>
        public string Description => "Wraps the hashing components of Unlimitedinf.Tools for ease of use.";

        /// <inheritdoc/>
        public bool IsAsync => true;

        /// <inheritdoc/>
        public void Run(string[] args) => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();
    }
}
