using System.Threading.Tasks;

namespace Unlimitedinf.Tom
{
    /// <summary>
    /// Implement this interface, once per DLL, to be the equivalent of "static void Main" for tom.
    /// </summary>
    public interface ITom
    {
        /// <summary>
        /// The name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A short description of the module to display in top-level helptext.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// True if the <see cref="RunAsync(string[])"/> method should be called.
        /// </summary>
        bool IsAsync { get; }

        /// <summary>
        /// Execute the module.
        /// </summary>
        /// <param name="args">The remaining command-line args after the module name.</param>
        Task RunAsync(string[] args);

        /// <summary>
        /// Execute the module.
        /// </summary>
        /// <param name="args">The remaining command-line args after the module name.</param>
        void Run(string[] args);
    }
}
