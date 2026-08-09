using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Drawing;
using System.Reflection;
using System.Text;
using GHelper;

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

    public class HimpqConfigExportData
    {
        public int format_version { get; set; } = 1;
        public string app_version { get; set; } = "";
        public string exported_at { get; set; } = "";
        public HimpqConfigData? himpq_config { get; set; }
        public Dictionary<string, string>? power_schemes { get; set; }
    }

    public static class HimpqConfig
    {
        private static readonly string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        private static readonly string configFile = Path.Combine(configDir, "himpqenhanced.json");
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

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
                SaveStrict(data);
            }
            catch { }
        }

        public static void SaveStrict(HimpqConfigData data)
        {
            Directory.CreateDirectory(configDir);
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(configFile, json, new UTF8Encoding(false));
        }

        public static HimpqConfigExportData CreateExportSnapshot()
        {
            var schemes = new Dictionary<string, string>();
            foreach (int mode in new[] { 0, 1, 2 })
            {
                string value = AppConfig.GetString("scheme_" + mode);
                if (!string.IsNullOrWhiteSpace(value))
                    schemes[mode.ToString()] = value;
            }

            return new HimpqConfigExportData
            {
                app_version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                exported_at = DateTimeOffset.Now.ToString("O"),
                himpq_config = Load(),
                power_schemes = schemes
            };
        }

        public static void Export(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(CreateExportSnapshot(), JsonOptions);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        public static HimpqConfigExportData ReadExport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Import path is empty.", nameof(path));

            string json = File.ReadAllText(path, new UTF8Encoding(false, true));
            var data = JsonSerializer.Deserialize<HimpqConfigExportData>(json)
                ?? throw new InvalidDataException("Import file is not a valid Himpq config export.");

            ValidateExport(data);
            return data;
        }

        public static void Import(string path)
        {
            var data = ReadExport(path);
            SaveStrict(data.himpq_config!);
            ApplyPowerSchemes(data.power_schemes!);
        }

        private static void ValidateExport(HimpqConfigExportData data)
        {
            if (data.format_version != 1)
                throw new InvalidDataException($"Unsupported Himpq config export version: {data.format_version}.");
            if (data.himpq_config is null)
                throw new InvalidDataException("Import file is missing himpq_config.");
            if (data.himpq_config.taskbar_items is null)
                throw new InvalidDataException("Import file is missing himpq_config.taskbar_items.");
            if (data.power_schemes is null)
                throw new InvalidDataException("Import file is missing power_schemes.");

            foreach (string key in data.power_schemes.Keys)
            {
                if (key is not ("0" or "1" or "2"))
                    throw new InvalidDataException("Import file contains an unknown power scheme key: " + key);
            }
        }

        private static void ApplyPowerSchemes(Dictionary<string, string> schemes)
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                string key = mode.ToString();
                if (schemes.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
                    AppConfig.Set("scheme_" + mode, value.Trim());
                else
                    AppConfig.Remove("scheme_" + mode);
            }
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
