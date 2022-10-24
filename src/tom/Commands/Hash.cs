using System.CommandLine;
using System.Threading.Tasks;
using Unlimitedinf.Tom.Hashing;

namespace Unlimitedinf.Tom.Commands
{
    internal static class Hash
    {
        public static Command Create()
        {
            Command command = new("hash", "Hashes files based on the chosen hash algorithm.");

            Argument<Hasher.Algorithm> algorithmArg = new("algorithm", "The type of hash algorithm to use. Note that blockhash will only work for images and throw exceptions for everything else.");
            command.AddArgument(algorithmArg);

            Argument<string[]> pathsToHash = new("paths", "The file(s) or directory path(s) to hash.") { };

            command.SetHandler(HandleAsync, algorithmArg);
            return command;
        }

        private static async Task HandleAsync(Hasher.Algorithm algorithm)
        {

        }
    }
}
