using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

            var dir = new FileInfo(typeof(Program).Assembly.Location).Directory;
            var assemblies = dir.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly).Select(_ => Assembly.LoadFile(_.FullName)).ToList();
            // Keep only the assemblies that have one ITom, and complain and remove the assemblies that have more than one ITom
            assemblies = assemblies.Where(assembly =>
            {
                var types = assembly.GetTypes();
                types = types.Where(type => type.GetInterfaces().Contains(typeof(ITom)) && type.IsClass && type.IsPublic && type.IsSealed).ToArray();
                if (types.Length == 0)
                {
                    Log.Ver($"Found 0 ITom in {assembly.GetName().Name}.");
                    return false;
                }
                else if (types.Length > 1)
                {
                    Log.Wrn($"Found >1 ITom in {assembly.GetName().Name}!");
                    return false;
                }
                else
                {
                    Log.Ver($"Found 1 ITom in {assembly.GetName().Name}.");
                    return true;
                }
            }).ToList();

            var itomTypes = assemblies.Select(assembly => assembly.GetTypes().Single(t => t.GetInterfaces().Contains(typeof(ITom)) && t.IsClass && t.IsPublic && t.IsSealed)).ToList();
            var itoms = itomTypes.Select(itomType => (ITom)Activator.CreateInstance(itomType)).ToDictionary(itom => itom.Name);

            if (args.Length == 0)
            {
                Log.Ver("Printing help because args.Length == 0.");
            }
            else if (!itoms.ContainsKey(args[0]))
            {
                Log.Wrn($"Module '{args[0]}' not found!");
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
    }
}
