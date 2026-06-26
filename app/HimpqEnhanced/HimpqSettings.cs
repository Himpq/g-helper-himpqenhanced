using GHelper;
using GHelper.Mode;
using GHelper.UI;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HimpqEnhanced
{
    public partial class HimpqSettings : RForm
    {
        private const string SectionTitleTag = "SectionTitle";
        private const string ColorSwatchTag = "ColorSwatch";
        private CheckBox checkDebug;
        private CheckBox checkTaskbarEnabled;
        private CheckBox checkTaskbarFloating = null!;
        private RComboBox comboDefaultMode;
        private RComboBox comboUnplugMode;
        private RComboBox comboTaskbarPosition;
        private bool _loading;

        private RNumericUpDown numFontSize;
        private RNumericUpDown numOffset;
        private RNumericUpDown numFloatingX = null!;
        private RNumericUpDown numFloatingY = null!;
        private RNumericUpDown numInterItemGap;
        private RNumericUpDown numRowGap;
        private RNumericUpDown numRefreshInterval;
        private CheckBox checkTaskbarTextShadow;
        private CheckBox checkFloatingClickThrough = null!;
        private CheckBox checkFloatingTopmost = null!;
        private Panel panelTaskbarLabelColor = null!;
        private Panel panelTaskbarValueColor = null!;
        private Panel panelTaskbarShadowColor = null!;
        private Panel rowTaskbarLabelColor = null!;
        private Panel rowTaskbarValueColor = null!;
        private Panel rowTaskbarShadowColor = null!;
        private RButton btnResetTaskbarColors = null!;

        private readonly (int mode, string label)[] modes = new[]
        {
            (0, "平衡"),
            (1, "增强 (Turbo)"),
            (2, "静音")
        };

        private RComboBox[] powerPlanCombos;
        private TextBox[] powerPlanCustom;
        private Label labelCurrent;
        private System.Windows.Forms.Timer refreshTimer;

        private List<(string guid, string name)> allPowerPlans;

        public HimpqSettings()
        {
            Text = "Himpq 设置";
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(800, 920);
            MinimumSize = new Size(700, 400);
            Padding = new Padding(10, 10, 10, 10);
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
                else
                    DisposeRefreshTimer();
            };

            allPowerPlans = EnumeratePowerPlans();

            _loading = true;
            InitControls();
            LoadConfig();
            _loading = false;
            InitTheme(true);
            ApplySettingsVisualLayout();

            refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            refreshTimer.Tick += (_, _) =>
            {
                if (!Visible) return;
                labelCurrent.Text = "当前电源计划: " + GetActivePlanName();
            };
            refreshTimer.Start();
        }

        private void DisposeRefreshTimer()
        {
            if (refreshTimer is null) return;

            refreshTimer.Stop();
            refreshTimer.Dispose();
            refreshTimer = null;
        }

        public new bool InitTheme(bool setDPI = false)
        {
            bool changed = base.InitTheme(setDPI);
            if (Controls.Count > 0)
                ApplySettingsVisualLayout();
            return changed;
        }

        private static List<(string guid, string name)> EnumeratePowerPlans()
        {
            var plans = new List<(string, string)>();
            try
            {
                var proc = Process.Start(new ProcessStartInfo("powercfg", "/list")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    var regex = new Regex(@"GUID:\s*([a-fA-F0-9\-]+)\s*\((.+)\)", RegexOptions.Multiline);
                    foreach (Match m in regex.Matches(output))
                        plans.Add((m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim()));
                }
            }
            catch { }
            return plans;
        }

        private static void PopulateModeCombo(RComboBox combo, string emptyText)
        {
            combo.Items.Add(new ModeItem(emptyText, -1));
            foreach (var mode in Modes.GetDictonary())
                combo.Items.Add(new ModeItem(mode.Value, mode.Key));

            combo.DisplayMember = "Text";
            combo.ValueMember = "Value";
        }

        private void InitControls()
        {
            int y = 20;
            int leftLabel = 20;
            int leftControl = 210;
            int controlW = 360;

            // ── Himpq 增强设置 ──
            var title = new Label
            {
                Text = "Himpq 增强设置",
                Location = new Point(leftLabel, y),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Tag = SectionTitleTag,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(title);
            y += 50;

            // 调试弹窗
            var labelDebug = new Label
            {
                Text = "调试弹窗",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkDebug = new CheckBox
            {
                Text = "启动时显示调试弹窗",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkDebug.CheckedChanged += (_, _) => SaveHimpqConfig();
            Controls.Add(labelDebug);
            Controls.Add(checkDebug);
            y += 50;

            // 默认性能模式
            var labelDefaultMode = new Label
            {
                Text = "默认性能模式",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            comboDefaultMode = new RComboBox
            {
                NativeHeight = true,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            PopulateModeCombo(comboDefaultMode, "不启用");
            comboDefaultMode.SelectedIndexChanged += (_, _) => SaveHimpqConfig();
            Controls.Add(labelDefaultMode);
            Controls.Add(comboDefaultMode);
            y += 50;

            // 断电性能模式
            var labelUnplugMode = new Label
            {
                Text = "断电性能模式",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            comboUnplugMode = new RComboBox
            {
                NativeHeight = true,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            PopulateModeCombo(comboUnplugMode, "不切换");
            comboUnplugMode.SelectedIndexChanged += (_, _) => SaveHimpqConfig();
            Controls.Add(labelUnplugMode);
            Controls.Add(comboUnplugMode);
            y += 60;

            // ── 各模式电源计划 ──
            var sep1 = new Label
            {
                Text = "────────────────────────────────────────────────────",
                Location = new Point(leftLabel, y),
                Size = new Size(700, 16),
                ForeColor = SystemColors.ControlDark
            };
            Controls.Add(sep1);
            y += 20;

            var titlePower = new Label
            {
                Text = "各模式电源计划",
                Location = new Point(leftLabel, y),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Tag = SectionTitleTag,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(titlePower);
            y += 40;

            powerPlanCombos = new RComboBox[modes.Length];
            powerPlanCustom = new TextBox[modes.Length];

            for (int i = 0; i < modes.Length; i++)
            {
                int modeIdx = i;
                var label = new Label
                {
                    Text = modes[i].label,
                    Location = new Point(leftLabel, y),
                    Size = new Size(180, 30),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var combo = new RComboBox
                {
                    NativeHeight = true,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(leftControl, y + 2),
                    Size = new Size(controlW - 120, 26)
                };
                combo.Items.Add(new PowerPlanItem("默认（不覆盖）", ""));
                combo.Items.Add(new PowerPlanItem("自定义 GUID...", "CUSTOM"));
                foreach (var p in allPowerPlans)
                    combo.Items.Add(new PowerPlanItem(p.name, p.guid));
                combo.DisplayMember = "Name";
                combo.ValueMember = "Guid";

                var txtCustom = new TextBox
                {
                    Location = new Point(leftControl + controlW - 115, y + 2),
                    Size = new Size(115, 26),
                    Visible = false
                };
                combo.SelectedIndexChanged += (_, _) =>
                {
                    if (combo.SelectedItem is PowerPlanItem item)
                    {
                        txtCustom.Visible = (item.Guid == "CUSTOM");
                        if (item.Guid != "CUSTOM" && !string.IsNullOrEmpty(item.Guid))
                            AppConfig.Set("scheme_" + modes[modeIdx].mode, item.Guid);
                        else if (item.Guid == "")
                            AppConfig.Remove("scheme_" + modes[modeIdx].mode);
                    }
                };
                txtCustom.Leave += (_, _) =>
                {
                    string guid = txtCustom.Text.Trim();
                    if (!string.IsNullOrEmpty(guid))
                        AppConfig.Set("scheme_" + modes[modeIdx].mode, guid);
                };

                powerPlanCombos[i] = combo;
                powerPlanCustom[i] = txtCustom;

                Controls.Add(label);
                Controls.Add(combo);
                Controls.Add(txtCustom);
                y += 42;
            }

            y += 10;
            labelCurrent = new Label
            {
                Text = "当前电源计划: " + GetActivePlanName(),
                Location = new Point(leftLabel, y),
                Size = new Size(500, 30),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(labelCurrent);
            y += 60;

            // ── 任务栏监控窗口 ──
            var sep2 = new Label
            {
                Text = "────────────────────────────────────────────────────",
                Location = new Point(leftLabel, y),
                Size = new Size(700, 16),
                ForeColor = SystemColors.ControlDark
            };
            Controls.Add(sep2);
            y += 20;

            var titleTaskbar = new Label
            {
                Text = "任务栏监控窗口",
                Location = new Point(leftLabel, y),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Tag = SectionTitleTag,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(titleTaskbar);
            y += 45;

            // 启用
            var labelEnable = new Label
            {
                Text = "启用任务栏窗口",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkTaskbarEnabled = new CheckBox
            {
                Text = "在系统任务栏嵌入硬件监控",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkTaskbarEnabled.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig();
                ApplyTaskbarState();
            };
            Controls.Add(labelEnable);
            Controls.Add(checkTaskbarEnabled);
            y += 50;

            // 显示形式
            var labelFloating = new Label
            {
                Text = "显示形式",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkTaskbarFloating = new CheckBox
            {
                Text = "使用独立浮窗显示",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkTaskbarFloating.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig();
                UpdateTaskbarModeControls();
                Main.RestartTaskbarWindow();
                BeginInvoke((Action)RefreshFloatingPositionControls);
            };
            Controls.Add(labelFloating);
            Controls.Add(checkTaskbarFloating);
            y += 50;

            // 位置
            var labelPos = new Label
            {
                Text = "任务栏位置",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            comboTaskbarPosition = new RComboBox
            {
                NativeHeight = true,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(leftControl, y + 2),
                Size = new Size(150, 26)
            };
            comboTaskbarPosition.Items.Add("左侧");
            comboTaskbarPosition.Items.Add("右侧");
            comboTaskbarPosition.SelectedIndexChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig();
                _ = Task.Run(RefreshTaskbarPosition);
            };
            Controls.Add(labelPos);
            Controls.Add(comboTaskbarPosition);
            y += 48;

            // 浮窗 X
            var labelFloatingX = new Label
            {
                Text = "浮窗 X",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numFloatingX = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(100, 26),
                Minimum = -20000,
                Maximum = 20000
            };
            numFloatingX.ValueChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig(saveFloatingPosition: true);
                Main.RefreshTaskbarPosition();
            };
            Controls.Add(labelFloatingX);
            Controls.Add(numFloatingX);
            y += 48;

            // 浮窗 Y
            var labelFloatingY = new Label
            {
                Text = "浮窗 Y",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numFloatingY = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(100, 26),
                Minimum = -20000,
                Maximum = 20000
            };
            numFloatingY.ValueChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig(saveFloatingPosition: true);
                Main.RefreshTaskbarPosition();
            };
            Controls.Add(labelFloatingY);
            Controls.Add(numFloatingY);
            y += 48;

            // 点击穿透
            var labelFloatingClickThrough = new Label
            {
                Text = "点击穿透",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkFloatingClickThrough = new CheckBox
            {
                Text = "浮窗不拦截鼠标点击",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkFloatingClickThrough.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig();
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelFloatingClickThrough);
            Controls.Add(checkFloatingClickThrough);
            y += 48;

            // 浮窗置顶
            var labelFloatingTopmost = new Label
            {
                Text = "浮窗置顶",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkFloatingTopmost = new CheckBox
            {
                Text = "保持浮窗在最前",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkFloatingTopmost.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                SaveHimpqConfig();
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelFloatingTopmost);
            Controls.Add(checkFloatingTopmost);
            y += 48;

            // 显示项目
            var labelItems = new Label
            {
                Text = "显示项目",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var btnEditItems = new RButton
            {
                Text = "编辑任务栏项目...",
                Location = new Point(leftControl, y + 2),
                Size = new Size(160, 28)
            };
            btnEditItems.Click += (_, _) =>
            {
                var config = HimpqConfig.Load();
                var items = (config.taskbar_items is { Count: > 0 })
                    ? config.taskbar_items : HimpqConfig.DefaultItems();
                using var editor = new TaskbarItemEditor(items);
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    config.taskbar_items = editor.GetItems();
                    HimpqConfig.Save(config);
                    Main.RefreshTaskbarLayout();
                }
            };
            Controls.Add(labelItems);
            Controls.Add(btnEditItems);
            y += 50;

            // 字体大小
            var labelFont = new Label
            {
                Text = "字体大小",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numFontSize = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(80, 26),
                Minimum = 6,
                Maximum = 24
            };
            numFontSize.ValueChanged += (_, _) =>
            {
                SaveHimpqConfig();
                Main.UpdateTaskbarFont("Segoe UI", (float)numFontSize.Value);
            };
            Controls.Add(labelFont);
            Controls.Add(numFontSize);
            y += 48;

            // 水平偏移
            var labelOffset = new Label
            {
                Text = "任务栏水平偏移",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numOffset = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(100, 26),
                Minimum = -200,
                Maximum = 200
            };
            numOffset.ValueChanged += (_, _) =>
            {
                SaveHimpqConfig();
                _ = Task.Run(RefreshTaskbarPosition);
            };
            Controls.Add(labelOffset);
            Controls.Add(numOffset);
            y += 48;

            // 列间距
            var labelInterItemGap = new Label
            {
                Text = "列间距",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numInterItemGap = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(70, 26),
                Minimum = -80,
                Maximum = 80
            };
            numInterItemGap.ValueChanged += (_, _) =>
            {
                SaveHimpqConfig();
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelInterItemGap);
            Controls.Add(numInterItemGap);
            y += 48;

            // 行间距
            var labelRowGap = new Label
            {
                Text = "行间距",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numRowGap = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(70, 26),
                Minimum = -20,
                Maximum = 40
            };
            numRowGap.ValueChanged += (_, _) =>
            {
                SaveHimpqConfig();
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelRowGap);
            Controls.Add(numRowGap);
            y += 48;

            // 刷新间隔
            var labelRefreshInterval = new Label
            {
                Text = "刷新间隔(ms)",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            numRefreshInterval = new RNumericUpDown
            {
                Location = new Point(leftControl, y + 2),
                Size = new Size(100, 26),
                Minimum = 250,
                Maximum = 5000,
                Increment = 250
            };
            numRefreshInterval.ValueChanged += (_, _) =>
            {
                SaveHimpqConfig();
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelRefreshInterval);
            Controls.Add(numRefreshInterval);
            y += 48;

            // 文字阴影
            var labelTextShadow = new Label
            {
                Text = "文字阴影",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            checkTaskbarTextShadow = new CheckBox
            {
                Text = "启用任务栏文字阴影",
                Location = new Point(leftControl, y + 2),
                Size = new Size(controlW, 26)
            };
            checkTaskbarTextShadow.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                var config = HimpqConfig.Load();
                HimpqConfig.SetTaskbarShadowEnabled(config, darkTheme, checkTaskbarTextShadow.Checked);
                HimpqConfig.Save(config);
                Main.RefreshTaskbarLayout();
            };
            Controls.Add(labelTextShadow);
            Controls.Add(checkTaskbarTextShadow);
            y += 48;

            // 标签颜色
            var labelTaskbarLabelColor = new Label
            {
                Text = "标签颜色",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowTaskbarLabelColor = CreateColorRow(out panelTaskbarLabelColor, () => PickTaskbarColor(TaskbarColorTarget.Label), includeReset: false);
            Controls.Add(labelTaskbarLabelColor);
            Controls.Add(rowTaskbarLabelColor);
            y += 48;

            // 数值颜色
            var labelTaskbarValueColor = new Label
            {
                Text = "数值颜色",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowTaskbarValueColor = CreateColorRow(out panelTaskbarValueColor, () => PickTaskbarColor(TaskbarColorTarget.Value), includeReset: false);
            Controls.Add(labelTaskbarValueColor);
            Controls.Add(rowTaskbarValueColor);
            y += 48;

            // 阴影颜色
            var labelTaskbarShadowColor = new Label
            {
                Text = "阴影颜色",
                Location = new Point(leftLabel, y),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowTaskbarShadowColor = CreateColorRow(out panelTaskbarShadowColor, () => PickTaskbarColor(TaskbarColorTarget.Shadow), includeReset: true);
            Controls.Add(labelTaskbarShadowColor);
            Controls.Add(rowTaskbarShadowColor);
            y += 64;

        }

        private Panel CreateColorRow(out Panel swatch, Action pickColor, bool includeReset)
        {
            var row = new Panel
            {
                Size = new Size(360, 34),
                BackColor = BackColor
            };

            swatch = new Panel
            {
                Location = new Point(0, 3),
                Size = new Size(34, 28),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = ColorSwatchTag
            };
            swatch.Click += (_, _) => pickColor();

            var btnPick = new RButton
            {
                Text = "选择",
                Location = new Point(44, 0),
                Size = new Size(76, 34)
            };
            btnPick.Click += (_, _) => pickColor();

            row.Controls.Add(swatch);
            row.Controls.Add(btnPick);

            if (includeReset)
            {
                btnResetTaskbarColors = new RButton
                {
                    Text = darkTheme ? "重置深色" : "重置浅色",
                    Location = new Point(132, 0),
                    Size = new Size(122, 34)
                };
                btnResetTaskbarColors.Click += (_, _) => ResetTaskbarColors();
                row.Controls.Add(btnResetTaskbarColors);
            }

            return row;
        }

        private void PickTaskbarColor(TaskbarColorTarget target)
        {
            var data = HimpqConfig.Load();
            Color current = target switch
            {
                TaskbarColorTarget.Label => HimpqConfig.ResolveTaskbarLabelColor(data, darkTheme),
                TaskbarColorTarget.Value => HimpqConfig.ResolveTaskbarValueColor(data, darkTheme),
                _ => HimpqConfig.ResolveTaskbarShadowColor(data, darkTheme)
            };

            using var picker = new RColorPicker(current);
            picker.ColorChanged += color =>
            {
                var config = HimpqConfig.Load();
                switch (target)
                {
                    case TaskbarColorTarget.Label:
                        HimpqConfig.SetTaskbarLabelColor(config, darkTheme, color);
                        break;
                    case TaskbarColorTarget.Value:
                        HimpqConfig.SetTaskbarValueColor(config, darkTheme, color);
                        break;
                    case TaskbarColorTarget.Shadow:
                        HimpqConfig.SetTaskbarShadowColor(config, darkTheme, color);
                        break;
                }

                HimpqConfig.Save(config);
                UpdateTaskbarColorSwatches();
                Main.RefreshTaskbarLayout();
            };
            picker.ShowDialog(this);
        }

        private void ResetTaskbarColors()
        {
            var config = HimpqConfig.Load();
            HimpqConfig.ResetTaskbarColors(config, darkTheme);
            HimpqConfig.Save(config);
            UpdateTaskbarColorSwatches();
            Main.RefreshTaskbarLayout();
        }

        private void UpdateTaskbarColorSwatches()
        {
            if (panelTaskbarLabelColor is null || panelTaskbarValueColor is null || panelTaskbarShadowColor is null)
                return;

            var data = HimpqConfig.Load();
            panelTaskbarLabelColor.BackColor = HimpqConfig.ResolveTaskbarLabelColor(data, darkTheme);
            panelTaskbarValueColor.BackColor = HimpqConfig.ResolveTaskbarValueColor(data, darkTheme);
            panelTaskbarShadowColor.BackColor = HimpqConfig.ResolveTaskbarShadowColor(data, darkTheme);

            if (checkTaskbarTextShadow is not null)
            {
                bool wasLoading = _loading;
                _loading = true;
                checkTaskbarTextShadow.Checked = HimpqConfig.ResolveTaskbarShadowEnabled(data, darkTheme);
                _loading = wasLoading;
            }

            if (btnResetTaskbarColors is not null)
                btnResetTaskbarColors.Text = darkTheme ? "重置深色" : "重置浅色";
        }

        private void ApplySettingsVisualLayout()
        {
            AutoScroll = true;
            ClientSize = new Size(800, 920);
            MinimumSize = new Size(720, 700);

            BackColor = RForm.formBack;
            ForeColor = RForm.foreMain;

            foreach (Control control in Controls)
                ApplySettingsControlTheme(control);

            ArrangeTaskbarSettingsControls();
            UpdateTaskbarColorSwatches();
            AutoScrollMargin = new Size(0, 48);
        }

        private void ApplySettingsControlTheme(Control control)
        {
            if (control is Label label)
            {
                if (label.Width >= 650 && label.Height <= 18)
                {
                    label.Visible = false;
                    return;
                }

                if (label == labelCurrent)
                {
                    label.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                    label.Size = new Size(420, 26);
                    label.ForeColor = RForm.colorGray;
                }
                else if (Equals(label.Tag, SectionTitleTag))
                {
                    label.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    label.Height = 30;
                    label.Margin = new Padding(0, 0, 0, 10);
                    label.ForeColor = RForm.foreMain;
                }
                else
                {
                    label.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                    label.Height = Math.Max(label.Height, 30);
                    label.ForeColor = RForm.foreMain;
                }
                label.BackColor = BackColor;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.Height = Math.Max(checkBox.Height, 30);
                checkBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                checkBox.BackColor = BackColor;
                checkBox.ForeColor = RForm.foreMain;
            }
            else if (control is ComboBox or TextBox or NumericUpDown or RNumericUpDown)
            {
                control.Height = Math.Max(control.Height, 32);
                control.BackColor = RForm.buttonMain;
                control.ForeColor = RForm.foreMain;
                if (control is NumericUpDown numeric)
                    numeric.TextAlign = HorizontalAlignment.Center;
            }
            else if (control is RButton button)
            {
                button.Height = Math.Max(button.Height, 32);
                button.Padding = new Padding(8, 0, 8, 1);
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.BackColor = RForm.buttonMain;
                button.ForeColor = RForm.foreMain;
            }
            else if (control is Panel panel && !Equals(panel.Tag, ColorSwatchTag))
            {
                panel.BackColor = BackColor;
            }

            foreach (Control child in control.Controls)
                ApplySettingsControlTheme(child);
        }

        private void ArrangeTaskbarSettingsControls()
        {
            var titleTaskbar = Controls.OfType<Label>().FirstOrDefault(label => label.Text == "任务栏监控窗口");
            if (titleTaskbar is null) return;

            const int labelX = 20;
            const int controlX = 220;
            const int labelW = 170;
            const int controlW = 220;
            const int checkboxW = 360;
            const int controlH = 34;
            const int rowGap = 46;

            int y = titleTaskbar.Bottom + 22;
            PlaceTaskbarRow("启用任务栏窗口", checkTaskbarEnabled, y, labelX, controlX, labelW, checkboxW, controlH);
            y += rowGap;
            PlaceTaskbarRow("显示形式", checkTaskbarFloating, y, labelX, controlX, labelW, checkboxW, controlH);
            y += rowGap;
            PlaceTaskbarRow("任务栏位置", comboTaskbarPosition, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("任务栏水平偏移", numOffset, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("浮窗 X", numFloatingX, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("浮窗 Y", numFloatingY, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("点击穿透", checkFloatingClickThrough, y, labelX, controlX, labelW, checkboxW, controlH);
            y += rowGap;
            PlaceTaskbarRow("浮窗置顶", checkFloatingTopmost, y, labelX, controlX, labelW, checkboxW, controlH);
            y += rowGap;

            var editButton = Controls.OfType<RButton>().FirstOrDefault(button => button.Text == "编辑任务栏项目...");
            if (editButton is not null)
                PlaceTaskbarRow("显示项目", editButton, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;

            PlaceTaskbarRow("字体大小", numFontSize, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("列间距", numInterItemGap, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("行间距", numRowGap, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("刷新间隔(ms)", numRefreshInterval, y, labelX, controlX, labelW, controlW, controlH);
            y += rowGap;
            PlaceTaskbarRow("文字阴影", checkTaskbarTextShadow, y, labelX, controlX, labelW, checkboxW, controlH);
            y += rowGap;
            PlaceTaskbarRow("标签颜色", rowTaskbarLabelColor, y, labelX, controlX, labelW, controlW + 140, controlH);
            y += rowGap;
            PlaceTaskbarRow("数值颜色", rowTaskbarValueColor, y, labelX, controlX, labelW, controlW + 140, controlH);
            y += rowGap;
            PlaceTaskbarRow("阴影颜色", rowTaskbarShadowColor, y, labelX, controlX, labelW, controlW + 140, controlH);
            AutoScrollMinSize = new Size(0, y + controlH + 64);
            UpdateTaskbarModeControls();
        }

        private void PlaceTaskbarRow(string labelText, Control control, int y, int labelX, int controlX, int labelW, int controlW, int controlH)
        {
            var label = Controls.OfType<Label>().FirstOrDefault(item => item.Text == labelText);
            if (label is not null)
            {
                label.Location = new Point(labelX, y);
                label.Size = new Size(labelW, controlH);
                label.TextAlign = ContentAlignment.MiddleLeft;
            }

            control.Location = new Point(controlX, y);
            control.Size = new Size(controlW, controlH);
        }

        private void UpdateTaskbarModeControls()
        {
            if (checkTaskbarFloating is null) return;

            bool floating = checkTaskbarFloating.Checked;
            comboTaskbarPosition.Enabled = !floating;
            numOffset.Enabled = !floating;
            numFloatingX.Enabled = floating;
            numFloatingY.Enabled = floating;
            checkFloatingClickThrough.Enabled = floating;
            checkFloatingTopmost.Enabled = floating;
        }

        private void RefreshFloatingPositionControls()
        {
            if (numFloatingX is null || numFloatingY is null) return;

            var data = HimpqConfig.Load();
            if (data.taskbar_floating_position_initialized != 1) return;

            bool wasLoading = _loading;
            _loading = true;
            SetNumericValue(numFloatingX, data.taskbar_floating_x);
            SetNumericValue(numFloatingY, data.taskbar_floating_y);
            _loading = wasLoading;
        }

        private static void SetNumericValue(NumericUpDown numeric, int value)
        {
            numeric.Value = Math.Clamp(value, (int)numeric.Minimum, (int)numeric.Maximum);
        }

        private void AddSectionBackPanel(int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BackColor
            };
            Controls.Add(panel);
            panel.SendToBack();
        }

        private string GetActivePlanName()
        {
            try
            {
                var proc = Process.Start(new ProcessStartInfo("powercfg", "/getactivescheme")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    var match = Regex.Match(output, @"GUID:\s*([a-fA-F0-9\-]+)\s*\((.+)\)");
                    if (match.Success)
                    {
                        string guid = match.Groups[1].Value.Trim();
                        string name = match.Groups[2].Value.Trim();
                        foreach (var p in allPowerPlans)
                            if (p.guid == guid) return p.name;
                        return name;
                    }
                }
            }
            catch { }
            return "未知";
        }

        private static void SelectModeItem(RComboBox combo, int value)
        {
            foreach (ModeItem item in combo.Items)
            {
                if (item.Value == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private void LoadConfig()
        {
            var data = HimpqConfig.Load();

            checkDebug.Checked = data.debug_mode == 1;
            checkTaskbarEnabled.Checked = data.taskbar_window_enabled == 1;
            checkTaskbarFloating.Checked = data.taskbar_window_floating_enabled == 1;
            comboTaskbarPosition.SelectedIndex = data.taskbar_window_position == "right" ? 1 : 0;
            numFontSize.Value = data.font_size > 0 ? data.font_size : 8;
            numOffset.Value = data.taskbar_window_offset;
            SetNumericValue(numFloatingX, data.taskbar_floating_x);
            SetNumericValue(numFloatingY, data.taskbar_floating_y);
            checkFloatingClickThrough.Checked = data.taskbar_floating_click_through == 1;
            checkFloatingTopmost.Checked = data.taskbar_floating_topmost == 1;
            numInterItemGap.Value = data.inter_item_gap;
            numRowGap.Value = data.row_gap;
            numRefreshInterval.Value = Math.Clamp(data.taskbar_refresh_interval, (int)numRefreshInterval.Minimum, (int)numRefreshInterval.Maximum);
            checkTaskbarTextShadow.Checked = HimpqConfig.ResolveTaskbarShadowEnabled(data, RForm.IsDarkThemeActive());
            UpdateTaskbarModeControls();

            SelectModeItem(comboDefaultMode, data.default_performance_mode);
            SelectModeItem(comboUnplugMode, data.unplug_performance_mode);

            for (int i = 0; i < modes.Length; i++)
            {
                string saved = AppConfig.GetString("scheme_" + modes[i].mode);
                if (string.IsNullOrEmpty(saved))
                {
                    powerPlanCombos[i].SelectedIndex = 0;
                    continue;
                }

                bool found = false;
                foreach (PowerPlanItem item in powerPlanCombos[i].Items)
                {
                    if (!string.IsNullOrEmpty(item.Guid) && item.Guid == saved)
                    {
                        powerPlanCombos[i].SelectedItem = item;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    powerPlanCombos[i].SelectedIndex = 1;
                    powerPlanCustom[i].Text = saved;
                    powerPlanCustom[i].Visible = true;
                }
            }
        }

        private void SaveHimpqConfig(bool saveFloatingPosition = false)
        {
            if (_loading) return;

            var data = HimpqConfig.Load();
            data.debug_mode = checkDebug.Checked ? 1 : 0;
            data.taskbar_window_enabled = checkTaskbarEnabled.Checked ? 1 : 0;
            data.taskbar_window_floating_enabled = checkTaskbarFloating.Checked ? 1 : 0;
            data.taskbar_window_position = comboTaskbarPosition.SelectedIndex == 1 ? "right" : "left";
            data.font_size = (int)numFontSize.Value;
            data.taskbar_window_offset = (int)numOffset.Value;
            data.taskbar_floating_click_through = checkFloatingClickThrough.Checked ? 1 : 0;
            data.taskbar_floating_topmost = checkFloatingTopmost.Checked ? 1 : 0;
            if (saveFloatingPosition || data.taskbar_floating_position_initialized == 1)
            {
                data.taskbar_floating_x = (int)numFloatingX.Value;
                data.taskbar_floating_y = (int)numFloatingY.Value;
                data.taskbar_floating_position_initialized = 1;
            }
            data.inter_item_gap = (int)numInterItemGap.Value;
            data.row_gap = (int)numRowGap.Value;
            data.taskbar_refresh_interval = (int)numRefreshInterval.Value;
            if (comboDefaultMode.SelectedItem is ModeItem selected)
                data.default_performance_mode = selected.Value;
            if (comboUnplugMode.SelectedItem is ModeItem unplugSelected)
                data.unplug_performance_mode = unplugSelected.Value;
            HimpqConfig.Save(data);
        }

        private void ApplyTaskbarState()
        {
            if (checkTaskbarEnabled.Checked)
            {
                Main.ShowTaskbarWindow();
                BeginInvoke((Action)RefreshFloatingPositionControls);
            }
            else
            {
                Main.HideTaskbarWindow();
            }
        }

        private void RefreshTaskbarPosition()
        {
            Main.RefreshTaskbarPosition();
        }

        private enum TaskbarColorTarget
        {
            Label,
            Value,
            Shadow
        }

        private class ModeItem
        {
            public string Text { get; }
            public int Value { get; }
            public ModeItem(string text, int value) { Text = text; Value = value; }
        }

        private class PowerPlanItem
        {
            public string Name { get; }
            public string Guid { get; }
            public PowerPlanItem(string name, string guid) { Name = name; Guid = guid; }
        }
    }
}
