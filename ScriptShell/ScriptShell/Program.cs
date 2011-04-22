using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;

namespace ScriptShell
{
    class Program
    {
        const string USAGE = @"
Usage: ScriptShell [script-file-list] [-r reference-directory-list] 
                   [-d scripts-directory-list] [-a]

Options:
    script-file-list              Run the specified script file(s) .
    -r reference-directory-list   Look for reference assemblies in this/these
                                  location(s).
    -d scripts-directory-list     Run scripts from the specified 
                                  directory/directories.
    -a                            Automated mode.  This option will not prompt 
                                  for advancement.  
                                  Note: Console prompts written into script 
                                  files will be unaffected.
                                  
    When specifying script files with the -s and -d options, the specified 
    script files or runnable directory contents must be named with the 
    following extensions:
                                  *.py or *.griffpy for IronPython scripts
                                  *.ps1 or *.griffps for PowerShell scripts";

        internal interface IReferenceResolver
        {
            Assembly OnResolveAssembly(object sender, ResolveEventArgs args);
        }

        static List<IReferenceResolver> _resolvers = new List<IReferenceResolver>();

        static Program()
        {
            _resolvers.Add(new ResourceReferenceResolver());
            AppDomain.CurrentDomain.AssemblyResolve +=
                delegate(object sender, ResolveEventArgs args)
                {
                    return _resolvers.Select<IReferenceResolver, Assembly>(
                        drr => drr.OnResolveAssembly(sender, args)).FirstOrDefault(asm => asm != null);
                };
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(USAGE);
                return;
            }
            List<string> runFiles = new List<string>();
            bool automatedMode = false;

            IEnumerable<string> files = args.TakeWhile(arg => !arg.StartsWith("-"));
            foreach (string sFile in files)
            {
                if (!File.Exists(sFile))
                {
                    Console.WriteLine(string.Format("File {0} not found.", sFile));
                    return;
                }
                string file_lower = sFile.ToLower();
                if (!file_lower.EndsWith(".ps1") && !file_lower.EndsWith(".griffps") && !file_lower.EndsWith(".py") && !file_lower.EndsWith(".griffpy"))
                {
                    Console.WriteLine(string.Format("File {0} not named with supported extension (\".ps1\", \".griffps\", \".py\" or \".griffpy\").", sFile));
                    return;
                }
                runFiles.Add(file_lower);
                _resolvers.Add(new DirectoryReferenceResolver(Path.GetDirectoryName(Path.GetFullPath(file_lower))));
            }
            for (int i = files.Count(); i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-r":
                        IEnumerable<string> dirs = args.Skip(i + 1).TakeWhile(arg => !arg.StartsWith("-"));
                        foreach(string dir in dirs)
                        {
                            if (!Directory.Exists(dir))
                            {
                                Console.WriteLine(string.Format("Directory {0} not found.", dir));
                                return;
                            }
                            _resolvers.Add(new DirectoryReferenceResolver(dir));
                        }
                        i += dirs.Count();
                        break;
                    case "-d":
                        IEnumerable<string> sDirs = args.Skip(i + 1).TakeWhile(arg => !arg.StartsWith("-"));
                        foreach (string dir in sDirs)
                        {
                            if (!Directory.Exists(dir))
                            {
                                Console.WriteLine(string.Format("Directory {0} not found.", dir));
                                return;
                            }
                            _resolvers.Add(new DirectoryReferenceResolver(dir));
                            foreach (string file in Directory.EnumerateFiles(dir))
                            {
                                string file_lower = file.ToLower();
                                if (file_lower.EndsWith(".ps1") || file_lower.EndsWith(".griffps") || file_lower.EndsWith(".py") || file_lower.EndsWith(".griffpy"))
                                    runFiles.Add(file_lower);
                            }
                        }
                        i += sDirs.Count();
                        break;
                    case "-a":
                        automatedMode = true;
                        break;
                    default:
                        Console.WriteLine("Unrecognized parameter: " + args[i]);
                        Console.WriteLine(USAGE);
                        return;
                        break;
                }
            }
            foreach (string scriptFile in runFiles)
            {
                if (scriptFile.EndsWith(".ps1") || scriptFile.EndsWith(".griffps"))
                {
                    if (!automatedMode)
                    {
                        Console.WriteLine("{0} is a PowerShell script.  Run this script now? ([y]/n).", Path.GetFileName(scriptFile));
                        if (Console.ReadKey().Key != ConsoleKey.N)
                            PowerShellRunner.Run(scriptFile);
                        Console.WriteLine();
                    }
                    else
                        PowerShellRunner.Run(scriptFile);
                }
                else if (scriptFile.EndsWith(".py") || scriptFile.EndsWith(".griffpy"))
                {
                    if (!automatedMode)
                    {
                        Console.WriteLine("{0} is a Python script.  Run this script now? ([y]/n).", Path.GetFileName(scriptFile));
                        if (Console.ReadKey().Key != ConsoleKey.N)
                            PythonRunner.Run(scriptFile);
                        Console.WriteLine();
                    }
                    else
                        PythonRunner.Run(scriptFile);
                }
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(false);
                }
            }
            if (!automatedMode)
            {
                Console.WriteLine("Press enter to exit ScriptShell.");
                Console.ReadLine();
            }
        }
    }
}
