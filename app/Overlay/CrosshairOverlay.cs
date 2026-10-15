using GHelper.Helpers;
using Microsoft.Win32;

namespace GHelper.Overlay
{
    public sealed class CrosshairOverlay : IDisposable
    {
        private const int CanvasSize = 80;
        private const int MinSize = 8;
        private const int MaxSize = 64;

        private readonly object gate = new();
        private readonly List<CrosshairWindow> windows = new();
        private bool running;
        private bool disposed;

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
                    foreach (CrosshairWindow window in windows)
                        window.Update(color, size, type, centerDot);
                }
                finally
                {
                    RestoreDpiContext(previousDpiContext);
                }
            }
        }

        public void RefreshScreens()
        {
            RefreshDisplays();
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
                settingsForm.BeginInvoke((Action)RefreshDisplays);
                return;
            }

            RefreshDisplays();
        }

        private void RefreshDisplays()
        {
            lock (gate)
            {
                if (!running || disposed) return;

                RecreateWindows();
                Logger.WriteLine($"Crosshair overlay refreshed on {windows.Count} display(s).");
            }
        }

        private void RecreateWindows()
        {
            if (!TryEnterPerMonitorDpiContext(out nint previousDpiContext)) return;
            try
            {
                CloseWindows();

                Color color = GetConfiguredColor();
                int size = GetConfiguredSize();
                int type = GetConfiguredType();
                bool centerDot = IsCenterDotEnabled();
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
                    return;
                }

                foreach (Screen screen in targetScreens)
                {
                    var window = new CrosshairWindow(color, size, type, centerDot);
                    Point center = new(
                        screen.Bounds.Left + screen.Bounds.Width / 2,
                        screen.Bounds.Top + screen.Bounds.Height / 2);
                    Point location = new(center.X - CanvasSize / 2, center.Y - CanvasSize / 2);
                    window.Location = location;
                    windows.Add(window);
                    window.Show();
                    Logger.WriteLine($"Crosshair display={screen.DeviceName} bounds={screen.Bounds} center={center} window={location} size={CanvasSize}.");
                }
            }
            finally
            {
                RestoreDpiContext(previousDpiContext);
            }
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
            private Color color;
            private int size;
            private int type;
            private bool centerDot;

            public CrosshairWindow(Color color, int size, int type, bool centerDot)
            {
                this.color = color;
                this.size = size;
                this.type = type;
                this.centerDot = centerDot;
                Size = new Size(CanvasSize, CanvasSize);
                Alpha = 255;
            }

            public void Update(Color color, int size, int type, bool centerDot)
            {
                this.color = color;
                this.size = size;
                this.type = type;
                this.centerDot = centerDot;
                Invalidate();
            }

            protected override void PerformPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float centerX = CanvasSize / 2f;
                float centerY = CanvasSize / 2f;
                float radius = size / 2f;
                int thickness = Math.Clamp(size / 18, 1, 3);

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
                    float gap = Math.Max(3, thickness * 2);
                    float halfThickness = thickness / 2f;
                    DrawArm(e.Graphics, new RectangleF(centerX - radius, centerY - halfThickness, radius - gap, thickness), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX + gap, centerY - halfThickness, radius - gap, thickness), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX - halfThickness, centerY - radius, thickness, radius - gap), outlineBrush, colorBrush);
                    DrawArm(e.Graphics, new RectangleF(centerX - halfThickness, centerY + gap, thickness, radius - gap), outlineBrush, colorBrush);
                }

                if (centerDot)
                {
                    int dotSize = Math.Clamp(size / 10, 4, 8);
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
