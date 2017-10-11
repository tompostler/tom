using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unlimitedinf.Tools;

namespace Unlimitedinf.Tom.ImageDimensionDelete
{
    /// <summary>
    /// Main program.
    /// </summary>
    public sealed class Program : ITom
    {
        /// <inheritdoc/>
        public string Name => "img-dimdel";

        /// <inheritdoc/>
        public string Description => "Delete images based on image size.";

        /// <inheritdoc/>
        public bool IsAsync => false;

        /// <inheritdoc/>
        public void Run(string[] args)
        {
            var options = ParseOptions(args);

            var fileInfos = options.SourceDir.EnumerateFiles().ToList();
            long totalBytes = fileInfos.Sum(_ => _.Length);
            long seenBytes = 0;
            long gcBytes = 100_000_000;
            Log.Inf($"There are {fileInfos.Count} files to img-dimdir, with {(totalBytes / 1e6).ToString("0.00")}MB of data to img-dimdir.");

            Parallel.ForEach(fileInfos, fileInfo =>
            {
                Interlocked.Add(ref seenBytes, fileInfo.Length);
                DoTheThing(options, fileInfo);

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

        private static void DoTheThing(Options options, FileInfo info)
        {
            try
            {
                using (var image = Image.FromFile(info.FullName))
                {
                    // Figure out if we should keep it
                    var keep = options.ShouldKeepImage(image);
                    if (keep)
                        return;
                    lock (consolelock)
                        Log.Inf($"{info.Name}: {options.WhyNotKeepImage(image)}");
                }

                // Move it
                info.MoveTo(Path.Combine(options.MoveToDir.FullName, info.Name));
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Log.Err($"{info.FullName}: {e.Message}");
            }
        }

        private static void PrintHelp(OptionSet options)
        {
            Console.WriteLine($@"
tom.exe v{FileVersionInfo.GetVersionInfo(typeof(ITom).Assembly.Location).FileVersion} img-dimdel v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion}
Usage: tom.exe img-dimdel [OPTIONS]+ DIR

Based on the dimensions of an image, move it.

Items to filter on include: megapixels, width, and height. If width and height
are selected for filtering, megapixels will be ignored. If only one filter, or
one dimension with megapixels, is defined, then each will apply separately.

Defaults to only checking for images >0.5 megapixels, and moves the ones that
don't qualify to a new directory in the DIR called 'toss'.

OPTIONS:
");
            options.WriteOptionDescriptions(Console.Out);
        }

        private class Options
        {
            public DirectoryInfo SourceDir { get; set; } = null;
            public uint Width { get; set; } = 0;
            public uint Height { get; set; } = 0;
            public double Megapixels { get; set; } = 0.5;
            public DirectoryInfo MoveToDir { get; set; } = null;
            public bool ShowProgress { get; set; } = true;

            /// <summary>
            /// True for good options. False for bad options.
            /// </summary>
            public bool ValidateOptions()
            {
                // This is required in order to work
                if (this.SourceDir == null)
                    throw new ArgumentNullException("DIR");

                // Make sure at least one width/height/mp is set
                if (this.Width <= 0 && this.Height <= 0 && this.Megapixels <= 0)
                {
                    Log.Err($"ARGS: Combination of width ({this.Width}), height ({this.Height}), and megapixels ({this.Megapixels}) is invalid.");
                    return false;
                }

                // Create MoveToDir if it doesn't exist
                if (MoveToDir != null && !MoveToDir.Exists)
                {
                    Log.Wrn($"ARGS: dest-dir '{MoveToDir.FullName}' does not exist. Creating it!");
                    MoveToDir.Create();
                }

                return true;
            }

            /// <summary>
            /// Determine if an image should be kept based on the options.
            /// </summary>
            public bool ShouldKeepImage(Image image)
            {
                // If there's height and width
                if (this.Width > 0 && this.Height > 0)
                    return image.Width >= this.Width && image.Height >= this.Height;

                // Check just width
                if (this.Width > 0 && image.Width < this.Width)
                    return false;

                // Check just height
                if (this.Height > 0 && image.Height < this.Height)
                    return false;

                // Check the mp
                return image.GetMegapixels() > this.Megapixels;
            }

            /// <summary>
            /// Return a message saying why we should not keep it.
            /// </summary>
            public string WhyNotKeepImage(Image image)
            {
                return $"{(this.Width > 0 && image.Width < this.Width ? $"wi {image.Width.ToString().PadLeft(5)} ({this.Width}) " : "")}"
                    + $"{(this.Height > 0 && image.Height < this.Height ? $"he {image.Height.ToString().PadLeft(5)} ({this.Height}) " : "")}"
                    + $"{(this.Megapixels > 0 && image.GetMegapixels() < this.Megapixels ? $"mp {image.GetMegapixels():0.00} ({this.Megapixels:0.00})" : "")}";
            }
        }

        private static Options ParseOptions(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            Log.Verbosity = Log.VerbositySetting.Verbose;
            Log.ProgramName = "IMGDIMDEL";

            var options = new Options();

            var optSet = new OptionSet
            {
                {
                    "w|width=",
                    "Minimum image width. Defaults to 0 which means ignore this dimension.",
                    (uint w) => options.Width = w
                },
                {
                    "h|height=",
                    "Minimum image height. Defaults to 0 which means ignore this dimension.",
                    (uint h) => options.Height = h
                },
                {
                    "m|megapixels=",
                    "Minimum image area. Defaults to 0.5",
                    (double m) => options.Megapixels = m
                },
                {
                    "d|dest-dir=",
                    "Move images to this directory. Defaults to DIR/toss",
                    (string d) => options.MoveToDir = new DirectoryInfo(d)
                },
                {
                    "p|progress",
                    "Show progress. Will scan ahead for all files, but does not check if they are images in advance. Due to the nature of outputting additional characters for status updates, this is primarily intended for human viewing. Defaults to true",
                    p => options.ShowProgress = p != null
                }
            };
            if (args.Length == 0 || args[0] == "--help")
            {
                PrintHelp(optSet);
                Environment.Exit(1);
            }

            try
            {
                var dir = optSet.Parse(args);
                if (dir.Count == 0)
                    throw new ArgumentNullException("DIR");
                if (dir.Count > 1)
                    throw new ArgumentException("Too many DIRs");
                options.SourceDir = new DirectoryInfo(dir[0]);
                if (options.MoveToDir == null)
                    options.MoveToDir = new DirectoryInfo(Path.Combine(options.SourceDir.FullName, "toss"));

                options.ValidateOptions();
            }
            catch (Exception e) when (e is OptionException || e is ArgumentNullException || e is ArgumentException)
            {
                Log.Err($"ARGS: {e.Message}");
                Log.Inf("Try `tom.exe img-dimdel --help` for more information.");
                Environment.Exit(1);
            }
            return options;
        }
    }

    internal static class ImageExtensions
    {
        public static double GetMegapixels(this Image image) => image.Width * image.Height / 1_000_000d;
    }
}
