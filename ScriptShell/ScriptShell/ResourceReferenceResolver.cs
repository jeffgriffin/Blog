using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Globalization;

namespace ScriptShell
{
    public class ResourceReferenceResolver: ScriptShell.Program.IReferenceResolver
    {
        private Dictionary<string, Assembly> _assemblyCache = new Dictionary<string, Assembly>();

        private Assembly GetAssembly(string path)
        {
            if (_assemblyCache.ContainsKey(path))
                return _assemblyCache[path];

            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            using (Stream stream = executingAssembly.GetManifestResourceStream(path))
            {
                if (stream == null)
                    return null;

                byte[] assemblyRawBytes = new byte[stream.Length];
                stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                _assemblyCache[path] = Assembly.Load(assemblyRawBytes);
                return _assemblyCache[path];
            }
        }

        public IEnumerable<Assembly> GetResourceReferences()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            return executingAssembly.GetManifestResourceNames().Where(name => name.ToLower().EndsWith(".dll")).Select<string, Assembly>(
                path => GetAssembly(path));
        }

        public Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            /*if (assemblyName.CultureInfo == null)
                return null;*/

            string path = assemblyName.Name + ".dll";
            if (assemblyName.CultureInfo!=null && assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false)
            {
                path = String.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
            }

            return GetAssembly(path);
        }
    }
}
