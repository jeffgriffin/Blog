using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security;
using System.Reflection;
using System.Threading;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting;

namespace ScriptShell
{
    public static class PythonRunner
    {
        private static ScriptEngine _engine = null;
        private static ScriptEngine ScriptEngine
        {
            get
            {
                if (_engine == null)
                {
                    _engine = IronPython.Hosting.Python.CreateEngine();
                    _engine.Runtime.IO.SetOutput(Console.OpenStandardOutput(), Console.Out);
                    _engine.Runtime.IO.SetErrorOutput(Console.OpenStandardOutput(), Console.Out);
                }
                return _engine;
            }
        }

        public static void Run(string file)
        {
            try { ScriptEngine.CreateScriptSourceFromFile(file).Execute(); }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                Console.WriteLine();
            }
        }
    }
}
