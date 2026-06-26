using System.ComponentModel;
using GHelper.UI;

namespace HimpqEnhanced
{
    public class TaskbarItemEditor : RForm
    {
        private const int WindowWidth = 820;
        private const int WindowHeight = 780;
        private const int MarginSize = 20;
        private const int RowHeight = 52;
        private const int ListTopGap = 14;
        private const int ListBottomGap = 18;
        private const int MoveButtonEditorGap = 16;
        private const int EditorButtonGap = 12;
        private const int EditorPanelHeight = 104;
        private const int BottomButtonHeight = 40;
        private const int BottomButtonTop = WindowHeight - MarginSize - BottomButtonHeight;

        private BindingList<TaskbarItemConfig> _items;
        private Panel _listPanel;
        private Panel _editorPanel;
        private TextBox _txtLabel;
        private TextBox _txtSuffix;
        private NumericUpDown _numRow;
        private Label _tokenDescLabel;
        private int _selectedIndex = -1;
        private bool _updating;

        private static readonly (string token, string desc, string label, string suffix, int row)[] KnownTokens = new[]
        {
            ("CPU_TEMP", "CPU 温度", "CPU", "°C", 0),
            ("GPU_TEMP", "GPU 温度", "GPU", "°C", 0),
            ("CPU_USAGE", "CPU 使用率", "CPU", "%", 0),
            ("GPU_USAGE", "GPU 使用率", "GPU", "%", 0),
            ("CPU_FREQ", "CPU 频率", "CPU", "MHz", 0),
            ("GPU_FREQ", "GPU 频率", "GPU", "MHz", 0),
            ("CPU_FREQ_GHZ", "CPU 频率(GHz)", "CPU", "GHz", 0),
            ("GPU_FREQ_GHZ", "GPU 频率(GHz)", "GPU", "GHz", 0),
            ("RAM_USAGE", "内存使用率", "RAM", "%", 1),
            ("RAM_USED", "内存占用", "RAM", "MB", 1),
            ("VRAM_USAGE", "显存使用率", "VRAM", "%", 1),
            ("VRAM_USED", "显存占用", "VRAM", "MB", 1),
            ("CPU_POWER", "CPU 功耗", "CPU", "W", 1),
            ("GPU_POWER", "GPU 功耗", "GPU", "W", 1),
            ("TOTAL_POWER", "功率消耗", "PWR", "W", 1),
            ("BATTERY_POWER", "电池功耗", "PWR", "W", 1),
            ("BATTERY_LEVEL", "电量", "BAT", "%", 1),
            ("BATTERY_HEALTH", "电池健康", "HLT", "%", 1),
            ("POWER_SOURCE", "供电来源", "SRC", "", 1),
            ("MODE", "性能模式", "MODE", "", 1),
            ("FAN_CPU", "CPU 风扇", "FAN", "rpm", 1),
            ("FAN_GPU", "GPU 风扇", "GPU", "rpm", 1),
            ("FAN_MID", "中置风扇", "MID", "rpm", 1),
        };

        public TaskbarItemEditor(List<TaskbarItemConfig> items)
        {
            Text = "任务栏显示项目";
            ClientSize = new Size(WindowWidth, WindowHeight);
            MinimumSize = new Size(WindowWidth + 16, WindowHeight + 40);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _items = new BindingList<TaskbarItemConfig>(MergeKnownItems(items));

            InitUI();
            InitTheme(true);
            ApplyEditorTheme();
        }

        private static List<TaskbarItemConfig> DefaultItems() => new()
        {
            new() { enabled = true, label = "CPU", token = "CPU_TEMP", suffix = "°C", row = 0 },
            new() { enabled = true, label = "LOAD", token = "CPU_USAGE", suffix = "%", row = 0 },
            new() { enabled = true, label = "GPU", token = "GPU_TEMP", suffix = "°C", row = 0 },
            new() { enabled = true, label = "GPU", token = "GPU_USAGE", suffix = "%", row = 0 },
            new() { enabled = true, label = "RAM", token = "RAM_USAGE", suffix = "%", row = 1 },
            new() { enabled = true, label = "FAN", token = "FAN_CPU", suffix = "rpm", row = 1 },
            new() { enabled = true, label = "GPU", token = "FAN_GPU", suffix = "rpm", row = 1 },
        };

