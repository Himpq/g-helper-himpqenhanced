using GHelper;
using GHelper.Mode;

namespace HimpqEnhanced
{
    public static class Main
    {
        private static HimpqTaskbarWindow? _taskbarWindow;
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

            if (_taskbarWindow is null || _taskbarWindow.IsDisposed || _taskbarWindow.IsFloatingMode != floatingMode)
            {
                StopTaskbarWindow();
                _taskbarWindow = new HimpqTaskbarWindow();
            }
            _taskbarWindow.ShowWindow();
        }

        public static void ShowTaskbarWindow()
        {
            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1) return;
            StartTaskbarWindow();
        }

        public static void HideTaskbarWindow()
        {
            _taskbarWindow?.HideWindow();
        }

        public static void RefreshTaskbarPosition()
        {
            _taskbarWindow?.RefreshLayout();
        }

        public static void RefreshTaskbarLayout()
        {
            _taskbarWindow?.RefreshLayout();
        }

        public static void RefreshTaskbarTheme()
        {
            _taskbarWindow?.RefreshTheme();
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
            _taskbarWindow?.UpdateFont(fontName, fontSize);
        }

        public static void StopTaskbarWindow()
        {
            if (_taskbarWindow is not null && !_taskbarWindow.IsDisposed)
            {
                _taskbarWindow.HideWindow();
                _taskbarWindow.Close();
                _taskbarWindow.Dispose();
            }
            _taskbarWindow = null;
        }
    }
}
