using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tools;

namespace Unlimitedinf.Tom.ImageBlockhashDelete
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "img-blockdel";

        /// <inheritdoc/>
        public string Description => "Delete images based on image blockhash.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            var options = ParseOptions(args);

            var fileInfos = new List<FileInfo>();
            foreach (var dirinfo in options.SourceDirs)
                fileInfos.AddRange(dirinfo.EnumerateFiles("*", SearchOption.AllDirectories));
            long totalBytes = fileInfos.Sum(_ => _.Length);
            long seenBytes = 0;
            long gcBytes = 100_000_000;
            Log.Inf($"There are {fileInfos.Count} files to img-blockdel, with {(totalBytes / 1e6).ToString("0.00")}MB of data to img-blockdel.");

            var files = new List<(byte[] Hash, FileInfo Info)>();
            object filesLock = new object();

            Parallel.ForEach(fileInfos, fileInfo =>
            {
                Interlocked.Add(ref seenBytes, fileInfo.Length);

                // Do the thing
                byte[] hash = null;
                using (FileStream fileStream = fileInfo.OpenRead())
                    try
                    {
                        hash = Tools.Hashing.Blockhash.ComputeHash(fileStream);
                    }
                    catch (ArgumentException)
                    {
                        // Not an image type
                        return;
                    }

                lock (filesLock)
                {
                    // Compute hamming distances between everything so far and the one we just looked at and print less than conf diff
                    for (int i = 0; i < files.Count; i++)
                    {
                        var hd = Tools.Hashing.Blockhash.HammingDistance(files[i].Hash, hash);
                        if (hd < options.Confidence)
                        {
                            var j = 1;
                            var newname = $"{files[i].Info.Directory.FullName}{Path.DirectorySeparatorChar}{files[i].Info.Name.Substring(0, files[i].Info.Name.LastIndexOf('.'))}_DUP{j++}{fileInfo.Extension}";
                            while (File.Exists(newname))
                                newname = $"{files[i].Info.Directory.FullName}{Path.DirectorySeparatorChar}{files[i].Info.Name.Substring(0, files[i].Info.Name.LastIndexOf('.'))}_DUP{j++}{fileInfo.Extension}";
                            lock (consolelock)
                                Console.WriteLine($"{newname.PadRight(newname.LastIndexOf(Path.DirectorySeparatorChar) + 32)} <- {fileInfo.FullName} (dist:{hd})");
                            fileInfo.MoveTo(newname);
                        }
                    }

                    files.Add((hash, fileInfo));
                }

                if (options.ShowProgress)
                    lock (consolelock)
                    {
                        Console.Write(seenBytes * 100 / totalBytes);
                        Console.Write('%');
                        Console.SetCursorPosition(0, Console.CursorTop);
                    }

                // Creating and chucking a bunch of images is expensive and the GC doesn't get it... So give it a kick every 100MB.
                if (gcBytes > seenBytes)
                {
                    Interlocked.Add(ref gcBytes, 100_000_000);
                    GC.Collect();
                }
            });
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();

        private static object consolelock = new object();

        private static void PrintHelp(OptionSet options)
        {
            Console.WriteLine($@"
tom.exe v{FileVersionInfo.GetVersionInfo(typeof(ITom).Assembly.Location).FileVersion} img-blockdel v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion}
Usage: tom.exe img-blockdel [OPTIONS]+ DIRS+

Based on the blockhash of an image, rename it to the matching file.

OPTIONS:
");
            options.WriteOptionDescriptions(Console.Out);
        }

        private class Options
        {
            public List<DirectoryInfo> SourceDirs { get; set; } = null;
            public bool ShowProgress { get; set; } = true;
            public byte Confidence { get; set; } = 25;

            /// <summary>
            /// True for good options. False for bad options.
            /// </summary>
            public bool ValidateOptions()
            {
                // This is required in order to work
                if (this.SourceDirs == null)
                    throw new ArgumentNullException("DIRS");

                return true;
            }
        }

        private static Options ParseOptions(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Log.ProgramName = "IMGBLOCKDEL";
            Log.ConfigureDefaultConsoleApp();

            var options = new Options();

            var optSet = new OptionSet
            {
                {
                    "c|confidence=",
                    "Blockhash distance for similarity. Defaults to 25",
                    (byte c) => options.Confidence = c
                },
                {
                    "p|hide-progress",
                    "Hides progress. By default, will scan ahead for all files, but does not check if they are images in advance. Due to the nature of outputting additional characters for status updates, this is primarily intended for human viewing.",
                    p => options.ShowProgress = false
                }
            };
            if (args.Length == 0 || args[0] == "--help")
            {
                PrintHelp(optSet);
                Environment.Exit(1);
            }

            try
            {
                var dirs = optSet.Parse(args);
                if (dirs.Count == 0)
                    throw new ArgumentNullException("DIRS");
                options.SourceDirs = dirs.Select(_ => new DirectoryInfo(_)).ToList();

                options.ValidateOptions();
            }
            catch (Exception e) when (e is OptionException || e is ArgumentNullException || e is ArgumentException)
            {
                Log.Err($"ARGS: {e.Message}");
                Log.Inf("Try `tom.exe img-blockdel --help` for more information.");
                Environment.Exit(1);
            }
            return options;
        }
    }
}
