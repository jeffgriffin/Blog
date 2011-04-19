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
