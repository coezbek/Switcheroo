/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * https://github.com/coezbek/switcheroo
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 * 
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Switcheroo.Properties;

namespace Switcheroo
{
    internal class Program
    {
        private const string mutex_id = "DBDE24E4-91F6-11DF-B495-C536DFD72085-switcheroo";

#if CONSOLE_DEBUG
        // P/Invoke declarations for console handling
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(uint dwProcessId);

        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        private delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        private enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static CancellationTokenSource _cts;
#endif

        [STAThread]
        private static void Main()
        {
#if CONSOLE_DEBUG
            // Attach to the parent console (your WSL terminal)
            AttachConsole(ATTACH_PARENT_PROCESS);
            _cts = new CancellationTokenSource();
            
            // Set up the low-level handler
            SetConsoleCtrlHandler(type => {
                Console.WriteLine("Ctrl+C detected. Shutting down Switcheroo...");
                if (type == CtrlTypes.CTRL_C_EVENT)
                {
                    Console.WriteLine("Ctrl+C detected. Shutting down Switcheroo...");
                    _cts.Cancel();
                    // Return true to indicate we've handled the event
                    return true;
                }
                return false;
            }, true);
#endif
            RunAsAdministratorIfConfigured();

            using (var mutex = new Mutex(false, mutex_id))
            {
                var hasHandle = false;
                try
                {
                    try
                    {
                        hasHandle = mutex.WaitOne(5000, false);
                        if (hasHandle == false) return; //another instance exist
                    }
                    catch (AbandonedMutexException)
                    {
                        // Log the fact the mutex was abandoned in another process, it will still get aquired
                    }

#if PORTABLE
                    MakePortable(Settings.Default);
#endif

                    MigrateUserSettings();

                    var app = new App();
                    var mainWindow = new MainWindow();

#if CONSOLE_DEBUG
                    // When cancellation is requested, shut down the WPF application
                    _cts.Token.Register(() =>
                    {
                        // We need to dispatch this to the UI thread
                        app.Dispatcher.Invoke(app.Shutdown);
                    });
#endif

                    Console.WriteLine("Switcheroo started...");

                    // This starts the WPF message loop and blocks until the app exits
                    app.Run(mainWindow);
                }
                finally
                {
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }
        }

        private static void RunAsAdministratorIfConfigured()
        {
            if (RunAsAdminRequested() && !IsRunAsAdmin())
            {
                ProcessStartInfo proc = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Assembly.GetEntryAssembly().CodeBase,
                    Verb = "runas"
                };

                Process.Start(proc);
                Environment.Exit(0);
            }
        }

        private static bool RunAsAdminRequested()
        {
            return Settings.Default.RunAsAdmin;
        }

        private static void MakePortable(ApplicationSettingsBase settings)
        {
            var portableSettingsProvider = new PortableSettingsProvider();
            settings.Providers.Add(portableSettingsProvider);
            foreach (SettingsProperty prop in settings.Properties)
            {
                prop.Provider = portableSettingsProvider;
            }
            settings.Reload();
        }

        private static void MigrateUserSettings()
        {
            if (!Settings.Default.FirstRun) return;

            Settings.Default.Upgrade();
            Settings.Default.FirstRun = false;
            Settings.Default.Save();
        }

        private static bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}