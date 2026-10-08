using System.Drawing;
using System.Drawing.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GHelper;
using GHelper.Mode;
using GHelper.UI;

namespace HimpqEnhanced
{
    public class HimpqTaskbarWindow : Form
    {
        #region Win32

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_WINDOWPOSCHANGED = 0x0047;
        private const int WM_SHOWWINDOW = 0x0018;
        private const int WM_ACTIVATE = 0x0006;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int WM_NCACTIVATE = 0x0086;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int HTTRANSPARENT = -1;
        private const int MA_NOACTIVATE = 3;
        private const int GWLP_HWNDPARENT = -8;
        private const uint GW_HWNDPREV = 3;
        private const int DWMWA_CLOAKED = 14;
        private const int TopMostKeeperIntervalMs = 100;
        private const int ShellSurfaceMonitorIntervalMs = 150;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }


        #endregion

        private static readonly Dictionary<string, string> _maxPlaceholders = new()
        {
            ["{CPU_TEMP}"] = "100",
            ["{GPU_TEMP}"] = "100",
            ["{CPU_USAGE}"] = "100",
            ["{GPU_USAGE}"] = "100",
            ["{CPU_FREQ}"] = "9999",
            ["{GPU_FREQ}"] = "9999",
            ["{CPU_FREQ_GHZ}"] = "10.00",
            ["{GPU_FREQ_GHZ}"] = "10.00",
            ["{RAM_USAGE}"] = "100",
            ["{RAM_USED}"] = "99999",
            ["{VRAM_USAGE}"] = "100",
            ["{VRAM_USED}"] = "99999",
            ["{CPU_POWER}"] = "100.0",
            ["{GPU_POWER}"] = "100.0",
            ["{TOTAL_POWER}"] = "100.0",
            ["{BATTERY_POWER}"] = "100.0",
            ["{BATTERY_LEVEL}"] = "100.0",
            ["{BATTERY_HEALTH}"] = "100.0",
            ["{POWER_SOURCE}"] = "USB-C",
            ["{MODE}"] = "增强 (Turbo)",
            ["{FAN_CPU}"] = "10000",
            ["{FAN_GPU}"] = "10000",
            ["{FAN_MID}"] = "10000",
        };

        private const int MaxVisibleRows = 2;

        private System.Windows.Forms.Timer? _updateTimer;
        private float _configFontSize;
        private Font? _displayFont;
        private bool _layoutDirty = true;

        private class DisplayRow
        {
            public List<DisplayItem> Items = [];
        }

        private class DisplayItem
        {
            public string Label = "";
            public string ValueTemplate = "";
            public string ValueMax = "";
            public string ValueText = "";
        }

        private List<DisplayRow> _rows = [];
        private int[] _colWidths = [];
        private int _totalWidth;
        private int _totalHeight;
        private int _lineHeight;
        private int _interItemGap;
        private int _rowGap;
        private bool _readPower;
        private bool _readBatteryState;
        private bool _readBatteryHealth;
        private long _lastBatteryHealthRead;

        private SolidBrush _labelBrush;
        private SolidBrush _valueBrush;
        private SolidBrush _shadowBrush;
        private int _dpi = 96;
        private IntPtr _hTaskbar;
        private IntPtr _hNotify;
        private readonly bool _isWin11;
        private bool _embedded;
        private int _padding;
        private bool _darkTheme;
        private bool _textShadowEnabled;
        private System.Windows.Forms.Timer? _topMostKeeperTimer;
        private System.Windows.Forms.Timer? _shellSurfaceMonitorTimer;
        private bool _topMostKeeperSuspended;
        private bool _shellSurfaceWasActive;
        private string _lastShellSurface = "";
        private long _lastTopMostKeeperLog;
        private IntPtr _floatingOwnerTaskbar;
        public bool IsFloatingMode { get; }

