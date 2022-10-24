using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using Unlimitedinf.Tom.Hashing;

namespace Unlimitedinf.Tom.Commands
{
    internal static class Hash
    {
        public static Command Create()
        {
            Command command = new("hash", "Hashes files based on the chosen hash algorithm.");

            Argument<Hasher.Algorithm> algorithmArg = new(
                "algorithm",
                () => Hasher.Algorithm.MD5,
                "The type of hash algorithm to use. Note that blockhash will only work for images and throw exceptions for everything else.");
            command.AddArgument(algorithmArg);

            Argument<string[]> pathsToHashArg = new(
                "paths",
                () => new[] { Environment.CurrentDirectory },
                "The file(s) or directory path(s) to hash. The default is Environment.CurrentDirectory.");
            command.AddArgument(pathsToHashArg);

            Option<bool> quietOpt = new("--quiet", "Do not display any extra information beyond the file hashes.");
            quietOpt.AddAlias("-q");
            command.AddOption(quietOpt);

            command.SetHandler(Handle, algorithmArg, pathsToHashArg, quietOpt);
            return command;
        }

        private static void Handle(Hasher.Algorithm algorithm, string[] pathsToHash, bool quiet)
        {
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
            // Then actually hash all of them
            Hasher hasher = new(algorithm);
            foreach (FileInfo fileInfo in fileInfos)
            {
                using FileStream fs = fileInfo.OpenRead();
                Console.WriteLine($"{hasher.ComputeHashS(fs).ToLower()} {fileInfo.FullName}");
                seenBytes += fileInfo.Length;

                // Emit progress as we go
                if (!quiet)
                {
                    Console.Write(seenBytes * 100 / totalBytes);
                    Console.Write('%');
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
            }
        }
    }
}
