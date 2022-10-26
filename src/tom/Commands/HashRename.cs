using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using Unlimitedinf.Tom.Hashing;

namespace Unlimitedinf.Tom.Commands
{
    internal static class HashRename
    {
        public static Command Create()
        {
            Command command = new("hash-rename", "Hashes files based on the chosen hash algorithm, and then renames the files to their hash.");
            
            Argument<string> algorithmArg = new Argument<string>(
                "algorithm",
                () => Hasher.Algorithm.MD5.ToString().ToLower(),
                "The type of hash algorithm to use.")
                .FromAmong(Enum.GetValues<Hasher.Algorithm>().Where(x => x != Hasher.Algorithm.Blockhash).Select(x => x.ToString().ToLower()).ToArray());
            command.AddArgument(algorithmArg);

            Argument<string[]> pathsToHashArg = new(
                "paths",
                () => new[] { Environment.CurrentDirectory },
                "The file(s) or directory path(s) to hash. The default is Environment.CurrentDirectory.");
            command.AddArgument(pathsToHashArg);

            Option<bool> quietOpt = new("--quiet", "Do not display any extra information beyond the renamed files.");
            quietOpt.AddAlias("-q");
            command.AddOption(quietOpt);

            command.SetHandler(Handle, algorithmArg, pathsToHashArg, quietOpt);
            return command;
        }

        private static void Handle(string algorithmStr, string[] pathsToHash, bool quiet)
        {
            Hasher.Algorithm algorithm = Enum.Parse<Hasher.Algorithm>(algorithmStr, ignoreCase: true);
            pathsToHash = pathsToHash.Select(x => Path.GetFullPath(x)).ToArray();
            if (!quiet)
            {
                Console.WriteLine("ARGS:");
                Console.WriteLine($" Algorithm:   {algorithm}");
                Console.WriteLine($" PathsToHash: {string.Join(", ", pathsToHash)}");
                Console.WriteLine();
            }

            // Build up the list of what to hash, so that we can first display some summary information
            List<FileInfo> fileInfos = new();
            foreach (string pathToHash in pathsToHash)
            {
                if (File.GetAttributes(pathToHash).HasFlag(FileAttributes.Directory))
                {
                    // Need to walk child directories
                    foreach (string filePath in Directory.EnumerateFiles(pathToHash, "*", new EnumerationOptions { RecurseSubdirectories = true }))
                    {
                        fileInfos.Add(new(filePath));
                    }
                }
                else
                {
                    // It's just one file, so do that
                    fileInfos.Add(new(pathToHash));
                }
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
                using FileStream fs = fileInfo.OpenRead();
                string hash = hasher.ComputeHashS(fs).ToLower();
                fs.Dispose();
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
                    targetFileName = $"{hash}_{i++:00}{fileInfo.Extension}";
                }

                // Log, and move it
                Console.WriteLine($"{targetFileName.PadRight(hash.Length + 10)} <- {fileInfo.FullName}");
                fileInfo.MoveTo(Path.Combine(fileInfo.Directory.FullName, targetFileName));
            }
        }
    }
}
