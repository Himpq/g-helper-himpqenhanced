using GHelper.Helpers;
using Microsoft.Win32;

namespace GHelper.Overlay
{
    public sealed class CrosshairOverlay : IDisposable
    {
        private const int CanvasSize = 80;
        private const int MinSize = 8;
        private const int MaxSize = 64;
        public const int MinDotSize = 1;
        public const int MaxDotSize = 16;
        public const int MinLineThickness = 1;
        public const int MaxLineThickness = 8;

        private readonly object gate = new();
        private readonly List<CrosshairWindow> windows = new();
        private bool running;
        private bool disposed;
        private System.Windows.Forms.Timer? refreshDebounceTimer;
        private string lastScreenSignature = "";

        public static Color GetConfiguredColor()
        {
            Color configured = Color.FromArgb(AppConfig.Get("crosshair_color", Color.Lime.ToArgb()));
            return Color.FromArgb(255, configured.R, configured.G, configured.B);
        }

        public static int GetConfiguredSize()
        {
            return Math.Clamp(AppConfig.Get("crosshair_size", 32), MinSize, MaxSize);
        }

        public static int GetConfiguredType()
        {
            return Math.Clamp(AppConfig.Get("crosshair_type", 0), 0, 1);
        }

        public static int GetConfiguredDotSize()
        {
            int defaultSize = Math.Clamp(GetConfiguredSize() / 10, 4, 8);
            return Math.Clamp(AppConfig.Get("crosshair_dot_size", defaultSize), MinDotSize, MaxDotSize);
        }

        public static int GetConfiguredLineThickness()
        {
            int defaultThickness = Math.Clamp(GetConfiguredSize() / 18, 1, 3);
            return Math.Clamp(AppConfig.Get("crosshair_line_thickness", defaultThickness), MinLineThickness, MaxLineThickness);
        }

        public static bool IsCenterDotEnabled()
        {
            return AppConfig.Get("crosshair_center_dot", 1) != 0;
        }

        public void Start()
        {
            lock (gate)
            {
                if (disposed || running) return;

                running = true;
                SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
                RecreateWindows();
                Logger.WriteLine($"Crosshair overlay started on {windows.Count} display(s).");
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                if (!running) return;

                running = false;
                SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
                refreshDebounceTimer?.Stop();
                CloseWindows();
                Logger.WriteLine("Crosshair overlay stopped.");
            }
        }

        public void UpdateSettings()
        {
            lock (gate)
            {
                if (!running) return;

                if (!TryEnterPerMonitorDpiContext(out nint previousDpiContext)) return;
                try
                {
                    Color color = GetConfiguredColor();
                    int size = GetConfiguredSize();
                    int type = GetConfiguredType();
                    bool centerDot = IsCenterDotEnabled();
                    int dotSize = GetConfiguredDotSize();
                    int lineThickness = GetConfiguredLineThickness();
                    foreach (CrosshairWindow window in windows)
                        window.Update(color, size, type, centerDot, dotSize, lineThickness);
                }
                finally
                {
                    RestoreDpiContext(previousDpiContext);
                }
            }
        }

        public void RefreshScreens()
        {
            RefreshDisplays(force: true);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;

                if (running)
                    SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;

                running = false;
                disposed = true;
                refreshDebounceTimer?.Stop();
                refreshDebounceTimer?.Dispose();
                refreshDebounceTimer = null;
                CloseWindows();
            }
        }

        private void DisplaySettingsChanged(object? sender, EventArgs e)
        {
            var settingsForm = Program.settingsForm;
            if (settingsForm is null || settingsForm.IsDisposed || !settingsForm.IsHandleCreated)
                return;

            if (settingsForm.InvokeRequired)
            {
                settingsForm.BeginInvoke((Action)ScheduleRefresh);
                return;
            }

            ScheduleRefresh();
        }

        // A game switching to fullscreen (or back) fires DisplaySettingsChanged several
        // times in a row. Rebuilding the topmost windows for every event adds avoidable
        // window churn, so collapse the burst into a single refresh.
        private void ScheduleRefresh()
        {
            if (!running || disposed) return;

            if (refreshDebounceTimer is null)
            {
                refreshDebounceTimer = new System.Windows.Forms.Timer { Interval = 800 };
                refreshDebounceTimer.Tick += (_, _) =>
                {
                    refreshDebounceTimer!.Stop();
                    RefreshDisplays();
                };
            }

            refreshDebounceTimer.Stop();
            refreshDebounceTimer.Start();
        }

