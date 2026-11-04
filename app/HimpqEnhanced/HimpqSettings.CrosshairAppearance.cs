using GHelper;
using GHelper.Overlay;
using GHelper.UI;
using System.Globalization;

namespace HimpqEnhanced
{
    public partial class HimpqSettings
    {
        private Label labelCrosshairDotSize = null!;
        private RTrackBar trackCrosshairDotSize = null!;
        private Label valueCrosshairDotSize = null!;
        private Label labelCrosshairLineThickness = null!;
        private RTrackBar trackCrosshairLineThickness = null!;
        private Label valueCrosshairLineThickness = null!;

        private void InitCrosshairAppearanceControls()
        {
            (labelCrosshairDotSize, trackCrosshairDotSize, valueCrosshairDotSize) =
                CreateCrosshairAppearanceSlider("CrosshairDotSize", "crosshair_dot_size",
                    CrosshairOverlay.MinDotSize, CrosshairOverlay.MaxDotSize);
            (labelCrosshairLineThickness, trackCrosshairLineThickness, valueCrosshairLineThickness) =
                CreateCrosshairAppearanceSlider("CrosshairLineThickness", "crosshair_line_thickness",
                    CrosshairOverlay.MinLineThickness, CrosshairOverlay.MaxLineThickness);

            Controls.AddRange([
                labelCrosshairDotSize, trackCrosshairDotSize, valueCrosshairDotSize,
                labelCrosshairLineThickness, trackCrosshairLineThickness, valueCrosshairLineThickness
            ]);
        }

        private (Label Title, RTrackBar Slider, Label Value) CreateCrosshairAppearanceSlider(
            string resourceKey, string configKey, int minimum, int maximum)
        {
            var title = CreateCrosshairLabel(resourceKey);
            var slider = new RTrackBar
            {
                Minimum = minimum,
                Maximum = maximum,
                TickFrequency = 1,
                TickStyle = TickStyle.BottomRight,
                Size = new Size(270, 44),
                AccessibleName = CrosshairText.Get(resourceKey)
            };
            var value = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Size = new Size(100, 34)
            };
            slider.ValueChanged += (_, _) =>
            {
                if (_loading) return;
                AppConfig.Set(configKey, slider.Value);
                UpdateCrosshairAppearanceValue(slider, value);
                Program.crosshairOverlay?.UpdateSettings();
            };
            return (title, slider, value);
        }

        private void LoadCrosshairAppearanceConfig()
        {
            bool wasLoading = _loading;
            _loading = true;
            try
            {
                trackCrosshairDotSize.Value = CrosshairOverlay.GetConfiguredDotSize();
                trackCrosshairLineThickness.Value = CrosshairOverlay.GetConfiguredLineThickness();
                UpdateCrosshairAppearanceValue(trackCrosshairDotSize, valueCrosshairDotSize);
                UpdateCrosshairAppearanceValue(trackCrosshairLineThickness, valueCrosshairLineThickness);
                UpdateCrosshairAppearanceEnabled();
            }
            finally
            {
                _loading = wasLoading;
            }
        }

        private static void UpdateCrosshairAppearanceValue(RTrackBar slider, Label value)
        {
            value.Text = string.Format(CultureInfo.CurrentUICulture,
                CrosshairText.Get("CrosshairPixelsValue"), slider.Value);
        }

        private void UpdateCrosshairAppearanceEnabled()
        {
            bool enabled = checkCrosshairCenterDot.Checked;
            labelCrosshairDotSize.Enabled = enabled;
            trackCrosshairDotSize.Enabled = enabled;
            valueCrosshairDotSize.Enabled = enabled;
        }

        private int ArrangeCrosshairAppearanceControls(int y, int labelX, int controlX, int labelW, int controlH)
        {
            y = PlaceCrosshairSliderRow(labelCrosshairDotSize, trackCrosshairDotSize, valueCrosshairDotSize,
                y, labelX, controlX, labelW, controlH);
            return PlaceCrosshairSliderRow(labelCrosshairLineThickness, trackCrosshairLineThickness, valueCrosshairLineThickness,
                y, labelX, controlX, labelW, controlH);
        }

        private static int PlaceCrosshairSliderRow(Label title, RTrackBar slider, Label value,
            int y, int labelX, int controlX, int labelW, int controlH)
        {
            title.Location = new Point(labelX, y);
            title.Size = new Size(labelW, controlH);
            slider.Location = new Point(controlX, y - 4);
            slider.Size = new Size(270, 44);
            value.Location = new Point(controlX + 280, y);
            value.Size = new Size(100, controlH);
            // Native TrackBar.AutoSize grows with DPI, so use its actual bottom.
            return Math.Max(slider.Bottom, value.Bottom) + 12;
        }
    }
}
