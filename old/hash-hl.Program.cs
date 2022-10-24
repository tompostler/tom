using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unlimitedinf.Tools;
using Unlimitedinf.Tools.Hashing;
using Unlimitedinf.Tools.IO;

namespace Unlimitedinf.Tom.HashHardlink
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "hash-hl";

        /// <inheritdoc/>
        public string Description => "Will delete duplicate files based on their hash, and replace them with a hard link. Windows only.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            (var type, var showProgress, var pathsToHash) = ParseOptions(args);
            var hasher = new Hasher(type);

            var fileInfos = new List<FileInfo>();
            foreach (var pathToHash in pathsToHash)
                fileInfos.AddRange(Directory.EnumerateFiles(pathToHash, "*", SearchOption.AllDirectories).Select(_ => new FileInfo(_)));
            long totalBytes = fileInfos.Sum(_ => _.Length);
            long seenBytes = 0;
            long saveBytes = 0;
            int saveCount = 0;
            Log.Inf($"There are {fileInfos.Count} files to hash-hl, with {(totalBytes / 1e6).ToString("0.00")}MB of data to hash-hl.");

            var files = new Dictionary<string, FileInfo>(fileInfos.Count);
            foreach (var fileInfo in fileInfos)
            {
                seenBytes += fileInfo.Length;
                var hash = Hash(hasher, fileInfo);

                if (!files.ContainsKey(hash))
                {
                    // Haven't seen this hash before. Easy.
                    files.Add(hash, fileInfo);
                }
                else
                {
                    // Have seen this hash before...
                    var hardLinkPath = fileInfo.FullName;
                    saveCount++;
                    saveBytes += fileInfo.Length;
                    fileInfo.Delete();
                    MakeHardlink(files[hash].FullName, hardLinkPath);
                    Log.Inf($"{files[hash].FullName.PadRight(32)}  <==>  {hardLinkPath}");
                }

                if (showProgress)
                {
                    Console.Write(seenBytes * 100 / totalBytes);
                    Console.Write('%');
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
            }

            Log.Inf($"Hard linked {saveCount} files, saving {(saveBytes / 1e6).ToString("0.00")}MB for a {(1d * saveBytes / totalBytes * 100).ToString("0.0")}% reduction of space.");
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();

        private static string Hash(Hasher hasher, FileInfo info)
        {
            using (var stream = info.OpenRead())
                return hasher.ComputeHashS(stream).ToLowerInvariant();
        }

        private static void PrintHelp(OptionSet options)
        {
            Console.WriteLine($@"
tom.exe v{FileVersionInfo.GetVersionInfo(typeof(ITom).Assembly.Location).FileVersion} hash-hl v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion}
Usage: tom.exe hash-hl [OPTIONS]+ FILES+

Hashes things and then removes the duplicates and replaces them with a hard
link to save space. This obviously assumes that you want the files to be
linked and know that this means editing one will change the other.

OPTIONS:
");
            options.WriteOptionDescriptions(Console.Out);
        }

        private static (Hasher.Algorithm HashAlgorithm, bool ShowProgress, List<string> PathsToHash) ParseOptions(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Log.ProgramName = "HASHHL";
            Log.ConfigureDefaultConsoleApp();
            Log.PrintDateTime = false;

            Hasher.Algorithm type = Hasher.Algorithm.Crc32;
            var showProgress = true;
            var options = new OptionSet
            {
                {
                    "t|type=",
                    // Get the list of all the hash types, but hide blockhash because image-only hashing is not the goal here.
                    $"The type of hasing to use. Default is crc32. Available options: {string.Join(", ", Enum.GetNames(typeof(Hasher.Algorithm)).Select(_ => _.ToLowerInvariant()).Where(_ => !_.Equals("blockhash", StringComparison.OrdinalIgnoreCase)))}.",
                    (Hasher.Algorithm t) => type = t
                },
                {
                    "hide-progress",
                    "Hides progress. By default, this program will scan ahead for all files to be hashed and then print a percentage completion. Due to the nature of outputting additional characters for status updates, this is primarily intended for human viewing. Using this flag will turn that off.",
                    p => showProgress = false
                }
            };
            if (args.Length == 0)
            {
                PrintHelp(options);
                Environment.Exit(1);
            }

            List<string> pathsToHash = new List<string>();
            try
            {
                var optsToHash = options.Parse(args);
                Log.Ver($"ARGS: type     : {type.ToString()}");
                Log.Ver($"ARGS: progress : {showProgress.ToString()}");
                Log.Ver($"ARGS: files    : \"{string.Join("\", \"", optsToHash)}\"");
                pathsToHash = optsToHash.Select(_ => Path.GetFullPath(_)).ToList();
                Log.Ver($"ARGS: res-files: \"{string.Join("\", \"", pathsToHash)}\"");
            }
            catch (OptionException e)
            {
                Log.Err(e.Message);
                Log.Inf("Try `tom.exe hash-hl --help` for more information.");
                Environment.Exit(1);
            }
            return (type, showProgress, pathsToHash);
        }

        private static void MakeHardlink(string existingFilePath, string hardlinkFilePath)
        {
            try
            {
                if (!CreateHardLink(hardlinkFilePath, existingFilePath, IntPtr.Zero))
                {
                    Log.Err($"Create hard link failed for unknown reason ({hardlinkFilePath} -> {existingFilePath}). LastError={Marshal.GetLastWin32Error()}");
                    Environment.Exit(1);
                }
            }
            catch (DllNotFoundException)
            {
                Log.Err($"{nameof(DllNotFoundException)}: Perhaps not on Windows?");
                Environment.Exit(1);
            }
            catch (EntryPointNotFoundException)
            {
                Log.Err($"{nameof(EntryPointNotFoundException)}: Not sure what causes this...");
                Environment.Exit(1);
            }
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
    }
}
