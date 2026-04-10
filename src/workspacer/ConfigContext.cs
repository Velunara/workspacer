using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace workspacer
{
    public class ConfigContext : IConfigContext
    {
        public KeybindManager Keybinds { get; set; }
        IKeybindManager IConfigContext.Keybinds { get { return Keybinds; } }

        public WorkspaceManager Workspaces { get; set; }
        IWorkspaceManager IConfigContext.Workspaces { get { return Workspaces; } }

        public PluginManager Plugins { get; set; }
        IPluginManager IConfigContext.Plugins { get { return Plugins; } }

        public SystemTrayManager SystemTray { get; set; }
        ISystemTrayManager IConfigContext.SystemTray { get { return SystemTray; } }

        public WindowsManager Windows { get; set; }
        IWindowsManager IConfigContext.Windows { get { return Windows; } }

        public IWorkspaceContainer WorkspaceContainer { get; set; }
        public IWindowRouter WindowRouter { get; set; }
        public IMonitorContainer MonitorContainer { get; set; }

        public bool CanMinimizeWindows { get; set; } = false;
        public WindowOrder NewWindowOrder { get; set; } = WindowOrder.NewWindowsLast;

        public string ConfigDirectory => FileHelper.GetConfigDirectory();
        public bool Initializing { get; private set; } = true;
        private bool _exiting;

        private System.Timers.Timer _timer;
        private PipeServer _pipeServer;
        private Func<ILayoutEngine[]> _defaultLayouts;
        private List<Func<ILayoutEngine, ILayoutEngine>> _layoutProxies;
        private AltDrag _altDrag;
        private CancellationTokenSource _ctsSave = new CancellationTokenSource();

        public ConfigContext()
        {
            _exiting = false;
            _timer = new System.Timers.Timer();
            _timer.Elapsed += (s, e) => UpdateActiveHandles();
            _timer.Interval = 5000;
            _timer.Enabled = true;

            _pipeServer = new PipeServer();

            _defaultLayouts = () => new ILayoutEngine[] {
                new MasterLayoutEngine(),
            };
            _layoutProxies = new List<Func<ILayoutEngine, ILayoutEngine>>();

            SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;

            Plugins = new PluginManager();
            SystemTray = new SystemTrayManager();
            Workspaces = new WorkspaceManager(this);
            Windows = new WindowsManager();
            Keybinds = new KeybindManager(this);

            MonitorContainer = new NativeMonitorContainer();
            WorkspaceContainer = new WorkspaceContainer(this);
            WindowRouter = new WindowRouter(this);

            Windows.WindowCreated += Workspaces.AddWindow;
            Windows.WindowDestroyed += Workspaces.RemoveWindow;
            Windows.WindowUpdated += Workspaces.UpdateWindow;

            Windows.WorkspacerExternalWindowUpdate += Workspaces.HandleWindowUpdated;

            // ignore watcher windows in workspacer
            WindowRouter.AddFilter((window) => window.ProcessId != _pipeServer.WatcherProcess.Id);

            // ignore SunAwtWindows (common in some Sun AWT programs such at JetBrains products), prevents flickering
            WindowRouter.AddFilter((window) => !window.Class.Contains("SunAwtWindow"));

            var initThread = new Thread(() =>
            {
                while (DateTime.Now - Workspaces.LastWindowAddedTime > TimeSpan.FromSeconds(1))
                {
                    Thread.Sleep(100);
                }
                
                Initializing = false;
            });
            initThread.Start();
            
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                CleanupAndExit();
            };

            Task.Run(async () =>
            {
                while (!_ctsSave.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _ctsSave.Token);
                    SaveState();
                }
            });
        }

        public void ConnectToWatcher()
        {
            _pipeServer.Start();
        }

        public LogLevel ConsoleLogLevel
        {
            get
            {
                return Logger.ConsoleLogLevel;
            }
            set
            {
                Logger.ConsoleLogLevel = value;
            }
        }

        public LogLevel FileLogLevel
        {
            get
            {
                return Logger.FileLogLevel;
            }
            set
            {
                Logger.FileLogLevel = value;
            }
        }

        public MonitorRemoveStrategy MonitorRemoveStrategy { get; set; } = MonitorRemoveStrategy.Spread;

        public Func<ILayoutEngine[]> DefaultLayouts
        {
            get
            {
                return () => ProxyLayouts(_defaultLayouts()).ToArray();
            }
            set
            {
                _defaultLayouts = value;
            }
        }

        public void AddLayoutProxy(Func<ILayoutEngine, ILayoutEngine> proxy)
        {
            _layoutProxies.Add(proxy);
        }

        public IEnumerable<ILayoutEngine> ProxyLayouts(IEnumerable<ILayoutEngine> layouts)
        {
            for (var i = 0; i < _layoutProxies.Count; i++)
            {
                layouts = layouts.Select(layout => _layoutProxies[i](layout)).ToArray();
            }
            return layouts;
        }

        public Branch? Branch { get; set; }

        public void ToggleConsoleWindow()
        {
            var response = new LauncherResponse()
            {
                Action = LauncherAction.ToggleConsole,
            };
            SendResponse(response);
        }

        public void SendLogToConsole(string message)
        {
            var response = new LauncherResponse()
            {
                Action = LauncherAction.Log,
                Message = message,
            };
            SendResponse(response);
        }

        private void SendResponse(LauncherResponse response)
        {
            var str = JsonConvert.SerializeObject(response);
            _pipeServer.SendResponse(str);
        }

        public void Restart()
        {
            _ctsSave.Cancel();
            SaveState();
            var response = new LauncherResponse()
            {
                Action = LauncherAction.Restart,
            };
            SendResponse(response);

            CleanupAndExit();
        }

        public void Quit()
        {
            var response = new LauncherResponse()
            {
                Action = LauncherAction.Quit,
            };
            SendResponse(response);

            CleanupAndExit();
        }

        public void QuitWithException(Exception e)
        {
            var message = e.ToString();
            var response = new LauncherResponse()
            {
                Action = LauncherAction.QuitWithException,
                Message = message,
            };
            SendResponse(response);

            CleanupAndExit();
        }

        public void CleanupAndExit()
        {
            _exiting = true;
            foreach (var window in Windows.Windows)
            {
                Win32.ShowWindow(window.Handle, Win32.SW.SW_SHOW);
            }
            SaveState();
            
            _altDrag?.Dispose();
            SystemTray.Dispose();
            Application.Exit();
        }

        private void UpdateActiveHandles()
        {
            var response = new LauncherResponse()
            {
                Action = LauncherAction.UpdateHandles,
                ActiveHandles = GetActiveHandles().Select(h => h.ToInt64()).ToList(),
            };
            SendResponse(response);
        }

        private List<IntPtr> GetActiveHandles()
        {
            var list = new List<IntPtr>();
            if (WorkspaceContainer == null) return list;

            foreach (var ws in WorkspaceContainer.GetAllWorkspaces())
            {
                var handles = ws.ManagedWindows.Select(i => i.Handle);
                list.AddRange(handles);
            }
            return list;
        }

        private void HandleDisplaySettingsChanged(object sender, EventArgs e)
        {
            SaveState();
            var response = new LauncherResponse()
            {
                Action = LauncherAction.Restart
            };
            SendResponse(response);

            CleanupAndExit();
        }

        public bool Enabled
        {
            get => workspacer.Enabled;
            set
            {
                workspacer.Enabled = value;
            }
        }

        private void SaveState()
        {
            if (Initializing)
                return;

            var filePath = Path.Combine(Path.GetTempPath(), "workspacer.State.json");

            var state = GetState();

            // Optional: guard against empty state too
            if (IsEmptyState(state))
                return;

            var json = JsonConvert.SerializeObject(state);
            //Logger.Create().Debug(json);

            File.WriteAllText(filePath, json);
        }
        
        private bool IsEmptyState(WorkspacerState state)
        {
            var ws = state?.WorkspaceState?.MonitorWorkspaceWindows;

            if (ws == null)
                return true;

            return ws.All(m => m.All(w => w.Count == 0));
        }
        
        public WorkspacerState LoadState()
        {
            var filePath = Path.Combine(Path.GetTempPath(), "workspacer.State.json");

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var state = JsonConvert.DeserializeObject<WorkspacerState>(json);
                File.Delete(filePath);
                return state;
            }
            else
            {
                return null;
            }
        }

        public void UseAltDrag(KeyModifiers modifiers = KeyModifiers.Alt)
        {
            _altDrag = new AltDrag(this, modifiers);
        }

        private WorkspacerState GetState()
        {
            var state = new WorkspacerState()
            {
                WorkspaceState = Workspaces.GetState(),
                WindowState = Windows.GetState()
            };
            return state;
        }
    }
}