        public HimpqTaskbarWindow()
        {
            var config = HimpqConfig.Load();
            IsFloatingMode = config.taskbar_window_floating_enabled == 1;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = !IsFloatingMode || config.taskbar_floating_topmost == 1;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            MinimizeBox = false;
            MaximizeBox = false;
            ControlBox = false;
            Text = "HimpqTaskbarWindow";
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            UpdateStyles();

            using var g = CreateGraphics();
            _dpi = (int)g.DpiX;
            _padding = DpiScale(2);

            _configFontSize = config.font_size > 0 ? config.font_size : 8;
            _interItemGap = DpiScale(config.inter_item_gap);
            _rowGap = DpiScale(config.row_gap);
            UpdateFont(_configFontSize);

            _darkTheme = RForm.IsDarkThemeActive();
            _labelBrush = new SolidBrush(HimpqConfig.ResolveTaskbarLabelColor(config, _darkTheme));
            _valueBrush = new SolidBrush(HimpqConfig.ResolveTaskbarValueColor(config, _darkTheme));
            _shadowBrush = new SolidBrush(HimpqConfig.ResolveTaskbarShadowColor(config, _darkTheme));
            _textShadowEnabled = HimpqConfig.ResolveTaskbarShadowEnabled(config, _darkTheme);
            _isWin11 = Environment.OSVersion.Version.Build >= 22000;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                var config = HimpqConfig.Load();
                if (IsFloatingMode && config.taskbar_floating_topmost == 1)
                    cp.ExStyle |= WS_EX_TOPMOST;
                if (!IsFloatingMode || config.taskbar_floating_click_through == 1)
                    cp.ExStyle |= WS_EX_TRANSPARENT;
                return cp;
            }
        }

        private void UpdateFont(float fontSizePt)
        {
            _displayFont?.Dispose();
            float pt = fontSizePt * _dpi / 96f;
            _displayFont = new Font("Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point);
            using var g = CreateGraphics();
            _lineHeight = Math.Max(DpiScale(16), (int)Math.Ceiling(g.MeasureString("Ag", _displayFont).Height));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            BeginInvoke(() =>
            {
                var config = HimpqConfig.Load();
                if (!IsFloatingMode)
                    EmbedIntoTaskbar();
                ApplyWindowOptions(config);
                _updateTimer = new System.Windows.Forms.Timer { Interval = GetRefreshInterval(HimpqConfig.Load()) };
                _updateTimer.Tick += OnUpdateTick;
                _updateTimer.Start();
                ConfigureShellSurfaceMonitor(config);
                _layoutDirty = true;
                UpdateData();
            });
        }

        private void EmbedIntoTaskbar()
        {
            if (IsFloatingMode || _embedded || !IsHandleCreated) return;

            _hTaskbar = GetTaskbarHandle();
            if (_hTaskbar == IntPtr.Zero) return;

            IntPtr parent;
            if (_isWin11)
            {
                _hNotify = FindWindowEx(_hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
                parent = _hTaskbar;
            }
            else
            {
                var hBar = FindWindowEx(_hTaskbar, IntPtr.Zero, "ReBarWindow32", null);
                if (hBar == IntPtr.Zero) hBar = FindWindowEx(_hTaskbar, IntPtr.Zero, "WorkerW", null);
                parent = hBar != IntPtr.Zero ? hBar : _hTaskbar;
            }

            SetParent(Handle, parent);
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
            _embedded = true;
        }

        public void ShowWindow()
        {
            BeginInvoke(() =>
            {
                if (!IsHandleCreated)
                    CreateHandle();

                if (!IsFloatingMode && !_embedded)
                {
                    EmbedIntoTaskbar();
                }
                var config = HimpqConfig.Load();
                ApplyWindowOptions(config);
                if (_updateTimer is not null && !_updateTimer.Enabled)
                    _updateTimer.Start();
                ConfigureShellSurfaceMonitor(config);
                _layoutDirty = true;
                UpdateData();
                ShowNoActivate();
                if (IsFloatingMode)
                {
                    if (config.taskbar_floating_topmost == 1)
                        ForceFloatingTopMost(config);
                    ConfigureTopMostKeeper(config);
                }
                LogFloatingWindowState("show");
            });
        }

        public void HideWindow()
        {
            BeginInvoke(() =>
            {
                base.Hide();
                _updateTimer?.Stop();
                _topMostKeeperTimer?.Stop();
                _shellSurfaceMonitorTimer?.Stop();
                _shellSurfaceWasActive = false;
                _lastShellSurface = "";
                ClearSensorFlags();
            });
        }

        public void SuspendTopMostKeeper()
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)SuspendTopMostKeeper);
                return;
            }

            _topMostKeeperSuspended = true;
            _topMostKeeperTimer?.Stop();
            _shellSurfaceMonitorTimer?.Stop();
            _updateTimer?.Stop();
        }

        public void ResumeTopMostKeeper()
        {
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)ResumeTopMostKeeper);
                return;
            }

            _topMostKeeperSuspended = false;
            var config = HimpqConfig.Load();
            if (IsFloatingMode && config.taskbar_window_enabled == 1 &&
                config.taskbar_window_floating_enabled == 1 && config.taskbar_floating_topmost == 1)
            {
                _topMostKeeperTimer ??= CreateTopMostKeeperTimer();
                if (!_topMostKeeperTimer.Enabled)
                    _topMostKeeperTimer.Start();
            }
            if (!IsFloatingMode || config.taskbar_window_enabled == 1)
            {
                _updateTimer ??= new System.Windows.Forms.Timer { Interval = GetRefreshInterval(config) };
                if (!_updateTimer.Enabled)
                    _updateTimer.Start();
                ConfigureShellSurfaceMonitor(config);
            }
        }

        public void RefreshLayout()
        {
            _layoutDirty = true;
            UpdateData();
        }

        public void RefreshTheme()
        {
            if (IsDisposed) return;
            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)RefreshTheme);
                return;
            }

            if (UpdateBrushes(HimpqConfig.Load()))
                Invalidate();
        }

        public void UpdateFont(string fontName, float fontSize)
        {
            _configFontSize = fontSize > 0 ? fontSize : 8;
            _layoutDirty = true;
            UpdateData();
        }

        private int DpiScale(int value) => (int)Math.Round(value * _dpi / 96.0);

        private IntPtr GetTaskbarHandle()
        {
            if (_hTaskbar == IntPtr.Zero || !IsWindow(_hTaskbar))
            {
                _hTaskbar = FindWindow("Shell_TrayWnd", null);
                _hNotify = IntPtr.Zero;
            }

            return _hTaskbar;
        }

        private void ShowNoActivate()
        {
            if (!Visible)
                base.Show();

            ShowWindow(Handle, SW_SHOWNOACTIVATE);
        }

        private void ForceFloatingTopMost(HimpqConfigData config)
        {
            if (!IsFloatingMode || config.taskbar_floating_topmost != 1 || !IsHandleCreated) return;

            EnsureFloatingTaskbarOwner();
            EnsureFloatingExtendedStyles(config);
            if (!TopMost)
                TopMost = true;
            uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, flags);
        }

        private void EnsureFloatingTaskbarOwner()
        {
            if (!IsFloatingMode || !IsHandleCreated) return;

            IntPtr taskbar = GetTaskbarHandle();
            if (taskbar == IntPtr.Zero || _floatingOwnerTaskbar == taskbar) return;

            if (GetWindowOwner(Handle) == taskbar)
            {
                _floatingOwnerTaskbar = taskbar;
                return;
            }

            SetWindowOwner(Handle, taskbar);
            if (GetWindowOwner(Handle) == taskbar)
            {
                _floatingOwnerTaskbar = taskbar;
                LogFloatingOwnerChanged(taskbar);
            }
            else
            {
                Logger.WriteLine("Himpq floating window: failed to set taskbar owner=" + DescribeWindow(taskbar));
            }
        }

        private static IntPtr SetWindowOwner(IntPtr hWnd, IntPtr owner)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, GWLP_HWNDPARENT, owner)
                : new IntPtr(SetWindowLongPtr32(hWnd, GWLP_HWNDPARENT, owner.ToInt32()));
        }

        private static IntPtr GetWindowOwner(IntPtr hWnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, GWLP_HWNDPARENT)
                : new IntPtr(GetWindowLongPtr32(hWnd, GWLP_HWNDPARENT));
        }

        private void ConfigureTopMostKeeper(HimpqConfigData config)
        {
            if (!IsFloatingMode ||
                !IsHandleCreated ||
                config.taskbar_window_enabled != 1 ||
                config.taskbar_window_floating_enabled != 1 ||
                config.taskbar_floating_topmost != 1)
            {
                _topMostKeeperTimer?.Stop();
                return;
            }

            _topMostKeeperTimer ??= CreateTopMostKeeperTimer();
            if (!_topMostKeeperTimer.Enabled)
                _topMostKeeperTimer.Start();
        }

        private void ConfigureShellSurfaceMonitor(HimpqConfigData config)
        {
            bool configuredFloating = config.taskbar_window_floating_enabled == 1;
            if (!IsHandleCreated ||
                config.taskbar_window_enabled != 1 ||
                configuredFloating != IsFloatingMode)
            {
                _shellSurfaceMonitorTimer?.Stop();
                _shellSurfaceWasActive = false;
                _lastShellSurface = "";
                return;
            }

            _shellSurfaceMonitorTimer ??= CreateShellSurfaceMonitorTimer();
            if (!_shellSurfaceMonitorTimer.Enabled)
                _shellSurfaceMonitorTimer.Start();
        }

        private System.Windows.Forms.Timer CreateTopMostKeeperTimer()
        {
            var timer = new System.Windows.Forms.Timer { Interval = TopMostKeeperIntervalMs };
            timer.Tick += (_, _) => KeepFloatingTopMost();
            return timer;
        }

        private System.Windows.Forms.Timer CreateShellSurfaceMonitorTimer()
        {
            var timer = new System.Windows.Forms.Timer { Interval = ShellSurfaceMonitorIntervalMs };
            timer.Tick += (_, _) => MonitorShellSurface();
            return timer;
        }

        private void KeepFloatingTopMost()
        {
            if (_topMostKeeperSuspended || !IsFloatingMode || IsDisposed || !IsHandleCreated)
            {
                _topMostKeeperTimer?.Stop();
                return;
            }

            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1 ||
                config.taskbar_window_floating_enabled != 1 ||
                config.taskbar_floating_topmost != 1)
            {
                _topMostKeeperTimer?.Stop();
                return;
            }

            if (!IsWindowVisible(Handle))
            {
                LogFloatingWindowState("keeper stopped hidden");
                _topMostKeeperTimer?.Stop();
                return;
            }

            IntPtr windowAbove = GetWindow(Handle, GW_HWNDPREV);

            ForceFloatingTopMost(config);
            if (windowAbove != IntPtr.Zero)
                LogTopMostKeeperReassert(windowAbove);
        }

        private void MonitorShellSurface()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                _shellSurfaceMonitorTimer?.Stop();
                _shellSurfaceWasActive = false;
                _lastShellSurface = "";
                return;
            }

            bool shellSurfaceActive = TryDescribeActiveTaskbarShellSurface(out string description);
            if (shellSurfaceActive)
            {
                if (!_shellSurfaceWasActive)
                    Logger.WriteLine("Himpq taskbar window: shell surface active mode=" + (IsFloatingMode ? "floating" : "embedded") + " surface=" + description);
                _shellSurfaceWasActive = true;
                _lastShellSurface = description;
                return;
            }

            if (!_shellSurfaceWasActive) return;

            _shellSurfaceWasActive = false;
            RestoreAfterShellSurfaceClosed(_lastShellSurface);
            _lastShellSurface = "";
        }

        private void RestoreAfterShellSurfaceClosed(string shellSurface)
        {
            if (IsFloatingMode)
                RestoreFloatingTaskbarWindow(shellSurface);
            else
                RestoreEmbeddedTaskbarWindow(shellSurface);
        }

        private void RestoreFloatingTaskbarWindow(string shellSurface)
        {
            if (!IsFloatingMode || IsDisposed || !IsHandleCreated) return;

            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1 || config.taskbar_window_floating_enabled != 1) return;

            if (_layoutDirty)
                UpdateData();
            else
            {
                ApplyWindowOptions(config);
                ApplyPosition(config);
            }

            if (_updateTimer is not null && !_updateTimer.Enabled)
                _updateTimer.Start();

            ShowNoActivate();
            if (config.taskbar_floating_topmost == 1)
                ForceFloatingTopMost(config);
            ConfigureTopMostKeeper(config);
            Invalidate();
            LogFloatingWindowRestore(shellSurface);
        }

        private void RestoreEmbeddedTaskbarWindow(string shellSurface)
        {
            if (IsFloatingMode || IsDisposed || !IsHandleCreated) return;

            var config = HimpqConfig.Load();
            if (config.taskbar_window_enabled != 1 || config.taskbar_window_floating_enabled == 1) return;

            if (!_embedded || _hTaskbar == IntPtr.Zero || !IsWindow(_hTaskbar))
            {
                _embedded = false;
                EmbedIntoTaskbar();
            }
            if (!_embedded) return;

            if (_layoutDirty)
                UpdateData();
            else
                ApplyPosition(config);

            ShowNoActivate();
            SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Invalidate();
            LogEmbeddedWindowRestore(shellSurface);
        }

        private static bool TryDescribeActiveTaskbarShellSurface(out string description)
        {
            IntPtr foreground = GetForegroundWindow();
            description = DescribeWindow(foreground);

            if (foreground == IntPtr.Zero || !IsWindowVisible(foreground) || IsWindowCloaked(foreground))
                return false;

            string processName = GetWindowProcessName(foreground);
            description += " process=" + processName;
            if (!IsTaskbarShellSurfaceProcess(processName))
                return false;
            if (processName == "StartMenuExperienceHost")
                return true;

            string className = GetWindowClassNameText(foreground);
            return className is "Windows.UI.Core.CoreWindow"
                or "Windows.UI.Composition.DesktopWindowContentBridge"
                or "XamlExplorerHostIslandWindow"
                or "Shell_TrayWnd";
        }

        private static bool IsTaskbarShellSurfaceProcess(string processName)
        {
            return processName is "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchHost" or "TextInputHost";
        }

        private static bool IsWindowCloaked(IntPtr hWnd)
        {
            return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
        }

        private static string GetWindowProcessName(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0) return "";

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (ArgumentException)
            {
                return "";
            }
            catch (InvalidOperationException)
            {
                return "";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "";
            }
        }

        private void LogTopMostKeeperReassert(IntPtr windowAbove)
        {
            var config = HimpqConfig.Load();
            if (config.debug_mode != 1) return;

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (Math.Abs(now - _lastTopMostKeeperLog) < 5000) return;

            _lastTopMostKeeperLog = now;
            Logger.WriteLine("Himpq floating window: topmost keeper reassert above=0x" + windowAbove.ToInt64().ToString("X"));
        }

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            try { UpdateData(); } catch { }
        }

        private void UpdateData()
        {
            if (_layoutDirty)
            {
                var config = HimpqConfig.Load();
                _configFontSize = config.font_size > 0 ? config.font_size : 8;
                _interItemGap = DpiScale(config.inter_item_gap);
                _rowGap = DpiScale(config.row_gap);
                if (_updateTimer is not null)
                    _updateTimer.Interval = GetRefreshInterval(config);
                UpdateFont(_configFontSize);
                UpdateBrushes(config);
                ApplyWindowOptions(config);

                BuildLayout(config);
                _layoutDirty = false;
                ApplyPosition(config);
            }

            if (_readBatteryState)
                HardwareControl.ReadBatteryState();

            if (_readBatteryHealth)
                RefreshBatteryHealth();

            HardwareControl.ReadSensorsOverlay();

            if (UpdateValues()) Invalidate();
        }

        private void RefreshBatteryHealth()
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            int interval = HardwareControl.batteryHealth >= 0 ? 600000 : 60000;
            if (Math.Abs(now - _lastBatteryHealthRead) < interval) return;

            _lastBatteryHealthRead = now;
            HardwareControl.RefreshBatteryHealth();
        }

        private void BuildLayout(HimpqConfigData config)
        {
            _rows.Clear();

            var items = (config.taskbar_items is { Count: > 0 } ? config.taskbar_items : DefaultItems())
                .Where(i => i.enabled).ToList();
            ApplySensorFlags(items);

            if (items.Count == 0)
            {
                _colWidths = [];
                _totalWidth = _totalHeight = 0;
                return;
            }

            for (int r = 0; r < MaxVisibleRows; r++)
                _rows.Add(new DisplayRow());

            foreach (var item in items)
            {
                int targetRow = Math.Abs(item.row) % MaxVisibleRows;
                string token = "{" + item.token + "}";
                string template = token + item.suffix;
                string maxText = GetMaxWidthText(template);
                _rows[targetRow].Items.Add(new DisplayItem
                {
                    Label = item.label,
                    ValueTemplate = template,
                    ValueMax = maxText,
                    ValueText = maxText
                });
            }

            _rows = _rows.Where(r => r.Items.Count > 0).ToList();

            int maxCols = _rows.Count > 0 ? _rows.Max(r => r.Items.Count * 2) : 0;
            _colWidths = new int[maxCols];

            for (int c = 0; c < maxCols; c++)
            {
                int maxW = 0;
                foreach (var row in _rows)
                {
                    int idx = c / 2;
                    if (idx >= row.Items.Count) continue;
                    string text = (c % 2 == 0) ? row.Items[idx].Label : row.Items[idx].ValueMax;
                    maxW = Math.Max(maxW, TextRenderer.MeasureText(text, _displayFont).Width);
                }
                _colWidths[c] = Math.Max(maxW, 4);
            }

            _totalWidth = 0;
            foreach (var row in _rows)
            {
                int x = 0;
                int maxRight = 0;
                for (int c = 0; c < row.Items.Count; c++)
                {
                    int lc = c * 2;
                    int vc = c * 2 + 1;

                    if (lc < _colWidths.Length)
                    {
                        maxRight = Math.Max(maxRight, x + _colWidths[lc]);
                        x += _colWidths[lc];
                    }

                    if (lc < _colWidths.Length - 1)
                        x += _interItemGap;

                    if (vc < _colWidths.Length)
                    {
                        maxRight = Math.Max(maxRight, x + _colWidths[vc]);
                        x += _colWidths[vc];
                    }

                    if (c < row.Items.Count - 1)
                        x += _interItemGap;
                }
                _totalWidth = Math.Max(_totalWidth, maxRight);
            }

            _totalHeight = _rows.Count * _lineHeight + Math.Max(0, _rows.Count - 1) * _rowGap;
        }

        private void ApplySensorFlags(List<TaskbarItemConfig> items)
        {
            bool readFans = items.Any(i => i.token is "FAN_CPU" or "FAN_GPU" or "FAN_MID");
            bool readUsage = items.Any(i => i.token is "CPU_USAGE" or "GPU_USAGE");
            bool readMemory = items.Any(i => i.token is "RAM_USAGE" or "RAM_USED" or "VRAM_USAGE" or "VRAM_USED");
            bool readPower = items.Any(i => i.token is "CPU_POWER" or "GPU_POWER" or "TOTAL_POWER");
            bool readFrequency = items.Any(i => i.token is "CPU_FREQ" or "GPU_FREQ" or "CPU_FREQ_GHZ" or "GPU_FREQ_GHZ");
            bool readBatteryState = items.Any(i => i.token is "BATTERY_POWER" or "BATTERY_LEVEL" or "BATTERY_HEALTH");

            HardwareControl.taskbarReadFans = readFans;
            HardwareControl.taskbarReadUsage = readUsage;
            HardwareControl.taskbarReadMemory = readMemory;
            HardwareControl.taskbarReadPower = readPower;
            HardwareControl.taskbarReadFrequency = readFrequency;

            if (readPower && !_readPower)
                HardwareControl.ResetCPUPowerCounter();

            _readPower = readPower;
            _readBatteryState = readBatteryState;
            _readBatteryHealth = items.Any(i => i.token == "BATTERY_HEALTH");
        }

        private void ClearSensorFlags()
        {
            HardwareControl.taskbarReadFans = false;
            HardwareControl.taskbarReadUsage = false;
            HardwareControl.taskbarReadMemory = false;
            HardwareControl.taskbarReadPower = false;
            HardwareControl.taskbarReadFrequency = false;
            _readPower = false;
            _readBatteryState = false;
            _readBatteryHealth = false;
        }

        private void ApplyPosition(HimpqConfigData config)
        {
            if (IsFloatingMode)
            {
                int floatingTargetW = Math.Max(_totalWidth + _padding * 2, DpiScale(16));
                int floatingTargetH = Math.Max(_totalHeight + _padding * 2, DpiScale(16));
                ApplyFloatingPosition(config, floatingTargetW, floatingTargetH);
                return;
            }

            if (!_embedded || _hTaskbar == IntPtr.Zero) return;

            GetWindowRect(_hTaskbar, out RECT tr);
            int taskbarH = tr.Bottom - tr.Top;
            int maxH = Math.Max(taskbarH - DpiScale(2), DpiScale(16));
            int targetW = _totalWidth + _padding * 2;
            int targetH = Math.Min(Math.Max(_totalHeight + _padding * 2, DpiScale(16)), maxH);

            bool left = config.taskbar_window_position != "right";
            int offset = config.taskbar_window_offset;

            int x;
            if (_isWin11 && !left && _hNotify != IntPtr.Zero)
            {
                GetWindowRect(_hNotify, out RECT nr);
                x = Math.Max(2, (nr.Left - tr.Left) - targetW - DpiScale(2));
            }
            else if (!left)
            {
                x = (tr.Right - tr.Left) - targetW - DpiScale(2);
            }
            else
            {
                x = _padding;
            }
            x += offset;

            int y = Math.Max(0, (taskbarH - targetH) / 2);

            SetWindowPos(Handle, IntPtr.Zero, x, y, targetW, targetH, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private void ApplyFloatingPosition(HimpqConfigData config, int targetW, int targetH)
        {
            int x = config.taskbar_floating_x;
            int y = config.taskbar_floating_y;

            if (config.taskbar_floating_position_initialized != 1)
            {
                Point defaultPoint = CalculateDefaultFloatingPosition(config, targetW, targetH);
                x = defaultPoint.X;
                y = defaultPoint.Y;
                config.taskbar_floating_x = x;
                config.taskbar_floating_y = y;
                config.taskbar_floating_position_initialized = 1;
                HimpqConfig.Save(config);
            }

            EnsureFloatingTaskbarOwner();
            IntPtr zOrder = config.taskbar_floating_topmost == 1 ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(Handle, zOrder, x, y, targetW, targetH, SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        private Point CalculateDefaultFloatingPosition(HimpqConfigData config, int targetW, int targetH)
        {
            Screen screen = GetTargetDisplay(config);
            Rectangle bounds = screen.Bounds;
            Rectangle workArea = screen.WorkingArea;
            bool left = config.taskbar_window_position != "right";
            int x;
            int y;
            int topTaskbarH = Math.Max(0, workArea.Top - bounds.Top);
            int bottomTaskbarH = Math.Max(0, bounds.Bottom - workArea.Bottom);
            int leftTaskbarW = Math.Max(0, workArea.Left - bounds.Left);
            int rightTaskbarW = Math.Max(0, bounds.Right - workArea.Right);
            int edgeGap = DpiScale(2);

            if (bottomTaskbarH > 0)
            {
                x = left ? workArea.Left + _padding : workArea.Right - targetW - edgeGap;
                y = workArea.Bottom + Math.Max(0, (bottomTaskbarH - targetH) / 2);
            }
            else if (topTaskbarH > 0)
            {
                x = left ? workArea.Left + _padding : workArea.Right - targetW - edgeGap;
                y = bounds.Top + Math.Max(0, (topTaskbarH - targetH) / 2);
            }
            else if (leftTaskbarW > 0)
            {
                x = bounds.Left + Math.Max(0, (leftTaskbarW - targetW) / 2);
                y = left ? workArea.Top + _padding : workArea.Bottom - targetH - edgeGap;
            }
            else if (rightTaskbarW > 0)
            {
                x = workArea.Right + Math.Max(0, (rightTaskbarW - targetW) / 2);
                y = left ? workArea.Top + _padding : workArea.Bottom - targetH - edgeGap;
            }
            else
            {
                x = left ? workArea.Left + _padding : workArea.Right - targetW - edgeGap;
                y = workArea.Bottom - targetH - edgeGap;
            }

            x += config.taskbar_window_offset;
            x = Math.Clamp(x, bounds.Left, Math.Max(bounds.Left, bounds.Right - targetW));
            y = Math.Clamp(y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - targetH));
            return new Point(x, y);
        }

        private static Screen GetTargetDisplay(HimpqConfigData config)
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0)
                return Screen.PrimaryScreen ?? throw new InvalidOperationException("No display is available.");

            return screens.FirstOrDefault(screen =>
                    string.Equals(screen.DeviceName, config.taskbar_display_device_name, StringComparison.OrdinalIgnoreCase))
                ?? Screen.PrimaryScreen
                ?? screens[0];
        }

        private void ApplyWindowOptions(HimpqConfigData config)
        {
            if (!IsHandleCreated) return;

            bool topMost = !IsFloatingMode || config.taskbar_floating_topmost == 1;
            if (IsFloatingMode)
            {
                EnsureFloatingTaskbarOwner();
                EnsureFloatingExtendedStyles(config);
                TopMost = topMost;
            }
            else
                TopMost = topMost;

            if (IsFloatingMode)
            {
                IntPtr zOrder = topMost ? HWND_TOPMOST : HWND_NOTOPMOST;
                SetWindowPos(Handle, zOrder, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                ConfigureTopMostKeeper(config);
            }

            ConfigureShellSurfaceMonitor(config);
        }

        private void EnsureFloatingExtendedStyles(HimpqConfigData config)
        {
            if (!IsFloatingMode || !IsHandleCreated) return;

            bool clickThrough = config.taskbar_floating_click_through == 1;
            bool topMost = config.taskbar_floating_topmost == 1;
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            int targetStyle = exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

            if (topMost)
                targetStyle |= WS_EX_TOPMOST;
            else
                targetStyle &= ~WS_EX_TOPMOST;

            if (clickThrough)
                targetStyle |= WS_EX_TRANSPARENT;
            else
                targetStyle &= ~WS_EX_TRANSPARENT;

            if (targetStyle != exStyle)
                SetWindowLong(Handle, GWL_EXSTYLE, targetStyle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING && IsFloatingTopMostLocked())
            {
                var pos = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                if ((pos.flags & SWP_NOZORDER) == 0 && pos.hwndInsertAfter != HWND_TOPMOST)
                {
                    LogWindowPosChange("changing", pos.hwndInsertAfter, pos.flags);
                    pos.hwndInsertAfter = HWND_TOPMOST;
                    Marshal.StructureToPtr(pos, m.LParam, false);
                }
            }

            if (m.Msg == WM_NCHITTEST && IsFloatingClickThroughEnabled())
            {
                m.Result = new IntPtr(HTTRANSPARENT);
                return;
            }

            if (m.Msg == WM_MOUSEACTIVATE && IsFloatingMode)
            {
                m.Result = new IntPtr(MA_NOACTIVATE);
                return;
            }

            if (m.Msg == WM_WINDOWPOSCHANGED && IsFloatingTopMostLocked())
            {
                var pos = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                LogWindowPosChange("changed", pos.hwndInsertAfter, pos.flags);
            }

            if (m.Msg == WM_SHOWWINDOW && IsFloatingTopMostLocked())
            {
                Logger.WriteLine("Himpq floating window: WM_SHOWWINDOW show=" + m.WParam + " reason=" + m.LParam);
            }

            if (m.Msg == WM_ACTIVATE && IsFloatingTopMostLocked())
            {
                Logger.WriteLine("Himpq floating window: WM_ACTIVATE state=0x" + m.WParam.ToInt64().ToString("X") + " other=0x" + m.LParam.ToInt64().ToString("X"));
            }

            if (m.Msg == WM_NCACTIVATE && IsFloatingTopMostLocked())
            {
                Logger.WriteLine("Himpq floating window: WM_NCACTIVATE active=" + m.WParam);
            }

            if (m.Msg == WM_DISPLAYCHANGE && IsFloatingMode && IsHandleCreated && !IsDisposed)
                BeginInvoke((Action)HandleDisplayConfigurationChanged);

            base.WndProc(ref m);
        }

        private bool IsFloatingTopMostLocked()
        {
            if (_topMostKeeperSuspended || !IsFloatingMode || !IsHandleCreated) return false;
            return HimpqConfig.Load().taskbar_floating_topmost == 1;
        }

        private void HandleDisplayConfigurationChanged()
        {
            if (!IsFloatingMode || IsDisposed || !IsHandleCreated) return;

            var config = HimpqConfig.Load();
            Screen target = GetTargetDisplay(config);
            bool windowOnTarget = false;
            if (GetWindowRect(Handle, out RECT rect))
            {
                Rectangle windowBounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                Screen current = Screen.FromRectangle(windowBounds);
                windowOnTarget = target.Bounds.Contains(windowBounds) &&
                    string.Equals(current.DeviceName, target.DeviceName, StringComparison.OrdinalIgnoreCase);
            }

            if (!windowOnTarget)
            {
                config.taskbar_floating_position_initialized = 0;
                HimpqConfig.Save(config);
            }

            _layoutDirty = true;
            UpdateData();
        }

        private void LogWindowPosChange(string phase, IntPtr insertAfter, uint flags)
        {
            if (HimpqConfig.Load().debug_mode != 1) return;

            string insertAfterText = insertAfter == HWND_TOPMOST ? "TOPMOST"
                : insertAfter == HWND_NOTOPMOST ? "NOTOPMOST"
                : insertAfter == HWND_BOTTOM ? "BOTTOM"
                : DescribeWindow(insertAfter);
            string foregroundText = DescribeWindow(GetForegroundWindow());
            Logger.WriteLine("Himpq floating window: WM_WINDOWPOS" + phase + " insertAfter=" + insertAfterText + " flags=0x" + flags.ToString("X") + " foreground=" + foregroundText);
        }

        private static string DescribeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "0x0";
            if (hWnd == HWND_TOPMOST) return "TOPMOST";
            if (hWnd == HWND_NOTOPMOST) return "NOTOPMOST";
            if (hWnd == HWND_BOTTOM) return "BOTTOM";

            string className = GetWindowClassNameText(hWnd);
            string title = GetWindowTitleText(hWnd);

            string result = "0x" + hWnd.ToInt64().ToString("X");
            if (!string.IsNullOrEmpty(className))
                result += "/" + className;
            if (!string.IsNullOrEmpty(title))
                result += ":" + title;
            return result;
        }

        private static string GetWindowClassNameText(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";

            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static string GetWindowTitleText(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";

            var title = new StringBuilder(Math.Max(2, GetWindowTextLength(hWnd) + 1));
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        private bool IsFloatingClickThroughEnabled()
        {
            return IsFloatingMode && HimpqConfig.Load().taskbar_floating_click_through == 1;
        }

        private void LogFloatingWindowState(string reason)
        {
            if (HimpqConfig.Load().debug_mode != 1) return;
            if (!IsHandleCreated) return;

            int cloaked = -1;
            int cloakedHr = DwmGetWindowAttribute(Handle, DWMWA_CLOAKED, out cloaked, sizeof(int));
            bool hasRect = GetWindowRect(Handle, out RECT rect);
            string rectText = hasRect
                ? rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom
                : "unavailable";
            string cloakedText = cloakedHr == 0
                ? cloaked.ToString()
                : "hr=0x" + cloakedHr.ToString("X8");

            Logger.WriteLine(
                "Himpq floating window: " + reason +
                " nativeVisible=" + IsWindowVisible(Handle) +
                " managedVisible=" + Visible +
                " cloaked=" + cloakedText +
                " rect=" + rectText +
                " topMost=" + TopMost +
                " nativeTopMost=" + HasFloatingTopMostStyle() +
                " owner=" + DescribeWindow(_floatingOwnerTaskbar));
        }

        private void LogFloatingOwnerChanged(IntPtr taskbar)
        {
            if (HimpqConfig.Load().debug_mode != 1) return;
            Logger.WriteLine("Himpq floating window: taskbar owner=" + DescribeWindow(taskbar));
        }

        private void LogEmbeddedWindowRestore(string shellSurface)
        {
            if (HimpqConfig.Load().debug_mode != 1) return;
            Logger.WriteLine("Himpq taskbar window: restored after shell surface=" + shellSurface + " foreground=" + DescribeWindow(GetForegroundWindow()));
        }

        private void LogFloatingWindowRestore(string shellSurface)
        {
            if (!IsHandleCreated) return;

            int cloaked = -1;
            int cloakedHr = DwmGetWindowAttribute(Handle, DWMWA_CLOAKED, out cloaked, sizeof(int));
            bool hasRect = GetWindowRect(Handle, out RECT rect);
            string rectText = hasRect
                ? rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom
                : "unavailable";
            string cloakedText = cloakedHr == 0
                ? cloaked.ToString()
                : "hr=0x" + cloakedHr.ToString("X8");

            Logger.WriteLine(
                "Himpq floating window: restored after shell surface=" + shellSurface +
                " nativeVisible=" + IsWindowVisible(Handle) +
                " managedVisible=" + Visible +
                " cloaked=" + cloakedText +
                " rect=" + rectText +
                " topMost=" + TopMost +
                " nativeTopMost=" + HasFloatingTopMostStyle() +
                " owner=" + DescribeWindow(_floatingOwnerTaskbar) +
                " foreground=" + DescribeWindow(GetForegroundWindow()));
        }

        private bool HasFloatingTopMostStyle()
        {
            if (!IsHandleCreated) return false;
            return (GetWindowLong(Handle, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            LogFloatingWindowState("handle destroyed");
            _topMostKeeperTimer?.Stop();
            _shellSurfaceMonitorTimer?.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            LogFloatingWindowState("visible changed");
        }

        private static string GetMaxWidthText(string text)
        {
            foreach (var kvp in _maxPlaceholders)
                if (text.Contains(kvp.Key))
                    return text.Replace(kvp.Key, kvp.Value);
            return text;
        }

        private static int GetRefreshInterval(HimpqConfigData config)
        {
            return Math.Clamp(config.taskbar_refresh_interval, 250, 5000);
        }

        private bool UpdateBrushes(HimpqConfigData config)
        {
            bool dark = RForm.IsDarkThemeActive();
            Color labelColor = HimpqConfig.ResolveTaskbarLabelColor(config, dark);
            Color valueColor = HimpqConfig.ResolveTaskbarValueColor(config, dark);
            Color shadowColor = HimpqConfig.ResolveTaskbarShadowColor(config, dark);
            bool shadowEnabled = HimpqConfig.ResolveTaskbarShadowEnabled(config, dark);
            bool changed = _darkTheme != dark;
            _darkTheme = dark;

            if (_labelBrush.Color.ToArgb() != labelColor.ToArgb())
            {
                _labelBrush.Dispose();
                _labelBrush = new SolidBrush(labelColor);
                changed = true;
            }

            if (_valueBrush.Color.ToArgb() != valueColor.ToArgb())
            {
                _valueBrush.Dispose();
                _valueBrush = new SolidBrush(valueColor);
                changed = true;
            }

            if (_shadowBrush.Color.ToArgb() != shadowColor.ToArgb())
            {
                _shadowBrush.Dispose();
                _shadowBrush = new SolidBrush(shadowColor);
                changed = true;
            }

            if (_textShadowEnabled != shadowEnabled)
            {
                _textShadowEnabled = shadowEnabled;
                changed = true;
            }

            return changed;
        }

        private bool UpdateValues()
        {
            bool changed = false;
            foreach (var row in _rows)
            {
                foreach (var item in row.Items)
                {
                    string replaced = ReplaceTokens(item.ValueTemplate);
                    if (item.ValueText != replaced)
                    {
                        item.ValueText = replaced;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private string ReplaceTokens(string template)
        {
            var r = template;
            r = ReplaceToken(r, "{CPU_TEMP}", HardwareControl.cpuTemp, "N0");
            r = ReplaceToken(r, "{GPU_TEMP}", HardwareControl.gpuTemp, "N0");
            r = ReplaceToken(r, "{CPU_USAGE}", HardwareControl.cpuUsage, "N0");
            r = ReplaceToken(r, "{GPU_USAGE}", HardwareControl.gpuUsage, "N0");
            r = ReplaceToken(r, "{CPU_FREQ}", HardwareControl.cpuFrequencyMHz, "N0");
            r = ReplaceToken(r, "{GPU_FREQ}", HardwareControl.gpuFrequencyMHz, "N0");
            r = ReplaceToken(r, "{CPU_FREQ_GHZ}", FrequencyGhz(HardwareControl.cpuFrequencyMHz), "F2");
            r = ReplaceToken(r, "{GPU_FREQ_GHZ}", FrequencyGhz(HardwareControl.gpuFrequencyMHz), "F2");
            r = ReplaceToken(r, "{RAM_USAGE}", HardwareControl.ramUsage, "N0");
            r = ReplaceToken(r, "{RAM_USED}", HardwareControl.ramUsedMb, "N0");
            r = ReplaceToken(r, "{VRAM_USAGE}", HardwareControl.vramUsage, "N0");
            r = ReplaceToken(r, "{VRAM_USED}", HardwareControl.vramUsedMb, "N0");
            r = ReplaceToken(r, "{CPU_POWER}", HardwareControl.cpuPower, "F1");
            r = ReplaceToken(r, "{GPU_POWER}", HardwareControl.gpuPower, "F1");
            r = ReplaceToken(r, "{TOTAL_POWER}", HardwareControl.totalPower, "F1");
            r = ReplaceToken(r, "{BATTERY_POWER}", HardwareControl.batteryRate is decimal bd ? (float?)Math.Abs((float)bd) : null, "F1");
            r = ReplaceToken(r, "{BATTERY_LEVEL}", BatteryValue(HardwareControl.batteryCapacity), "F1");
            r = ReplaceToken(r, "{BATTERY_HEALTH}", BatteryValue(HardwareControl.batteryHealth), "F1");
            r = r.Replace("{POWER_SOURCE}", GetPowerSourceText());
            r = r.Replace("{MODE}", Modes.GetCurrentName() ?? "--");
            r = r.Replace("{FAN_CPU}", HardwareControl.cpuFanRPM?.ToString() ?? "--");
            r = r.Replace("{FAN_GPU}", HardwareControl.gpuFanRPM?.ToString() ?? "--");
            r = r.Replace("{FAN_MID}", HardwareControl.midFanRPM?.ToString() ?? "--");
            return r;
        }

        private static float? BatteryValue(decimal value)
        {
            return value >= 0 ? (float)value : null;
        }

        private static float? FrequencyGhz(int? mhz)
        {
            return mhz is > 0 ? mhz.Value / 1000f : null;
        }

        private static string GetPowerSourceText()
        {
            return Program.currentSource switch
            {
                Program.PowerSource.Barrel => "AC",
                Program.PowerSource.USBC => "USB-C",
                _ => "BAT",
            };
        }

        private static string ReplaceToken(string template, string token, float? value, string format)
        {
            return template.Replace(token, value.HasValue && value.Value >= 0 ? value.Value.ToString(format) : "--");
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Suppress the separate transparent-color erase that causes visible flicker between sensor frames.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.TextRenderingHint = _darkTheme ? TextRenderingHint.ClearTypeGridFit : TextRenderingHint.AntiAliasGridFit;
            if (_rows.Count == 0 || _displayFont is null) return;

            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                int y = _padding + r * (_lineHeight + _rowGap);
                int x = _padding;

                for (int c = 0; c < row.Items.Count; c++)
                {
                    int lc = c * 2;
                    int vc = c * 2 + 1;

                    if (lc < _colWidths.Length && !string.IsNullOrEmpty(row.Items[c].Label))
                        DrawTaskbarText(e.Graphics, row.Items[c].Label, _labelBrush, x, y);

                    x += _colWidths[lc];

                    if (lc < _colWidths.Length - 1)
                        x += _interItemGap;

                    if (vc < _colWidths.Length && !string.IsNullOrEmpty(row.Items[c].ValueText))
                        DrawTaskbarText(e.Graphics, row.Items[c].ValueText, _valueBrush, x, y);

                    x += _colWidths[vc];

                    if (c < row.Items.Count - 1)
                        x += _interItemGap;
                }
            }
        }

        private void DrawTaskbarText(Graphics graphics, string text, Brush textBrush, int x, int y)
        {
            if (_displayFont is null) return;
            if (_textShadowEnabled)
                graphics.DrawString(text, _displayFont, _shadowBrush, x + DpiScale(1), y + DpiScale(1));
            graphics.DrawString(text, _displayFont, textBrush, x, y);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _topMostKeeperTimer?.Stop();
            _topMostKeeperTimer?.Dispose();
            _shellSurfaceMonitorTimer?.Stop();
            _shellSurfaceMonitorTimer?.Dispose();
            ClearSensorFlags();
            _displayFont?.Dispose();
            _labelBrush.Dispose();
            _valueBrush.Dispose();
            _shadowBrush.Dispose();
            base.OnFormClosing(e);
        }

        private static List<TaskbarItemConfig> DefaultItems() => HimpqConfig.DefaultItems();
    }
}
