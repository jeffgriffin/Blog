using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.IO;
using System.Management.Automation.Host;
using System.Security;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;

namespace ScriptShell
{
    public class ConsolePsHostUserInterface : PSHostUserInterface
    {
        public ConsolePsHostUserInterface()
        {
        }

        public override Dictionary<string, PSObject> Prompt(string caption, string message, System.Collections.ObjectModel.Collection<FieldDescription> descriptions)
        {
            Console.WriteLine(caption);
            message = Console.ReadLine();
            return null;
        }

        public override int PromptForChoice(string caption, string message, System.Collections.ObjectModel.Collection<ChoiceDescription> choices, int defaultChoice)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            return null;
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            return null;
        }

        public override PSHostRawUserInterface RawUI
        {
            get { return null; }
        }

        public override string ReadLine()
        {
            return Console.ReadLine();
        }

        public override System.Security.SecureString ReadLineAsSecureString()
        {
            SecureString secret = new SecureString();
            ConsoleKeyInfo currentKey;
            while ((currentKey=Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (currentKey.Key == ConsoleKey.Backspace)
                {
                    if (secret.Length > 0)
                    {
                        secret.RemoveAt(secret.Length - 1);
                        Console.Write(currentKey.KeyChar);
                    }
                }
                else
                {
                    secret.AppendChar(currentKey.KeyChar);
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            secret.MakeReadOnly();
            return secret;

        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            Console.Write(value);
        }

        public override void Write(string value)
        {
            Console.Write(value);
        }

        public override void WriteDebugLine(string message)
        {
            Console.Write(message);
        }

        public override void WriteErrorLine(string value)
        {
            Console.Write(value);
        }

        public override void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            Console.WriteLine(record.PercentComplete);
        }

        public override void WriteVerboseLine(string message)
        {
            Console.Write(message);
        }

        public override void WriteWarningLine(string message)
        {
            Console.Write(message);
        }
    }

    public class ConsolePSHost : PSHost
    {
        public override System.Globalization.CultureInfo CurrentCulture
        {
            get { return Thread.CurrentThread.CurrentCulture; }
        }

        public override System.Globalization.CultureInfo CurrentUICulture
        {
            get { return Thread.CurrentThread.CurrentUICulture; }
        }

        public override void EnterNestedPrompt()
        {
        }

        public override void ExitNestedPrompt()
        {
        }

        private Guid m_InstanceId = Guid.Empty;

        public override Guid InstanceId
        {
            get
            {
                if (this.m_InstanceId == Guid.Empty)
                {
                    this.m_InstanceId = Guid.NewGuid();
                }

                return this.m_InstanceId;
            }
        }

        public override string Name
        {
            get { return "ConsolePSHost"; }
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public override void SetShouldExit(int exitCode)
        {
        }

        private PSHostUserInterface _ui = new ConsolePsHostUserInterface();
        public override PSHostUserInterface UI
        {
            get { return _ui; }
        }

        public override Version Version
        {
            get
            {
                Assembly executing = Assembly.GetExecutingAssembly();
                AssemblyName name = executing.GetName();
                return name.Version;
            }
        }
    }

    public static class PowerShellRunner
    {
        private static PowerShell _pws = null;
        private static PowerShell PowerShell
        {
            get 
            {
                if (_pws == null)
                {
                    _pws = PowerShell.Create();
                    ConsolePSHost host = new ConsolePSHost();
                    _pws.Runspace = RunspaceFactory.CreateRunspace(host);
                    _pws.Runspace.Open();
                }
                return _pws; 
            }
        }

        public static void Run(string file)
        {
            PowerShell.AddScript(File.ReadAllText(file));
            PowerShell.AddCommand("Out-Default");
            try { PowerShell.Invoke(); }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                Console.WriteLine();
            }
        }
    }
}
