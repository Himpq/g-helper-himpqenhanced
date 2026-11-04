using GHelper;
using GHelper.Mode;
using Microsoft.Win32;

namespace HimpqEnhanced
{
    public static class Main
    {
        private static readonly List<HimpqTaskbarWindow> _taskbarWindows = new();
        private static System.Windows.Forms.Timer? _initTimer;
        private static volatile bool _displayEventsRegistered;
        private static HimpqTaskbarWindow? _displayEventDispatcher;
        private static System.Windows.Forms.Timer? _displayChangeTimer;
        private static int _displayChangeDispatchPending;
        private static int _taskbarUiThreadId;

        public static void Init()
        {
            var config = HimpqConfig.Load();

            if (config.debug_mode == 1)
            {
                string modeName = GetPerformanceModeName(config.default_performance_mode, "未启用");
                string unplugModeName = GetPerformanceModeName(config.unplug_performance_mode, "不切换");
                MessageBox.Show(
                    $"HimpqEnhanced 已启动\n默认性能模式：{modeName}\n断电性能模式：{unplugModeName}",
                    "HimpqEnhanced",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            int targetMode = GetConfiguredPerformanceMode(config.default_performance_mode, "default performance");
            if (targetMode < 0) return;

            if (_initTimer is null)
            {
                _initTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _initTimer.Tick += (_, _) =>
                {
                    _initTimer.Stop();
                    _initTimer.Dispose();
                    _initTimer = null;

                    try
                    {
                        if (Program.modeControl is not null)
                        {
                            Program.modeControl.SetPerformanceMode(mode: targetMode, notify: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("Himpq default performance mode error: " + ex.Message);
                    }
                };
                _initTimer.Start();
            }
        }

        public static int GetUnplugPerformanceMode()
        {
            return GetConfiguredPerformanceMode(HimpqConfig.Load().unplug_performance_mode, "unplug performance");
        }

        private static int GetConfiguredPerformanceMode(int mode, string settingName)
        {
            if (mode < 0) return -1;

            if (Modes.GetDictonary().ContainsKey(mode)) return mode;

            Logger.WriteLine($"Himpq {settingName} mode is unavailable: {mode}");
            return -1;
        }

        private static string GetPerformanceModeName(int mode, string emptyText)
        {
            if (mode < 0) return emptyText;
            return Modes.GetDictonary().TryGetValue(mode, out string? modeName) ? modeName : $"未知 ({mode})";
        }

        public static void StartTaskbarWindow()
        {
            var config = HimpqConfig.Load();
            bool floatingMode = config.taskbar_window_floating_enabled == 1;

            // Embedded mode can only live in the primary taskbar (Shell_TrayWnd), so it
            // stays a single window. Floating mode is created once per target screen.
            List<string> targets = floatingMode
                ? ResolveTargetDevices(config)
                : new List<string> { "" };

            bool reuse = TaskbarWindowsMatch(floatingMode, targets);

            if (!reuse)
            {
                StopTaskbarWindow();
                foreach (string target in targets)
                {
                    var window = floatingMode
                        ? HimpqTaskbarWindow.CreateFloatingWindow(target)
                        : new HimpqTaskbarWindow(target);
                    window.EnsureHandleCreated();
                    _taskbarWindows.Add(window);
                }
            }

            foreach (var window in _taskbarWindows)
                window.ShowWindow();

            RegisterDisplayEvents();
        }

        private static void RegisterDisplayEvents()
        {
            if (_displayEventsRegistered) return;
            if (_taskbarWindows.Count == 0) return;

            _taskbarUiThreadId = Environment.CurrentManagedThreadId;
            _displayEventDispatcher = _taskbarWindows[0];
            _displayEventsRegistered = true;
            SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
        }

        private static void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
        {
            NotifyDisplayConfigurationChanged("SystemEvents.DisplaySettingsChanged");
        }

        public static void NotifyDisplayConfigurationChanged(string source)
        {
            if (!_displayEventsRegistered) return;

            var dispatcher = Volatile.Read(ref _displayEventDispatcher);
            if (dispatcher is null) return;
            if (Interlocked.Exchange(ref _displayChangeDispatchPending, 1) != 0) return;

            try
            {
                dispatcher.BeginInvoke((Action)(() =>
                {
                    Interlocked.Exchange(ref _displayChangeDispatchPending, 0);
                    if (!_displayEventsRegistered || !ReferenceEquals(dispatcher, Volatile.Read(ref _displayEventDispatcher)))
                        return;

                    Logger.WriteLine("Himpq taskbar: display change received from " + source + "; waiting for topology to settle.");
                    ScheduleDisplayChangeRefresh();
                }));
            }
            catch (InvalidOperationException ex)
            {
                Interlocked.Exchange(ref _displayChangeDispatchPending, 0);
                Logger.WriteLine("Himpq taskbar: couldn't queue display refresh from " + source + ": " + ex.Message);
            }
        }

        private static void ScheduleDisplayChangeRefresh()
        {
            _displayChangeTimer ??= new System.Windows.Forms.Timer { Interval = 300 };
            _displayChangeTimer.Tick -= OnDisplayChangeRefreshTick;
            _displayChangeTimer.Tick += OnDisplayChangeRefreshTick;
            _displayChangeTimer.Stop();
            _displayChangeTimer.Start();
        }

        private static void OnDisplayChangeRefreshTick(object? sender, EventArgs e)
        {
            _displayChangeTimer?.Stop();

            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1 || _taskbarWindows.Count == 0)
                return;

            bool floatingMode = config.taskbar_window_floating_enabled == 1;
            List<string> targets = floatingMode
                ? ResolveTargetDevices(config)
                : new List<string> { "" };

            if (!TaskbarWindowsMatch(floatingMode, targets))
            {
                Logger.WriteLine("Himpq taskbar: display target set changed; rebuilding " + targets.Count + " window(s).");
                StartTaskbarWindow();
                return;
            }

            Logger.WriteLine("Himpq taskbar: display geometry changed; repositioning " + _taskbarWindows.Count + " window(s).");
            foreach (var window in _taskbarWindows.ToArray())
                if (!window.IsDisposed)
                    window.HandleDisplayConfigurationChanged();
        }

        private static bool TaskbarWindowsMatch(bool floatingMode, IReadOnlyList<string> targets)
        {
            return _taskbarWindows.Count == targets.Count &&
                   _taskbarWindows.All(window => !window.IsDisposed && window.IsFloatingMode == floatingMode) &&
                   _taskbarWindows.Select(window => window.TargetDeviceName)
                       .SequenceEqual(targets, StringComparer.OrdinalIgnoreCase);
        }

        // Sort device names so display enumeration order changes do not recreate windows.
        private static List<string> ResolveTargetDevices(HimpqConfigData config)
        {
            if (config.taskbar_display_all == 1)
                return Screen.AllScreens
                    .OrderByDescending(screen => screen.Primary)
                    .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
                    .Select(screen => screen.DeviceName)
                    .ToList();

            return new List<string> { config.taskbar_display_device_name };
        }

        // True when a taskbar window other than the given one is still alive and using
        // the shared HardwareControl sensor flags.
        public static bool HasAnyTaskbarWindow(HimpqTaskbarWindow? except = null)
        {
            foreach (var window in _taskbarWindows)
                if (!ReferenceEquals(window, except) && !window.IsDisposed)
                    return true;
            return false;
        }

        public static void ShowTaskbarWindow()
        {
            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1) return;
            StartTaskbarWindow();
        }

        public static void HideTaskbarWindow()
        {
            foreach (var window in _taskbarWindows)
                window.HideWindow();
        }

        public static void RefreshTaskbarPosition()
        {
            foreach (var window in _taskbarWindows)
                window.RefreshLayout();
        }

        public static void RefreshTaskbarLayout()
        {
            foreach (var window in _taskbarWindows)
                window.RefreshLayout();
        }

        public static void RefreshTaskbarTheme()
        {
            foreach (var window in _taskbarWindows)
                window.RefreshTheme();
        }

        public static void RestartTaskbarWindow()
        {
            bool shouldShow = HimpqConfig.Load().taskbar_window_enabled == 1;
            StopTaskbarWindow();
            if (shouldShow)
                StartTaskbarWindow();
        }

        public static void RestartTaskbarWindowAfterShellRecreated()
        {
            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1) return;

            Logger.WriteLine("Himpq taskbar window: shell taskbar recreated, restarting monitor window");
            RestartTaskbarWindow();
        }

        public static void UpdateTaskbarFont(string fontName, float fontSize)
        {
            foreach (var window in _taskbarWindows)
                window.UpdateFont(fontName, fontSize);
        }

        public static void SuspendTopMostKeeper()
        {
            foreach (var window in _taskbarWindows)
                window.SuspendTopMostKeeper();
        }

        public static void ResumeTopMostKeeper()
        {
            foreach (var window in _taskbarWindows)
                window.ResumeTopMostKeeper();
        }

        public static void StopTaskbarWindow()
        {
            if (_taskbarUiThreadId != 0 && Environment.CurrentManagedThreadId != _taskbarUiThreadId)
            {
                var dispatcher = Volatile.Read(ref _displayEventDispatcher);
                if (dispatcher is not null)
                {
                    try
                    {
                        dispatcher.BeginInvoke((Action)StopTaskbarWindow);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Logger.WriteLine("Himpq taskbar: couldn't queue shutdown cleanup: " + ex.Message);
                    }
                    return;
                }

                Logger.WriteLine("Himpq taskbar: shutdown cleanup requested off the UI thread without a dispatcher.");
                return;
            }

            if (_displayEventsRegistered)
            {
                SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
                _displayEventsRegistered = false;
            }
            Volatile.Write(ref _displayEventDispatcher, null);
            Interlocked.Exchange(ref _displayChangeDispatchPending, 0);
            _displayChangeTimer?.Stop();
            _displayChangeTimer?.Dispose();
            _displayChangeTimer = null;
            foreach (var window in _taskbarWindows)
            {
                if (window.IsDisposed) continue;
                window.Close();
                window.Dispose();
            }
            _taskbarWindows.Clear();
            _taskbarUiThreadId = 0;
        }
    }
}