        private void RefreshDisplays(bool force = false)
        {
            lock (gate)
            {
                if (!running || disposed) return;

                if (RecreateWindows(skipIfUnchanged: !force))
                    Logger.WriteLine($"Crosshair overlay refreshed on {windows.Count} display(s).");
            }
        }

        private bool RecreateWindows(bool skipIfUnchanged = false)
        {
            if (!TryEnterPerMonitorDpiContext(out nint previousDpiContext)) return false;
            try
            {
                string signature = BuildScreenSignature();
                if (skipIfUnchanged && signature == lastScreenSignature && windows.Count > 0)
                    return false;

                CloseWindows();

                Color color = GetConfiguredColor();
                int size = GetConfiguredSize();
                int type = GetConfiguredType();
                bool centerDot = IsCenterDotEnabled();
                int dotSize = GetConfiguredDotSize();
                int lineThickness = GetConfiguredLineThickness();
                Screen[] availableScreens = Screen.AllScreens;
                string selectedDeviceName = AppConfig.GetString("crosshair_screen", "") ?? "";
                Screen[] targetScreens = string.IsNullOrWhiteSpace(selectedDeviceName)
                    ? availableScreens
                    : availableScreens.Where(screen => string.Equals(
                        screen.DeviceName,
                        selectedDeviceName,
                        StringComparison.OrdinalIgnoreCase)).ToArray();

                if (targetScreens.Length == 0)
                {
                    string availableNames = string.Join(", ", availableScreens.Select(screen => screen.DeviceName));
                    Logger.WriteLine(string.IsNullOrWhiteSpace(selectedDeviceName)
                        ? "Crosshair overlay was not created because Windows reports no displays."
                        : $"Crosshair overlay was not created because selected display '{selectedDeviceName}' is unavailable. Connected displays: {availableNames}");
                    lastScreenSignature = "";
                    return false;
                }

                lastScreenSignature = signature;

                foreach (Screen screen in targetScreens)
                {
                    var window = new CrosshairWindow(color, size, type, centerDot, dotSize, lineThickness);
                    Point center = new(
                        screen.Bounds.Left + screen.Bounds.Width / 2,
                        screen.Bounds.Top + screen.Bounds.Height / 2);
                    Point location = new(center.X - CanvasSize / 2, center.Y - CanvasSize / 2);
                    window.Location = location;
                    windows.Add(window);
                    window.Show();
                    Logger.WriteLine($"Crosshair display={screen.DeviceName} bounds={screen.Bounds} center={center} window={location} size={CanvasSize}.");
                }

                return true;
            }
            finally
            {
                RestoreDpiContext(previousDpiContext);
            }
        }

        // Identifies the display layout the crosshair is currently anchored to. When a
        // display-change event does not actually change any target screen, the existing
        // windows are still correct and do not need to be recreated.
        private static string BuildScreenSignature()
        {
            string selectedDeviceName = AppConfig.GetString("crosshair_screen", "") ?? "";
            Screen[] screens = string.IsNullOrWhiteSpace(selectedDeviceName)
                ? Screen.AllScreens
                : Screen.AllScreens.Where(screen => string.Equals(
                    screen.DeviceName,
                    selectedDeviceName,
                    StringComparison.OrdinalIgnoreCase)).ToArray();

            return selectedDeviceName + "|" + string.Join("|", screens
                .OrderBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
                .Select(screen => screen.DeviceName + ":" + screen.Bounds + ":" + screen.Primary));
        }

        private static bool TryEnterPerMonitorDpiContext(out nint previousDpiContext)
        {
            previousDpiContext = User32.SetThreadDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            if (previousDpiContext != nint.Zero) return true;

            Logger.WriteLine("Crosshair overlay could not enter per-monitor DPI awareness; display coordinates were not used.");
            return false;
        }

        private static void RestoreDpiContext(nint previousDpiContext)
        {
            if (User32.SetThreadDpiAwarenessContext(previousDpiContext) == nint.Zero)
                Logger.WriteLine("Crosshair overlay could not restore the previous DPI awareness context.");
        }

        private void CloseWindows()
        {
            foreach (CrosshairWindow window in windows)
                window.Close();
            windows.Clear();
        }

