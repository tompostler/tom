using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unlimitedinf.Tools;
using Unlimitedinf.Tools.Hashing;
using Unlimitedinf.Tools.IO;

namespace Unlimitedinf.Tom.HashRename
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "hash-rename";

        /// <inheritdoc/>
        public string Description => "In addition to hashing files, renames them to their hash.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            (var type, var showProgress, var pathsToHash) = ParseOptions(args);
            var hasher = new Hasher(type);

            // Because not showing progress is so easy, that logic is left entirely separate
            if (showProgress)
            {
                var fileInfos = new List<FileInfo>();
                fileInfos.AddRange(pathsToHash.Select(_ => new FileInfo(_)).Where(_ => _.Exists));
                foreach (var pathToHash in pathsToHash)
                    fileInfos.AddRange(new FileSystemCollection(pathToHash).Where(_ => !File.GetAttributes(_).HasFlag(FileAttributes.Directory)).Select(_ => new FileInfo(_)));
                long totalBytes = fileInfos.Sum(_ => _.Length);
                long seenBytes = 0;
                Log.Inf($"There are {fileInfos.Count} files to hash-rename, with {(totalBytes / 1e6).ToString("0.00")}MB of data to hash-rename.");

                foreach (var fileInfo in fileInfos)
                {
                    HashRename(hasher, fileInfo.FullName);
                    seenBytes += fileInfo.Length;

                    Console.Write(seenBytes * 100 / totalBytes);
                    Console.Write('%');
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
            }
            else
                foreach (var pathToHash in pathsToHash)
                    foreach (var info in new FileSystemCollection(pathToHash).Where(_ => !File.GetAttributes(_).HasFlag(FileAttributes.Directory)).Select(_ => new FileInfo(_)))
                        HashRename(hasher, info.FullName);
        }

        /// <inheritdoc/>
        public Task RunAsync(string[] args) => throw new NotImplementedException();

        private static void HashRename(Hasher hasher, string path)
        {
            try
            {
                var info = new FileInfo(path);
                string hash;
                using (var stream = info.OpenRead())
                    hash = hasher.ComputeHashS(stream);

                // Short cicuit if we're the file already
                if (hash == info.Name.Substring(0, info.Name.Length - info.Extension.Length))
                    return;

                // Find a non-colliding name
                var i = 0;
                var name = Path.Combine(info.Directory.FullName, $"{hash}{info.Extension}");
                while (File.Exists(name))
                    name = Path.Combine(info.Directory.FullName, $"{hash}_DUP{i++}{info.Extension}");

                Log.Inf($"{hash}{(i == 0 ? "     " : $"_DUP{i}")} <-   {path}", printProgramName: false);
                info.MoveTo(name);
            }
            catch (IOException e)
            {
                Log.Err($"{path}: {e.Message}");
            }
        }

        private static void PrintHelp(OptionSet options)
        {
            Console.WriteLine($@"
tom.exe v{FileVersionInfo.GetVersionInfo(typeof(ITom).Assembly.Location).FileVersion} hash-rename v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion}
Usage: tom.exe hash-rename [OPTIONS]+ FILES+

Hashes things and then renames them into their hash. Outputs only when the hash
has changed. Duplicate files are by default appended with _DUP#.

OPTIONS:
");
            options.WriteOptionDescriptions(Console.Out);
        }

        private static (Hasher.Algorithm HashAlgorithm, bool ShowProgress, List<string> PathsToHash) ParseOptions(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Log.Verbosity = Log.VerbositySetting.Verbose;
            Log.ProgramName = "HASHRENAME";

            Hasher.Algorithm type = Hasher.Algorithm.MD5;
            var showProgress = false;
            var options = new OptionSet
            {
                {
                    "t|type=",
                    // Get the list of all the hash types, but hide blockhash because image-only hashing is not the goal here.
                    $"The type of hasing to use. Default is md5. Available options: {string.Join(", ", Enum.GetNames(typeof(Hasher.Algorithm)).Select(_ => _.ToLowerInvariant()).Where(_ => !_.Equals("blockhash", StringComparison.OrdinalIgnoreCase)))}.",
                    (Hasher.Algorithm t) => type = t
                },
                {
                    "progress",
                    "Show progress. Will scan ahead for all files to be hashed. Due to the nature of outputting additional characters for status updates, this is primarily intended for human viewing.",
                    p => showProgress = p != null
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
                Log.Inf("Try `tom.exe hash --help` for more information.");
                Environment.Exit(1);
            }
            return (type, showProgress, pathsToHash);
        }
    }
}
