using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace ScriptShell
{
    public class DirectoryReferenceResolver: ScriptShell.Program.IReferenceResolver
    {
        string _dirPath;

        public DirectoryReferenceResolver(string dirPath)
        {
            _dirPath = dirPath;
        }

        private Dictionary<string, Assembly> _assemblyCache = new Dictionary<string, Assembly>();

        private Assembly GetAssembly(AssemblyName assemblyName)
        {
            return GetAssembly(assemblyName.Name + ".dll");
        }

        private Assembly GetAssembly(string path)
        {
            path = Path.Combine(_dirPath, path);
            if (_assemblyCache.ContainsKey(path))
                return _assemblyCache[path];
            if (!File.Exists(path))
                return null;
            _assemblyCache[path] = Assembly.LoadFile(Path.GetFullPath(path));
            return _assemblyCache[path];
        }

        public IEnumerable<Assembly> GetAssemblies()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            return Directory.EnumerateFiles(_dirPath).Where(name => name.ToLower().EndsWith(".dll")).Select<string, Assembly>(
                path => GetAssembly(path));
        }

        public Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            return GetAssembly(assemblyName);
        }
    }
}
