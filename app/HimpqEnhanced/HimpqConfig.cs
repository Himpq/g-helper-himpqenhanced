using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Drawing;

namespace HimpqEnhanced
{
    public class TaskbarItemConfig
    {
        public bool enabled { get; set; } = true;
        public string label { get; set; } = "";
        public string token { get; set; } = "CPU_TEMP";
        public string suffix { get; set; } = "";
        public int row { get; set; } = 0;
    }

    public class HimpqConfigData
    {
        public int default_performance_mode { get; set; } = -1;
        public int unplug_performance_mode { get; set; } = -1;
        public int debug_mode { get; set; } = 0;
        public int taskbar_window_enabled { get; set; } = 0;
        public int taskbar_window_floating_enabled { get; set; } = 0;
        public string taskbar_window_position { get; set; } = "left";
        public string taskbar_window_template { get; set; } = "";
        public int font_size { get; set; } = 8;
        public string font_name { get; set; } = "Segoe UI";
        public int taskbar_window_offset { get; set; } = 0;
        public int taskbar_floating_x { get; set; } = 0;
        public int taskbar_floating_y { get; set; } = 0;
        public int taskbar_floating_position_initialized { get; set; } = 0;
        public int taskbar_floating_click_through { get; set; } = 1;
        public int taskbar_floating_topmost { get; set; } = 1;
        public int internal_gap { get; set; } = 2;
        public int inter_item_gap { get; set; } = 4;
        public int row_gap { get; set; } = 1;
        public int taskbar_refresh_interval { get; set; } = 1000;
        public int? taskbar_label_color { get; set; }
        public int? taskbar_value_color { get; set; }
        public int? taskbar_dark_label_color { get; set; }
        public int? taskbar_dark_value_color { get; set; }
        public int? taskbar_light_label_color { get; set; }
        public int? taskbar_light_value_color { get; set; }
        public int? taskbar_dark_shadow_enabled { get; set; }
        public int? taskbar_light_shadow_enabled { get; set; }
        public int? taskbar_dark_shadow_color { get; set; }
        public int? taskbar_light_shadow_color { get; set; }
        public List<TaskbarItemConfig> taskbar_items { get; set; } = new();
    }

    public static class HimpqConfig
    {
        private static readonly string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        private static readonly string configFile = Path.Combine(configDir, "himpqenhanced.json");

        public static HimpqConfigData Load()
        {
            try
            {
                Directory.CreateDirectory(configDir);
                if (!File.Exists(configFile))
                {
                    var defaults = NewDefault();
                    Save(defaults);
                    return defaults;
                }

                string json = File.ReadAllText(configFile);
                var data = JsonSerializer.Deserialize<HimpqConfigData>(json) ?? NewDefault();
                if (data.taskbar_items is null)
                    data.taskbar_items = new List<TaskbarItemConfig>();
                return data;
            }
            catch
            {
                return NewDefault();
            }
        }

        public static void Save(HimpqConfigData data)
        {
            try
            {
                Directory.CreateDirectory(configDir);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(configFile, json);
            }
            catch { }
        }

        public static HimpqConfigData NewDefault()
        {
            return new HimpqConfigData
            {
                taskbar_items = DefaultItems()
            };
        }

        public static List<TaskbarItemConfig> DefaultItems() => new()
        {
            new() { enabled = true, label = "CPU", token = "CPU_TEMP", suffix = "°C", row = 0 },
            new() { enabled = true, label = "LOAD", token = "CPU_USAGE", suffix = "%", row = 0 },
            new() { enabled = true, label = "FAN", token = "FAN_CPU", suffix = "rpm", row = 1 },
            new() { enabled = true, label = "GPU", token = "FAN_GPU", suffix = "rpm", row = 1 },
        };

        public static Color DefaultTaskbarLabelColor(bool darkTheme)
            => darkTheme ? Color.FromArgb(255, 180, 180, 180) : Color.FromArgb(255, 87, 96, 106);

        public static Color DefaultTaskbarValueColor(bool darkTheme)
            => darkTheme ? Color.White : Color.FromArgb(255, 17, 24, 39);

        public static Color DefaultTaskbarShadowColor(bool darkTheme)
            => darkTheme ? Color.FromArgb(160, 0, 0, 0) : Color.FromArgb(96, 0, 0, 0);

        public static bool DefaultTaskbarShadowEnabled(bool darkTheme) => false;

        public static Color ResolveTaskbarLabelColor(HimpqConfigData data, bool darkTheme)
            => Color.FromArgb((darkTheme ? data.taskbar_dark_label_color : data.taskbar_light_label_color)
                ?? DefaultTaskbarLabelColor(darkTheme).ToArgb());

        public static Color ResolveTaskbarValueColor(HimpqConfigData data, bool darkTheme)
            => Color.FromArgb((darkTheme ? data.taskbar_dark_value_color : data.taskbar_light_value_color)
                ?? DefaultTaskbarValueColor(darkTheme).ToArgb());

        public static bool ResolveTaskbarShadowEnabled(HimpqConfigData data, bool darkTheme)
            => ((darkTheme ? data.taskbar_dark_shadow_enabled : data.taskbar_light_shadow_enabled)
                ?? (DefaultTaskbarShadowEnabled(darkTheme) ? 1 : 0)) == 1;

        public static Color ResolveTaskbarShadowColor(HimpqConfigData data, bool darkTheme)
            => Color.FromArgb((darkTheme ? data.taskbar_dark_shadow_color : data.taskbar_light_shadow_color)
                ?? DefaultTaskbarShadowColor(darkTheme).ToArgb());

        public static void SetTaskbarLabelColor(HimpqConfigData data, bool darkTheme, Color color)
        {
            if (darkTheme)
                data.taskbar_dark_label_color = color.ToArgb();
            else
                data.taskbar_light_label_color = color.ToArgb();
        }

        public static void SetTaskbarValueColor(HimpqConfigData data, bool darkTheme, Color color)
        {
            if (darkTheme)
                data.taskbar_dark_value_color = color.ToArgb();
            else
                data.taskbar_light_value_color = color.ToArgb();
        }

        public static void SetTaskbarShadowEnabled(HimpqConfigData data, bool darkTheme, bool enabled)
        {
            if (darkTheme)
                data.taskbar_dark_shadow_enabled = enabled ? 1 : 0;
            else
                data.taskbar_light_shadow_enabled = enabled ? 1 : 0;
        }

        public static void SetTaskbarShadowColor(HimpqConfigData data, bool darkTheme, Color color)
        {
            if (darkTheme)
                data.taskbar_dark_shadow_color = color.ToArgb();
            else
                data.taskbar_light_shadow_color = color.ToArgb();
        }

        public static void ResetTaskbarColors(HimpqConfigData data, bool darkTheme)
        {
            if (darkTheme)
            {
                data.taskbar_dark_label_color = null;
                data.taskbar_dark_value_color = null;
                data.taskbar_dark_shadow_enabled = null;
                data.taskbar_dark_shadow_color = null;
            }
            else
            {
                data.taskbar_light_label_color = null;
                data.taskbar_light_value_color = null;
                data.taskbar_light_shadow_enabled = null;
                data.taskbar_light_shadow_color = null;
            }
        }
    }
}