        public List<TaskbarItemConfig> GetItems() => _items.ToList();

        private static int NormalizeRow(int row) => Math.Abs(row) % 2;
        private static int ToDisplayRow(int row) => NormalizeRow(row) + 1;
        private static int FromDisplayRow(decimal row) => NormalizeRow((int)row - 1);
        private static string FormatDisplayRow(int row) => $"第 {ToDisplayRow(row)} 行";

        private static string GetDescription(string token)
        {
            foreach (var item in KnownTokens)
                if (item.token == token) return item.desc;
            return token;
        }

        private static List<TaskbarItemConfig> MergeKnownItems(List<TaskbarItemConfig> configuredItems)
        {
            var result = new List<TaskbarItemConfig>();
            var seen = new HashSet<string>();

            foreach (var item in configuredItems)
            {
                if (string.IsNullOrWhiteSpace(item.token) || !seen.Add(item.token))
                    continue;

                result.Add(new TaskbarItemConfig
                {
                    enabled = item.enabled,
                    label = item.label,
                    token = item.token,
                    suffix = item.suffix,
                    row = NormalizeRow(item.row)
                });
            }

            foreach (var item in KnownTokens)
            {
                if (seen.Contains(item.token))
                    continue;

                result.Add(new TaskbarItemConfig
                {
                    enabled = false,
                    label = item.label,
                    token = item.token,
                    suffix = item.suffix,
                    row = item.row
                });
            }

            return result.Count > 0 ? result : DefaultItems();
        }

