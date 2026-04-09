using AutoUpdaterDotNET;
using Microsoft.Win32;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Application = System.Windows.Forms.Application;
using Timer = System.Timers.Timer;

namespace workspacer
{

    class Program
    {
        private static workspacer _app;
        private static Branch? _branch;
        private static Logger _logger = Logger.Create();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ConfigContext context = new ConfigContext();
            try
            {
                ConfigHelper.DoConfig(context);
            }
            catch
            {
                // suppress error
            }
            _branch = context.Branch;
            context.SystemTray.Dispose();

            // check for updates
            if (_branch is null)
            {
#if BRANCH_unstable
                _branch = Branch.Unstable;
#elif BRANCH_stable
                _branch = Branch.Stable;
#elif BRANCH_beta
                _branch = Branch.Beta;
#else
                _branch = Branch.None;
#endif
            }

            Run();
        }

        private static void Run()
        {
            _app = new workspacer();

#if !DEBUG
            System.Threading.Thread.GetDomain().UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is System.Threading.ThreadAbortException) return;

                _logger.Fatal((Exception) e.ExceptionObject, "exception occurred, quitting workspacer: " + (Exception) e.ExceptionObject);
                _app.QuitWithException((Exception) e.ExceptionObject);
            };
#endif

            _app.Start();
        }

        #region Helper Methods
        private static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                using (File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                { }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // <summary>
        // Gets the InstallLocation value of app from registry based on DisplayName and Publisher. Returns null if it does not exist.
        // </summary>
        public static string GetInstallLocation(string displayName, string publisher)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            return key.GetSubKeyNames()
                .Select(keyName => key.OpenSubKey(keyName))
                .Where(subKey => displayName == (string)subKey?.GetValue("DisplayName")
                                 && publisher == (string)subKey?.GetValue("Publisher"))
                .Select(subKey => (string)subKey?.GetValue("InstallLocation")).FirstOrDefault();
        }
        #endregion
    }
}
