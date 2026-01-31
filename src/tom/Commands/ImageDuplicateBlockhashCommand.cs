using System.CommandLine;
using Unlimitedinf.Utilities.Hashing;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ImageDuplicateBlockhashCommand
    {
        public static Command Create()
        {
            Argument<int> confidenceArgument = new("confidence")
            {
                Description = "The confidence interval for blockhash similarity computed by hamming distance.",
                DefaultValueFactory = _ => 25,
            };
            Argument<DirectoryInfo[]> pathsToHashArgument = new Argument<DirectoryInfo[]>("paths")
            {
                Description = "The directory path(s) to hash. The default is Environment.CurrentDirectory.",
                DefaultValueFactory = _ => [new DirectoryInfo(Environment.CurrentDirectory)],
            }.AcceptExistingOnly();
            Option<bool> quietOption = new("--quiet", "-q")
            {
                Description = "Do not display any extra information beyond the file hashes."
            };
            Command command = new("blockhash", "Uses blockhash to compute the hamming distance between image files.")
            {
                confidenceArgument,
                pathsToHashArgument,
                quietOption,
            };
            command.SetAction(parseResult =>
            {
                int confidence = parseResult.GetRequiredValue(confidenceArgument);
                DirectoryInfo[] pathsToHash = parseResult.GetRequiredValue(pathsToHashArgument);
                bool quiet = parseResult.GetValue(quietOption);
                Handle(confidence, pathsToHash, quiet);
            });
            return command;
        }

        private static void Handle(int confidence, DirectoryInfo[] pathsToHash, bool quiet)
        {
            if (!quiet)
            {
                Console.WriteLine("ARGS:");
                Console.WriteLine($" Confidence:  {confidence}");
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
                Console.WriteLine($"There are {fileInfos.Count} files to blockhash, with {totalBytes / 1e6:0.00}MB of data.");
                Console.WriteLine();
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            // Then actually hash and rename all of them
            List<(byte[] Hash, FileInfo Info)> files = new();
            object filesLock = new();
            object consoleLock = new();
            long gcBytes = 100_000_000;
            _ = Parallel.ForEach(
                fileInfos,
                fileInfo =>
                {
                    _ = Interlocked.Add(ref seenBytes, fileInfo.Length);

                    // Compute the hash
                    byte[] hash = default;
                    using (FileStream fs = fileInfo.OpenRead())
                    {
                        try
                        {
                            hash = Blockhash.ComputeHash(fs);
                        }
                        catch (ArgumentException)
                        {
                            // Not an image type
                            return;
                        }
                    }

                    // Compare to all the existing ones
                    lock (filesLock)
                    {
                        for (int i = 0; i < files.Count; i++)
                        {
                            int hd = Blockhash.HammingDistance(files[i].Hash, hash);
                            if (hd < confidence)
                            {
                                int j = 1;
                                string targetFileName = files[i].Info.Name;
                                while (File.Exists(Path.Combine(files[i].Info.Directory.FullName, targetFileName)))
                                {
                                    targetFileName = $"{files[i].Info.Name.Substring(0, files[i].Info.Name.Length - files[i].Info.Extension.Length)}_DUPE-{j++:00}{fileInfo.Extension}";
                                }
                                string targetFilePath = Path.Combine(files[i].Info.Directory.FullName, targetFileName);
                                lock (consoleLock)
                                {
                                    Console.WriteLine($"{targetFilePath,-42} <- {fileInfo.FullName} (dist:{hd})");
                                }

                                fileInfo.MoveTo(targetFilePath);
                            }
                        }

                        files.Add((hash, fileInfo));
                    }

                    // Emit progress as we go
                    if (!quiet)
                    {
                        lock (consoleLock)
                        {
                            Console.Write(seenBytes * 100 / totalBytes);
                            Console.Write('%');
                            Console.SetCursorPosition(0, Console.CursorTop);
                        }
                    }

                    // Creating and discarding a bunch of images is expensive and the GC doesn't keep up... So give it a kick every 100MB.
                    if (gcBytes < seenBytes)
                    {
                        _ = Interlocked.Add(ref gcBytes, 100_000_000);
                        GC.Collect();
                    }
                });
        }
    }
}
