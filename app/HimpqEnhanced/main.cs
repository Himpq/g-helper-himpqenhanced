using GHelper;
using GHelper.Mode;

namespace HimpqEnhanced
{
    public static class Main
    {
        private static readonly List<HimpqTaskbarWindow> _taskbarWindows = new();
        private static System.Windows.Forms.Timer? _initTimer;

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

            bool reuse = _taskbarWindows.Count == targets.Count &&
                         _taskbarWindows.All(w => !w.IsDisposed && w.IsFloatingMode == floatingMode) &&
                         _taskbarWindows.Select(w => w.TargetDeviceName).SequenceEqual(targets, StringComparer.OrdinalIgnoreCase);

            if (!reuse)
            {
                StopTaskbarWindow();
                foreach (string target in targets)
                    _taskbarWindows.Add(new HimpqTaskbarWindow(target));
            }

            foreach (var window in _taskbarWindows)
                window.ShowWindow();
        }

        // "All displays" mode enumerates the screens live, so hot-plugging a monitor
        // picks it up on the next restart without any stored list to keep in sync.
        private static List<string> ResolveTargetDevices(HimpqConfigData config)
        {
            if (config.taskbar_display_all == 1)
                return Screen.AllScreens.Select(screen => screen.DeviceName).ToList();

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
            foreach (var window in _taskbarWindows)
            {
                if (window.IsDisposed) continue;
                window.HideWindow();
                window.Close();
                window.Dispose();
            }
            _taskbarWindows.Clear();
        }
    }
}