        private void InitUI()
        {
            var title = new Label
            {
                Text = "显示项目",
                Location = new Point(MarginSize, 16),
                Size = new Size(170, 30),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(title);

            var hint = new Label
            {
                Text = "勾选项目即可启用；选中后用上移/下移调整顺序。频率可选择 MHz 或 GHz 项，显示行使用 1 或 2。",
                AutoSize = false,
                Location = new Point(MarginSize, title.Bottom + 12),
                Size = new Size(WindowWidth - MarginSize * 2, 1),
                TextAlign = ContentAlignment.TopLeft
            };
            hint.Height = TextRenderer.MeasureText(
                hint.Text,
                hint.Font,
                new Size(hint.Width, int.MaxValue),
                TextFormatFlags.WordBreak).Height + 8;
            Controls.Add(hint);

            var btnReset = new RButton
            {
                Text = "恢复默认",
                Location = new Point(WindowWidth - MarginSize - 124, 18),
                Size = new Size(124, 36)
            };
            btnReset.Click += (_, _) =>
            {
                _items = new BindingList<TaskbarItemConfig>(MergeKnownItems(DefaultItems()));
                _selectedIndex = -1;
                RebuildList();
                _editorPanel.Visible = false;
            };
            Controls.Add(btnReset);

            int editorTop = BottomButtonTop - EditorButtonGap - EditorPanelHeight;
            int moveButtonTop = editorTop - MoveButtonEditorGap - 38;
            int listTop = hint.Bottom + ListTopGap;
            int listHeight = moveButtonTop - ListBottomGap - listTop;

            _listPanel = new Panel
            {
                Location = new Point(MarginSize, listTop),
                Size = new Size(WindowWidth - MarginSize * 2, listHeight),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            Controls.Add(_listPanel);

            var btnUp = new RButton { Text = "上移", Location = new Point(MarginSize, moveButtonTop), Size = new Size(100, 38) };
            btnUp.Click += (_, _) => MoveItem(-1);
            Controls.Add(btnUp);

            var btnDown = new RButton { Text = "下移", Location = new Point(132, moveButtonTop), Size = new Size(100, 38) };
            btnDown.Click += (_, _) => MoveItem(1);
            Controls.Add(btnDown);

            _editorPanel = new Panel
            {
                Location = new Point(MarginSize, editorTop),
                Size = new Size(WindowWidth - MarginSize * 2, EditorPanelHeight),
                Visible = false
            };
            Controls.Add(_editorPanel);

            var editorTitle = new Label
            {
                Text = "选中项目编辑",
                Location = new Point(0, 0),
                Size = new Size(180, 24),
                Font = new Font(Font, FontStyle.Bold)
            };
            _editorPanel.Controls.Add(editorTitle);

            var lblLabel = new Label { Text = "显示名称", Location = new Point(0, 36), Size = new Size(88, 32), TextAlign = ContentAlignment.MiddleLeft };
            _txtLabel = new TextBox { Location = new Point(96, 36), Size = new Size(132, 32) };
            _txtLabel.TextChanged += (_, _) =>
            {
                if (_updating || _selectedIndex < 0) return;
                _items[_selectedIndex].label = _txtLabel.Text.Trim();
                UpdateSelectedRowDisplay();
            };

            var lblSuffix = new Label { Text = "单位后缀", Location = new Point(252, 36), Size = new Size(88, 32), TextAlign = ContentAlignment.MiddleLeft };
            _txtSuffix = new TextBox { Location = new Point(348, 36), Size = new Size(132, 32) };
            _txtSuffix.TextChanged += (_, _) =>
            {
                if (_updating || _selectedIndex < 0) return;
                _items[_selectedIndex].suffix = _txtSuffix.Text.Trim();
                UpdateSelectedRowDisplay();
            };

            var lblRow = new Label { Text = "显示行", Location = new Point(504, 36), Size = new Size(86, 32), TextAlign = ContentAlignment.MiddleLeft };
            _numRow = new NumericUpDown { Location = new Point(596, 36), Size = new Size(66, 32), Minimum = 1, Maximum = 2 };
            _numRow.ValueChanged += (_, _) =>
            {
                if (_updating || _selectedIndex < 0) return;
                _items[_selectedIndex].row = FromDisplayRow(_numRow.Value);
                UpdateSelectedRowDisplay();
            };

            _tokenDescLabel = new Label
            {
                Location = new Point(0, 74),
                Size = new Size(720, 26),
                Font = new Font(Font.FontFamily, 8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _editorPanel.Controls.AddRange(new Control[] { lblLabel, _txtLabel, lblSuffix, _txtSuffix, lblRow, _numRow, _tokenDescLabel });

            var btnOK = new RButton
            {
                Text = "确定",
                Location = new Point(WindowWidth - MarginSize - 228, BottomButtonTop),
                Size = new Size(104, BottomButtonHeight),
                DialogResult = DialogResult.OK
            };
            Controls.Add(btnOK);

            var btnCancel = new RButton
            {
                Text = "取消",
                Location = new Point(WindowWidth - MarginSize - 108, BottomButtonTop),
                Size = new Size(104, BottomButtonHeight),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            UpdateSelectionHighlight();
        }

        private void ApplyEditorTheme()
        {
            BackColor = RForm.formBack;
            ForeColor = RForm.foreMain;
            _listPanel.BackColor = RForm.buttonSecond;
            _editorPanel.BackColor = RForm.formBack;

            foreach (Control control in Controls)
                ApplyControlTheme(control);

            RebuildList();
        }

        private void ApplyControlTheme(Control control)
        {
            if (control is Label label)
            {
                label.BackColor = control.Parent?.BackColor ?? BackColor;
                label.ForeColor = label == _tokenDescLabel ? RForm.colorGray : RForm.foreMain;
            }
            else if (control is TextBox or NumericUpDown)
            {
                control.BackColor = RForm.buttonMain;
                control.ForeColor = RForm.foreMain;
            }

            foreach (Control child in control.Controls)
                ApplyControlTheme(child);
        }

        private void MoveItem(int direction)
        {
            if (_selectedIndex < 0) return;
            int newIdx = _selectedIndex + direction;
            if (newIdx < 0 || newIdx >= _items.Count) return;

            var tmp = _items[_selectedIndex];
            _items[_selectedIndex] = _items[newIdx];
            _items[newIdx] = tmp;

            var selectedPanel = GetRowPanel(_selectedIndex);
            var targetPanel = GetRowPanel(newIdx);
            _selectedIndex = newIdx;

            if (selectedPanel is not null && targetPanel is not null)
            {
                int selectedTop = selectedPanel.Top;
                selectedPanel.Top = targetPanel.Top;
                targetPanel.Top = selectedTop;
                UpdateItemRow(selectedPanel, newIdx, _items[newIdx]);
                UpdateItemRow(targetPanel, newIdx - direction, _items[newIdx - direction]);
            }
            else
            {
                RebuildList();
            }

            SelectItem(newIdx);
        }

        private void RebuildList()
        {
            _listPanel.SuspendLayout();
            _listPanel.AutoScrollPosition = Point.Empty;
            _listPanel.Controls.Clear();

            int y = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var panel = CreateItemRow(i, _items[i]);
                panel.Location = new Point(0, y);
                _listPanel.Controls.Add(panel);
                y += RowHeight;
            }

            _listPanel.AutoScrollMinSize = new Size(0, y);
            _listPanel.ResumeLayout(false);
            UpdateSelectionHighlight();
        }

        private Panel? GetRowPanel(int index)
        {
            foreach (Control control in _listPanel.Controls)
                if (control is Panel panel && panel.Tag is int idx && idx == index)
                    return panel;
            return null;
        }

        private Panel CreateItemRow(int idx, TaskbarItemConfig item)
        {
            var panel = new Panel
            {
                Size = new Size(_listPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4, RowHeight - 4),
                Tag = idx,
                BackColor = idx == _selectedIndex ? RForm.buttonMain : RForm.buttonSecond
            };

            var chk = new CheckBox
            {
                Name = "Enabled",
                Location = new Point(10, 15),
                Size = new Size(22, 22),
                Checked = item.enabled,
                Tag = idx
            };
            chk.CheckedChanged += (sender, _) =>
            {
                if (sender is CheckBox cb && cb.Tag is int itemIdx && itemIdx >= 0 && itemIdx < _items.Count)
                    _items[itemIdx].enabled = cb.Checked;
            };
            chk.Click += (_, _) => SelectItemFromControl(chk);

            var desc = new Label
            {
                Name = "Description",
                Location = new Point(42, 4),
                Size = new Size(154, 23),
                Text = GetDescription(item.token),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = RForm.foreMain,
                Font = new Font(Font, FontStyle.Bold)
            };

            var token = new Label
            {
                Name = "Token",
                Location = new Point(42, 27),
                Size = new Size(154, 18),
                Text = item.token,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = RForm.colorGray,
                Font = new Font(Font.FontFamily, 8F)
            };

            var preview = new Label
            {
                Name = "Preview",
                Location = new Point(214, 7),
                Size = new Size(340, 34),
                Text = GetPreview(item),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = RForm.colorGray,
                Font = new Font(Font, FontStyle.Italic)
            };

            var row = new Label
            {
                Name = "Row",
                Location = new Point(572, 13),
                Size = new Size(84, 24),
                Text = FormatDisplayRow(item.row),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = RForm.colorGray
            };

            foreach (Control child in new Control[] { desc, token, preview, row })
            {
                child.BackColor = panel.BackColor;
                child.Click += (_, _) => SelectItemFromControl(child);
            }
            panel.Click += (_, _) => SelectItemFromControl(panel);

            panel.Controls.AddRange(new Control[] { chk, desc, token, preview, row });
            return panel;
        }

        private void UpdateItemRow(Panel panel, int idx, TaskbarItemConfig item)
        {
            panel.Tag = idx;
            foreach (Control child in panel.Controls)
            {
                child.BackColor = panel.BackColor;
                if (child is CheckBox chk && child.Name == "Enabled")
                {
                    chk.Tag = idx;
                    if (chk.Checked != item.enabled)
                        chk.Checked = item.enabled;
                }
                else if (child.Name == "Description")
                    child.Text = GetDescription(item.token);
                else if (child.Name == "Token")
                    child.Text = item.token;
                else if (child.Name == "Preview")
                    child.Text = GetPreview(item);
                else if (child.Name == "Row")
                    child.Text = FormatDisplayRow(item.row);
            }
        }

        private void SelectItemFromControl(Control control)
        {
            Control? current = control;
            while (current is not null && current.Parent != _listPanel)
                current = current.Parent;

            if (current?.Tag is int idx)
                SelectItem(idx);
        }

        private static string GetPreview(TaskbarItemConfig item)
        {
            string label = string.IsNullOrWhiteSpace(item.label) ? GetDescription(item.token) : item.label;
            return $"{label} {{{item.token}}}{item.suffix}";
        }

        private void SelectItem(int idx)
        {
            if (idx < 0 || idx >= _items.Count) return;

            _selectedIndex = idx;
            _editorPanel.Visible = true;
            _updating = true;
            var item = _items[idx];
            _txtLabel.Text = item.label;
            _txtSuffix.Text = item.suffix;
            _numRow.Value = ToDisplayRow(item.row);
            _tokenDescLabel.Text = $"传感器: {item.token}    示例最大值: {GetMaxPreview(item)}";
            _updating = false;
            UpdateSelectionHighlight();
        }

        private void UpdateSelectedRowDisplay()
        {
            if (_selectedIndex < 0)
                return;

            var panel = GetRowPanel(_selectedIndex);
            if (panel is null)
                return;

            var item = _items[_selectedIndex];
            foreach (Control child in panel.Controls)
            {
                if (child.Name == "Preview")
                    child.Text = GetPreview(item);
                else if (child.Name == "Row")
                    child.Text = FormatDisplayRow(item.row);
            }
            _tokenDescLabel.Text = $"传感器: {item.token}    示例最大值: {GetMaxPreview(item)}";
        }

        private static string GetMaxPreview(TaskbarItemConfig item)
        {
            var maxMap = new Dictionary<string, string>
            {
                ["CPU_TEMP"] = "100", ["GPU_TEMP"] = "100",
                ["CPU_USAGE"] = "100", ["GPU_USAGE"] = "100", ["CPU_FREQ"] = "9999", ["GPU_FREQ"] = "9999",
                ["CPU_FREQ_GHZ"] = "10.00", ["GPU_FREQ_GHZ"] = "10.00", ["RAM_USAGE"] = "100",
                ["RAM_USED"] = "99999", ["VRAM_USAGE"] = "100", ["VRAM_USED"] = "99999",
                ["CPU_POWER"] = "100.0", ["GPU_POWER"] = "100.0", ["TOTAL_POWER"] = "100.0", ["BATTERY_POWER"] = "100.0",
                ["BATTERY_LEVEL"] = "100.0", ["BATTERY_HEALTH"] = "100.0", ["POWER_SOURCE"] = "USB-C", ["MODE"] = "增强 (Turbo)",
                ["FAN_CPU"] = "10000", ["FAN_GPU"] = "10000", ["FAN_MID"] = "10000",
            };
            return maxMap.TryGetValue(item.token, out var max) ? $"{max}{item.suffix}" : $"?{item.suffix}";
        }

        private void UpdateSelectionHighlight()
        {
            foreach (Control row in _listPanel.Controls)
            {
                row.BackColor = row.Tag is int idx && idx == _selectedIndex ? RForm.buttonMain : RForm.buttonSecond;
                foreach (Control child in row.Controls)
                    child.BackColor = row.BackColor;
            }
        }

    }
}
