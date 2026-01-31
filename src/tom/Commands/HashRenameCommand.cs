using System.CommandLine;
using Unlimitedinf.Utilities.Hashing;

namespace Unlimitedinf.Tom.Commands
{
    internal static class HashRenameCommand
    {
        public static Command Create()
        {
            Argument<Hasher.Algorithm> algorithmArgument = new Argument<Hasher.Algorithm>("algorithm")
            {
                Description = "The type of hash algorithm to use.",
                DefaultValueFactory = _ => Hasher.Algorithm.MD5,
            }.AcceptOnlyFromAmong(Enum.GetValues<Hasher.Algorithm>().Where(x => x != Hasher.Algorithm.Blockhash).Select(x => x.ToString().ToLower()).ToArray());
            Argument<DirectoryInfo[]> pathsToHashArgument = new Argument<DirectoryInfo[]>("paths")
            {
                Description = "The directory path(s) to hash. The default is Environment.CurrentDirectory.",
                DefaultValueFactory = _ => [new DirectoryInfo(Environment.CurrentDirectory)],
            }.AcceptExistingOnly();
            Option<bool> quietOption = new("--quiet", "-q")
            {
                Description = "Do not display any extra information beyond the file hashes."
            };
            Command command = new("hash-rename", "Hashes files based on the chosen hash algorithm, and then renames the files to their hash.")
            {
                algorithmArgument,
                pathsToHashArgument,
                quietOption,
            };
            command.SetAction(parseResult =>
            {
                Hasher.Algorithm algorithm = parseResult.GetRequiredValue(algorithmArgument);
                DirectoryInfo[] pathsToHash = parseResult.GetRequiredValue(pathsToHashArgument);
                bool quiet = parseResult.GetValue(quietOption);
                Handle(algorithm, pathsToHash, quiet);
            });
            return command;
        }

        private static void Handle(Hasher.Algorithm algorithm, DirectoryInfo[] pathsToHash, bool quiet)
        {
            if (!quiet)
            {
                Console.WriteLine("ARGS:");
                Console.WriteLine($" Algorithm:   {algorithm}");
                Console.WriteLine($" PathsToHash: {string.Join(", ", pathsToHash.Select(x => x.FullName))}");
                Console.WriteLine();
            }

            // Build up the list of what to hash, so that we can first display some summary information
            List<FileInfo> fileInfos = new();
            foreach (DirectoryInfo pathToHash in pathsToHash)
            {
                fileInfos.AddRange(pathToHash.GetFiles("*", SearchOption.AllDirectories));
            }

            // Let know how much to do
            long totalBytes = fileInfos.Sum(x => x.Length);
            long seenBytes = 0;
            if (!quiet)
            {
                Console.WriteLine($"There are {fileInfos.Count} files to hash, with {totalBytes / 1e6:0.00}MB of data.");
                Console.WriteLine();
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            // Then actually hash and rename all of them
            Hasher hasher = new(algorithm);
            foreach (FileInfo fileInfo in fileInfos)
            {
                // Compute the hash and the target file name
                string hash = default;
                using (FileStream fs = fileInfo.OpenRead())
                {
                    hash = hasher.ComputeHashS(fs).ToLower();
                }
                string targetFileName = hash + fileInfo.Extension;
                seenBytes += fileInfo.Length;

                // Emit progress as we go
                if (!quiet)
                {
                    Console.Write(seenBytes * 100 / totalBytes);
                    Console.Write('%');
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                // If the target name is equal to the current file name, then it's already the right name
                if (string.Equals(fileInfo.Name, targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Iterate through file names looking for a new one if the new one would collide
                int i = 1;
                while (File.Exists(Path.Combine(fileInfo.Directory.FullName, targetFileName)))
                {
                    targetFileName = $"{hash}_DUPE-{i++:00}{fileInfo.Extension}";
                }

                // Log, and move it
                string targetFilePath = Path.Combine(fileInfo.Directory.FullName, targetFileName);
                Console.WriteLine($"{targetFilePath,-42} <- {fileInfo.FullName}");
                fileInfo.MoveTo(targetFilePath);
            }
        }
    }
}
