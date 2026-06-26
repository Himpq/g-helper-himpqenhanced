using GHelper.Helpers;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GHelper.AutoUpdate
{
    public class AutoUpdateControl
    {
        private const string ReleaseRepository = "Himpq/g-helper-himpqenhanced";
        private const string VersionSuffix = "-0he";
        private static readonly Regex HimpqEnhancedVersionRegex = new(
            @"^v?(?<main>\d+\.\d+\.\d+(?:\.\d+)?)-(?<he>\d+)he$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly string ReleasesUrl = $"https://github.com/{ReleaseRepository}/releases";
        private static readonly string LatestReleaseApiUrl = $"https://api.github.com/repos/{ReleaseRepository}/releases/latest";

        SettingsForm settings;

        public string versionUrl = ReleasesUrl;
        public bool update = false;

        static long lastUpdate;

        public AutoUpdateControl(SettingsForm settingsForm)
        {
            settings = settingsForm;
            settings.SetVersionLabel(Properties.Strings.VersionLabel + $": {GetDisplayVersion()}");
        }

        public void CheckForUpdates()
        {
            // Run update once per 12 hours
            if (Math.Abs(DateTimeOffset.Now.ToUnixTimeSeconds() - lastUpdate) < 43200) return;
            lastUpdate = DateTimeOffset.Now.ToUnixTimeSeconds();

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                CheckForUpdatesAsync();
            });
        }

        public void Update()
        {
            if (update)
            {
                Task.Run(() =>
                {
                    CheckForUpdatesAsync(true);
                });
            } else
            {
                LoadReleases();
            }
        }

        public void LoadReleases()
        {
            try
            {
                Process.Start(new ProcessStartInfo(versionUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to open releases page:" + ex.Message);
            }
        }

        async void CheckForUpdatesAsync(bool force = false)
        {

            if (AppConfig.Is("skip_updates")) return;

            try
            {

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "G-Helper App");
                    var json = await httpClient.GetStringAsync(LatestReleaseApiUrl);
                    var config = JsonSerializer.Deserialize<JsonElement>(json);
                    var tag = config.GetProperty("tag_name").ToString();
                    var releaseDisplayVersion = NormalizeDisplayVersion(tag);
                    var localDisplayVersion = GetDisplayVersion();
                    if (!TryGetComparableVersion(tag, out var gitVersion))
                    {
                        Logger.WriteLine($"Ignore unsupported {ReleaseRepository} release version: {tag}");
                        return;
                    }

                    if (!TryGetComparableVersion(localDisplayVersion, out var appVersion))
                    {
                        Logger.WriteLine($"Ignore update check because local version does not use HimpqEnhanced version format: {localDisplayVersion}");
                        return;
                    }

                    var assets = config.GetProperty("assets");

                    string? url = null;

                    for (int i = 0; i < assets.GetArrayLength(); i++)
                    {
                        if (assets[i].GetProperty("browser_download_url").ToString().Contains(".zip"))
                            url = assets[i].GetProperty("browser_download_url").ToString();
                    }

                    if (url is null)
                    {
                        Logger.WriteLine($"Latest {ReleaseRepository} release {releaseDisplayVersion} has no zip asset");
                        return;
                    }

                    if (gitVersion.CompareTo(appVersion) > 0)
                    {
                        versionUrl = url;
                        update = true;
                        settings.SetVersionLabel(Properties.Strings.DownloadUpdate + $": {localDisplayVersion} → {releaseDisplayVersion}", true);

                        string[] args = Environment.GetCommandLineArgs();
                        if (force || args.Length > 1 && args[1] == "autoupdate")
                        {
                            AutoUpdate(url);
                            return;
                        }

                        if (AppConfig.GetString("skip_version") != releaseDisplayVersion)
                        {
                            DialogResult dialogResult = DialogResult.No;

                            settings.Invoke((System.Windows.Forms.MethodInvoker)delegate
                            {
                                dialogResult = MessageBox.Show(settings, Properties.Strings.DownloadUpdate + ": G-Helper HimpqEnhanced " + releaseDisplayVersion + "?", "Update", MessageBoxButtons.YesNo);
                            });
                            
                            if (dialogResult == DialogResult.Yes)
                                AutoUpdate(url);
                            else
                                AppConfig.Set("skip_version", releaseDisplayVersion);
                        }

                    }
                    else
                    {
                        Logger.WriteLine($"Latest version {localDisplayVersion}");
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to check for updates:" + ex.Message);
            }

        }

        private static string GetDisplayVersion()
        {
            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return informationalVersion.Split('+')[0];

            var appVersion = Assembly.GetExecutingAssembly().GetName().Version
                ?? throw new InvalidOperationException("Assembly version is missing");
            return $"{appVersion.Major}.{appVersion.Minor}.{Math.Max(appVersion.Build, 0)}{VersionSuffix}";
        }

        private static string NormalizeDisplayVersion(string tag)
        {
            string normalized = tag.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[1..];
            return normalized;
        }

        private readonly record struct HimpqEnhancedVersion(Version MainVersion, int EnhancedVersion) : IComparable<HimpqEnhancedVersion>
        {
            public int CompareTo(HimpqEnhancedVersion other)
            {
                int mainCompare = MainVersion.CompareTo(other.MainVersion);
                return mainCompare != 0 ? mainCompare : EnhancedVersion.CompareTo(other.EnhancedVersion);
            }
        }

        private static bool TryGetComparableVersion(string versionText, out HimpqEnhancedVersion version)
        {
            version = default;
            string normalized = NormalizeDisplayVersion(versionText);
            var match = HimpqEnhancedVersionRegex.Match(normalized);
            if (!match.Success)
                return false;

            if (!Version.TryParse(match.Groups["main"].Value, out var mainVersion))
                return false;

            if (!int.TryParse(match.Groups["he"].Value, out int enhancedVersion))
                return false;

            version = new HimpqEnhancedVersion(
                new Version(
                    mainVersion.Major,
                    mainVersion.Minor,
                    Math.Max(mainVersion.Build, 0),
                    Math.Max(mainVersion.Revision, 0)),
                enhancedVersion);
            return true;
        }

        public static string EscapeString(string input)
        {
            return Regex.Replace(Regex.Replace(input, @"\[|\]", "`$0"), @"\'", "''");
        }

        async void AutoUpdate(string requestUri)
        {

            Uri uri = new Uri(requestUri);
            string zipName = Path.GetFileName(uri.LocalPath);

            string exeLocation = Application.ExecutablePath;
            string exeDir = Path.GetDirectoryName(exeLocation);
            //exeDir = "C:\\Program Files\\GHelper";
            string exeName = Path.GetFileName(exeLocation);
            string zipLocation = exeDir + "\\" + zipName;

            using (WebClient client = new WebClient())
            {

                client.Headers.Add("User-Agent", "G-Helper App");
                Logger.WriteLine(requestUri);
                Logger.WriteLine(exeDir);
                Logger.WriteLine(zipName);
                Logger.WriteLine(exeName);

                try
                {
                    client.DownloadFile(uri, zipLocation);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.Message);
                    if (!ProcessHelper.IsUserAdministrator())
                    {
                        ProcessHelper.RunAsAdmin("autoupdate");
                        Application.Exit();
                    } else
                    {
                        LoadReleases();
                    }
                    return;
                }

                string command = $"$ErrorActionPreference = \"Stop\"; Set-Location -Path '{EscapeString(exeDir)}'; Wait-Process -Name \"GHelper\"; Expand-Archive \"{zipName}\" -DestinationPath . -Force; Remove-Item \"{zipName}\" -Force; \".\\{exeName}\"; ";
                Logger.WriteLine(command);

                try
                {
                    var cmd = new Process();
                    cmd.StartInfo.WorkingDirectory = exeDir;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.FileName = "powershell";
                    cmd.StartInfo.Arguments = command;
                    if (ProcessHelper.IsUserAdministrator()) cmd.StartInfo.Verb = "runas";
                    cmd.Start();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.Message);
                }

                Application.Exit();
            }

        }

    }
}
