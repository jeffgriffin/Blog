//Copyright (c) 2011, Jeff Griffin
//All rights reserved.

//Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

//    Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Windows;
using System.Threading;
using net.visibleblue.util;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using System.Configuration;
using System.IO;
using System.Xml;

namespace TranscodeToMP4
{
    class Program
    {
        private static readonly string EMBEDDED_CONFIG_EXTRACT_LOCATION = @"%TEMP%\TranscodeToMP4.config";

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new Ref.ResourceReferenceResolver().OnResolveAssembly;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            LoadConfig();
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static void LoadConfig()
        {
            string path = Environment.ExpandEnvironmentVariables(EMBEDDED_CONFIG_EXTRACT_LOCATION);
            using(TextWriter writer = new StreamWriter(new FileStream(path, FileMode.Create)))
            {
                writer.Write(Properties.Resources.Embedded);
                writer.Flush();
            }
            ConfigurationManager.OpenExeConfiguration(path);
            Log.SetLogWriter(new LogWriterFactory(new FileConfigurationSource(path)).Create());
        }
    }
}