        private sealed class CrosshairWindow : OSDNativeForm
        {
            private const int WmActivate = 0x0006;
            private const int WmSetFocus = 0x0007;
            private const int WmMouseActivate = 0x0021;
            private const int WmNcHitTest = 0x0084;
            private const int MaNoActivate = 3;
            private const int HtTransparent = -1;

            private Color color;
            private int size;
            private int type;
            private bool centerDot;
            private int dotSize;
            private int lineThickness;
            private long lastMouseActivationLog;
            private long lastFocusLog;

            protected override bool AcceptsInput => false;

            public CrosshairWindow(Color color, int size, int type, bool centerDot, int dotSize, int lineThickness)
            {
                this.color = color;
                this.size = size;
                this.type = type;
                this.centerDot = centerDot;
                this.dotSize = dotSize;
                this.lineThickness = lineThickness;
                Size = new Size(CanvasSize, CanvasSize);
                Alpha = 255;
            }

            public void Update(Color color, int size, int type, bool centerDot, int dotSize, int lineThickness)
            {
                this.color = color;
                this.size = size;
                this.type = type;
                this.centerDot = centerDot;
                this.dotSize = dotSize;
                this.lineThickness = lineThickness;
                Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmNcHitTest)
                {
                    m.Result = (nint)HtTransparent;
                    return;
                }

                if (m.Msg == WmMouseActivate)
                {
                    // Hover activation can bypass WS_EX_NOACTIVATE and ask this directly.
                    m.Result = (nint)MaNoActivate;
                    long now = Environment.TickCount64;
                    if (lastMouseActivationLog == 0 || now - lastMouseActivationLog >= 5000)
                    {
                        lastMouseActivationLog = now;
                        Logger.WriteLine($"Crosshair mouse activation refused: hwnd=0x{m.HWnd:X}, source=0x{m.WParam:X}, details=0x{m.LParam:X}.");
                    }
                    return;
                }

                if ((m.Msg == WmActivate && (m.WParam.ToInt64() & 0xFFFF) != 0) || m.Msg == WmSetFocus)
                {
                    long now = Environment.TickCount64;
                    if (lastFocusLog == 0 || now - lastFocusLog >= 5000)
                    {
                        lastFocusLog = now;
                        Logger.WriteLine($"Crosshair unexpected focus: message=0x{m.Msg:X}, hwnd=0x{m.HWnd:X}, state=0x{m.WParam:X}, previous=0x{m.LParam:X}, position={Location}.");
                    }
                }

                base.WndProc(ref m);
            }

            protected override void PerformPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float centerX = CanvasSize / 2f;
                float centerY = CanvasSize / 2f;
                float radius = size / 2f;
                int thickness = lineThickness;

                using var outlineBrush = new SolidBrush(Color.FromArgb(210, Color.Black));
                using var colorBrush = new SolidBrush(color);

                if (type == 1)
                {
                    using var outlinePen = new Pen(outlineBrush, thickness + 2);
                    using var colorPen = new Pen(colorBrush, thickness);
                    var circle = new RectangleF(centerX - radius, centerY - radius, size, size);
                    e.Graphics.DrawEllipse(outlinePen, circle);
                    e.Graphics.DrawEllipse(colorPen, circle);
                }
                else
                {
                    // Keep the center gap unchanged when only the line width changes.
                    float gap = Math.Max(3, Math.Clamp(size / 18, 1, 3) * 2);
                    float halfThickness = thickness / 2f;
                    DrawArm(e.Graphics, new RectangleF(centerX - radius, centerY - halfThickness, radius - gap, thickness), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX + gap, centerY - halfThickness, radius - gap, thickness), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX - halfThickness, centerY - radius, thickness, radius - gap), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX - halfThickness, centerY + gap, thickness, radius - gap), outlineBrush, colorBrush);
                }

                if (centerDot)
                {
                    var dot = new RectangleF(centerX - dotSize / 2f, centerY - dotSize / 2f, dotSize, dotSize);
                    e.Graphics.FillEllipse(outlineBrush, dot.X - 1, dot.Y - 1, dot.Width + 2, dot.Height + 2);
                    e.Graphics.FillEllipse(colorBrush, dot);
                }
            }

            private static void DrawArm(Graphics graphics, RectangleF arm, Brush outlineBrush, Brush colorBrush)
            {
                graphics.FillRectangle(outlineBrush, arm.X - 1, arm.Y - 1, arm.Width + 2, arm.Height + 2);
                graphics.FillRectangle(colorBrush, arm);
            }
        }
    }
}
