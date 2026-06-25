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
                string modeName = config.default_performance_mode switch
                {
                    0 => "平衡",
                    1 => "增强 (Turbo)",
                    2 => "静音",
                    _ => "未启用"
                };
                MessageBox.Show(
                    $"HimpqEnhanced 已启动\n默认性能模式：{modeName}",
                    "HimpqEnhanced",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (config.default_performance_mode < 0) return;

            int targetMode = config.default_performance_mode;
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
                    catch { }
                };
                _initTimer.Start();
            }
        }

        public static void StartTaskbarWindow()
        {
            if (_taskbarWindow is null || _taskbarWindow.IsDisposed)
            {
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
