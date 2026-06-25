using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using GHelper;
using GHelper.UI;

namespace HimpqEnhanced
{
    public class HimpqTaskbarWindow : Form
    {
        #region Win32

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        #endregion

        private static readonly Dictionary<string, string> _maxPlaceholders = new()
        {
            ["{CPU_TEMP}"] = "100",
            ["{GPU_TEMP}"] = "100",
            ["{CPU_USAGE}"] = "100",
            ["{GPU_USAGE}"] = "100",
            ["{RAM_USAGE}"] = "100",
            ["{CPU_POWER}"] = "100.0",
            ["{GPU_POWER}"] = "100.0",
            ["{BATTERY_POWER}"] = "100.0",
            ["{FAN_CPU}"] = "10000",
            ["{FAN_GPU}"] = "10000",
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
        private bool _readBatteryPower;

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

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                var config = HimpqConfig.Load();
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
                _layoutDirty = true;
                UpdateData();
            });
        }

        private void EmbedIntoTaskbar()
        {
            if (IsFloatingMode || _embedded || !IsHandleCreated) return;

            _hTaskbar = FindWindow("Shell_TrayWnd", null);
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
                ApplyWindowOptions(HimpqConfig.Load());
                ShowWindow(Handle, SW_SHOWNOACTIVATE);
                if (_updateTimer is not null && !_updateTimer.Enabled)
                    _updateTimer.Start();
                _layoutDirty = true;
                UpdateData();
            });
        }

        public void HideWindow()
        {
            BeginInvoke(() =>
            {
                base.Hide();
                _updateTimer?.Stop();
                ClearSensorFlags();
            });
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

            if (_readBatteryPower)
                HardwareControl.ReadBatteryState();

            HardwareControl.ReadSensorsOverlay();

            if (UpdateValues()) Invalidate();
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
            bool readFans = items.Any(i => i.token is "FAN_CPU" or "FAN_GPU");
            bool readUsage = items.Any(i => i.token is "CPU_USAGE" or "GPU_USAGE");
            bool readMemory = items.Any(i => i.token == "RAM_USAGE");
            bool readPower = items.Any(i => i.token is "CPU_POWER" or "GPU_POWER");

            HardwareControl.taskbarReadFans = readFans;
            HardwareControl.taskbarReadUsage = readUsage;
            HardwareControl.taskbarReadMemory = readMemory;
            HardwareControl.taskbarReadPower = readPower;

            if (readPower && !_readPower)
                HardwareControl.ResetCPUPowerCounter();

            _readPower = readPower;
            _readBatteryPower = items.Any(i => i.token == "BATTERY_POWER");
        }

        private void ClearSensorFlags()
        {
            HardwareControl.taskbarReadFans = false;
            HardwareControl.taskbarReadUsage = false;
            HardwareControl.taskbarReadMemory = false;
            HardwareControl.taskbarReadPower = false;
            _readPower = false;
            _readBatteryPower = false;
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

            IntPtr zOrder = config.taskbar_floating_topmost == 1 ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(Handle, zOrder, x, y, targetW, targetH, SWP_NOACTIVATE);
        }

        private Point CalculateDefaultFloatingPosition(HimpqConfigData config, int targetW, int targetH)
        {
            IntPtr taskbar = _hTaskbar != IntPtr.Zero ? _hTaskbar : FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out RECT tr))
            {
                Logger.WriteLine("Himpq floating window: taskbar handle not found for initial position");
                return new Point(config.taskbar_floating_x, config.taskbar_floating_y);
            }

            bool left = config.taskbar_window_position != "right";
            int offset = config.taskbar_window_offset;
            int x;

            if (_isWin11 && !left)
            {
                IntPtr notify = _hNotify != IntPtr.Zero ? _hNotify : FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
                if (notify != IntPtr.Zero && GetWindowRect(notify, out RECT nr))
                    x = Math.Max(tr.Left + DpiScale(2), nr.Left - targetW - DpiScale(2));
                else
                    x = tr.Right - targetW - DpiScale(2);
            }
            else if (!left)
            {
                x = tr.Right - targetW - DpiScale(2);
            }
            else
            {
                x = tr.Left + _padding;
            }

            x += offset;

            int taskbarH = tr.Bottom - tr.Top;
            int y = tr.Top + Math.Max(0, (taskbarH - targetH) / 2);
            return new Point(x, y);
        }

        private void ApplyWindowOptions(HimpqConfigData config)
        {
            if (!IsHandleCreated) return;

            bool topMost = !IsFloatingMode || config.taskbar_floating_topmost == 1;
            TopMost = topMost;

            bool clickThrough = !IsFloatingMode || config.taskbar_floating_click_through == 1;
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            int targetStyle = clickThrough
                ? exStyle | WS_EX_TRANSPARENT
                : exStyle & ~WS_EX_TRANSPARENT;

            if (targetStyle != exStyle)
                SetWindowLong(Handle, GWL_EXSTYLE, targetStyle);

            if (IsFloatingMode)
            {
                IntPtr zOrder = topMost ? HWND_TOPMOST : HWND_NOTOPMOST;
                SetWindowPos(Handle, zOrder, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
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
            r = ReplaceToken(r, "{RAM_USAGE}", HardwareControl.ramUsage, "N0");
            r = ReplaceToken(r, "{CPU_POWER}", HardwareControl.cpuPower, "F1");
            r = ReplaceToken(r, "{GPU_POWER}", HardwareControl.gpuPower, "F1");
            r = ReplaceToken(r, "{BATTERY_POWER}", HardwareControl.batteryRate is decimal bd ? (float?)Math.Abs((float)bd) : null, "F1");
            r = r.Replace("{FAN_CPU}", HardwareControl.cpuFanRPM?.ToString() ?? "--");
            r = r.Replace("{FAN_GPU}", HardwareControl.gpuFanRPM?.ToString() ?? "--");
            return r;
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
