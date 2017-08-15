using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unlimitedinf.Tools;

namespace Unlimitedinf.Tom
{
    /// <summary>
    /// The main program entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main method.
        /// </summary>
        /// <remarks>
        /// Populate submodules.
        /// If args length is 0, provide overall helptext.
        /// Else, call submodule.
        /// </remarks>
        public static void Main(string[] args)
        {
            Log.Verbosity = Log.VerbositySetting.Verbose;
            Log.ProgramName = "TOM.EXE";
            Log.PrintProgramName = true;
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var itoms = GatherItoms();

            if (args.Length == 0)
            {
                Log.Ver("Printing help because args.Length == 0.");
                PrintHelp(itoms);
            }
            else if (!itoms.ContainsKey(args[0]))
            {
                Log.Wrn($"Module '{args[0]}' not found!");
                PrintHelp(itoms);
            }
            else
            {
                Log.Ver($"Running module '{args[0]}'");
                var itom = itoms[args[0]];
                var rargs = new string[args.Length - 1];
                Array.Copy(args, 1, rargs, 0, args.Length - 1);
                if (itom.IsAsync)
                    itom.RunAsync(rargs).Wait();
                else
                    itom.Run(rargs);
            }
        }

        private static Dictionary<string, ITom> GatherItoms()
        {
            var dir = new FileInfo(typeof(Program).Assembly.Location).Directory;
            var assemblies = dir.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly).Select(_ => Assembly.LoadFile(_.FullName)).ToList();
            // Keep only the assemblies that have one ITom, and complain and remove the assemblies that have more than one ITom
            assemblies = assemblies.Where(assembly =>
            {
                var types = assembly.GetTypes();
                types = types.Where(type => type.GetInterfaces().Contains(typeof(ITom)) && type.IsClass && type.IsPublic && type.IsSealed).ToArray();
                if (types.Length == 0)
                {
                    Log.Ver($"Found 0 {nameof(ITom)} in {assembly.GetName().Name}.");
                    return false;
                }
                else if (types.Length > 1)
                {
                    Log.Wrn($"Found >1 {nameof(ITom)} in {assembly.GetName().Name}!");
                    return false;
                }
                else
                {
                    Log.Ver($"Found 1 {nameof(ITom)} in {assembly.GetName().Name}.");
                    return true;
                }
            }).ToList();

            var itomTypes = assemblies.Select(assembly => assembly.GetTypes().Single(t => t.GetInterfaces().Contains(typeof(ITom)) && t.IsClass && t.IsPublic && t.IsSealed)).ToList();
            return itomTypes.Select(itomType => (ITom)Activator.CreateInstance(itomType)).ToDictionary(itom => itom.Name);
        }

        private static void PrintHelp(Dictionary<string, ITom> itoms)
        {
            var moduleStrings = itoms.Values.Select(itom =>
            {
                var descriptionTokens = itom.Description.Trim().Split(' ');
                var sb = new StringBuilder(80);

                // 20 chars for module name, 2 spaces before and after
                var line = new StringBuilder(80);
                line.Append("  ").Append(itom.Name.PadRight(16)).Append("  ").Append(descriptionTokens[0]);

                // 60 chars for lines of description
                // Put the version on the second line, 4 chars indented.
                var lineNumber = 1;
                var versionAdded = false;
                for (int i = 1; i < descriptionTokens.Length - 1; i++)
                {
                    if (line.Length < 80 && line.Length + descriptionTokens[i + 1].Length + 1 < 80)
                        // See if we can add another token to the current line
                        line.Append(' ').Append(descriptionTokens[i]);
                    else
                    {
                        // Else add a new line and keep going
                        sb.Append(line.ToString()).AppendLine();
                        line.Clear().Append(' ', 20).Append(descriptionTokens[i]);
                        lineNumber++;
                    }
                    if (lineNumber == 2 && !versionAdded)
                    {
                        // And if we're the second line, put the version in there
                        line.Clear()
                            .Append("    ")
                            .Append('v')
                            .Append(FileVersionInfo.GetVersionInfo(itom.GetType().Assembly.Location).FileVersion.PadRight(14))
                            .Append("  ")
                            .Append(descriptionTokens[i]);
                        versionAdded = true;
                    }
                }
                // At this point, sb has everything except the last token. line is set up to take that last token.
                line.Append(' ').Append(descriptionTokens[descriptionTokens.Length - 1]);
                sb.Append(line.ToString());

                // Double check if we need to print the version because of a single line description.
                if (lineNumber == 1)
                    sb.AppendLine()
                        .Append("    v")
                        .Append(FileVersionInfo.GetVersionInfo(itom.GetType().Assembly.Location).FileVersion);

                return sb.ToString();
            }).ToList();

            Console.WriteLine($@"
tom.exe v{FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).FileVersion}

Modules:
{string.Join(Environment.NewLine, moduleStrings)}
");
        }
    }
}
