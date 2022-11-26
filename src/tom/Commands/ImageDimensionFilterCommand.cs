using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unlimitedinf.Tom.Commands
{
    internal static class ImageDimensionFilterCommand
    {
        public static Command Create()
        {
            Command command = new(
                "img-dimension",
                "Uses image dimensions to filter low-quality images into a separate folder."
                + "\nFiltered images will be placed in a 'toss' directory in the directy of the item(s) to inspect."
                + "\nIf width and height are requested, then megapixels is ignored. Otherwise each filter will apply individually.");

            Option<int> widthOpt = new(
                "--width",
                () => 0,
                "The width of the image.");
            widthOpt.AddAlias("-w");
            command.AddOption(widthOpt);

            Option<int> heightOpt = new(
                "--height",
                () => 0,
                "The height of the image.");
            heightOpt.AddAlias("-h");
            command.AddOption(heightOpt);

            Option<double> megapixelOpt = new(
                "--megapixels",
                () => 0.5,
                "The area of the image. Default of 0.5mp is equivalent to an approx 700x700 square image.");
            megapixelOpt.AddAlias("-m");
            command.AddOption(megapixelOpt);

            Argument<string[]> pathsToHashArg = new(
                "paths",
                () => new[] { Environment.CurrentDirectory },
                "The file(s) or directory path(s) to measure. The default is Environment.CurrentDirectory.");
            command.AddArgument(pathsToHashArg);

            Option<bool> quietOpt = new("--quiet", "Do not display any extra information beyond the moved files.");
            quietOpt.AddAlias("-q");
            command.AddOption(quietOpt);

            command.SetHandler(Handle, widthOpt, heightOpt, megapixelOpt, pathsToHashArg, quietOpt);
            return command;
        }

        private static void Handle(int width, int height, double megapixels, string[] pathsToHash, bool quiet)
        {
            pathsToHash = pathsToHash.Select(x => Path.GetFullPath(x)).ToArray();
            if (!quiet)
            {
                Console.WriteLine("ARGS:");
                Console.WriteLine($" Width:       {width}");
                Console.WriteLine($" Height:      {height}");
                Console.WriteLine($" Megapixels:  {megapixels}");
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

            int maxFileNameLength = fileInfos.Max(x => x.FullName.Length);

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

#pragma warning disable CA1416 // Validate platform compatibility

                    // Check the dimensions
                    bool keep = true;
                    string reason = default;
                    try
                    {
                        using (var image = Image.FromFile(fileInfo.FullName))
                        {
                            // If width and height are defined, use those
                            if (width > 0 && height > 0)
                            {
                                if (image.Width < width || image.Height < height)
                                {
                                    keep = false;
                                    reason = $"width ({image.Width}) or height ({image.Height}) was less than requested: {width}x{height}";
                                }
                            }

                            // Then check just width, if defined
                            else if (width > 0 && image.Width < width)
                            {
                                keep = false;
                                reason = $"width ({image.Width}) was less than requested: {width}";
                            }

                            // Then check just height, if defined
                            else if (height > 0 && image.Height < height)
                            {
                                keep = false;
                                reason = $"height ({image.Height}) was less than requested: {height}";
                            }

                            // Then check just megapixels, if defined
                            else if (megapixels > 0 && image.GetMegapixels() < megapixels)
                            {
                                keep = false;
                                reason = $"megapixels ({image.GetMegapixels():0.0}) was less than requested: {megapixels:0.0}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{fileInfo.FullName.PadRight(maxFileNameLength)} {ex.Message}");
                    }

#pragma warning restore CA1416 // Validate platform compatibility

                    if (!keep)
                    {
                        string tossDirPath = Path.Combine(fileInfo.Directory.FullName, "toss");
                        _ = Directory.CreateDirectory(tossDirPath);

                        lock (consoleLock)
                        {
                            Console.WriteLine($"{fileInfo.FullName.PadRight(maxFileNameLength)} {reason}");
                        }

                        fileInfo.MoveTo(Path.Combine(tossDirPath, fileInfo.Name));
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
