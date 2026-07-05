using System.Text.Json;

namespace SkkoaStudio.Core.Settings;

public sealed class SkkoaStudioSettings
{
    public string Theme { get; set; } = "Dark";
    public string PrimaryColor { get; set; } = "#a259ff";
    public string FontFamily { get; set; } = "Consolas";
    public float FontSize { get; set; } = 12;
    public int TabSize { get; set; } = 4;
    public bool InsertSpaces { get; set; } = true;
    public bool FormatOnEnter { get; set; } = true;
    public bool FormatOnSave { get; set; }
    public bool DiagnosticsOnType { get; set; } = true;
    public string CompilerPath { get; set; } = "";
    public string LibPath { get; set; } = "";
    public List<string> RecentFiles { get; set; } = [];
    public List<string> RecentProjects { get; set; } = [];
}

public sealed class SkkoaSettingsService
{
    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public SkkoaSettingsService()
    {
        SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SKKOA Studio");
        SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    }

    public SkkoaStudioSettings Load()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (!File.Exists(SettingsPath))
            {
                SkkoaStudioSettings defaults = new();
                Save(defaults);
                return defaults;
            }

            SkkoaStudioSettings? settings = JsonSerializer.Deserialize<SkkoaStudioSettings>(
                File.ReadAllText(SettingsPath),
                JsonOptions());
            return Sanitize(settings ?? new SkkoaStudioSettings());
        }
        catch
        {
            return new SkkoaStudioSettings();
        }
    }

    public void Save(SkkoaStudioSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Sanitize(settings), JsonOptions()));
            if (File.Exists(SettingsPath))
            {
                File.Replace(tempPath, SettingsPath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, SettingsPath);
            }
        }
        catch
        {
            // Settings are user convenience state. A save failure must not prevent editing or closing the IDE.
        }
    }

    public void AddRecentFile(SkkoaStudioSettings settings, string path)
    {
        AddRecent(settings.RecentFiles, path);
        Save(settings);
    }

    public void AddRecentProject(SkkoaStudioSettings settings, string path)
    {
        AddRecent(settings.RecentProjects, path);
        Save(settings);
    }

    private static void AddRecent(List<string> list, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        list.RemoveAll(item => item.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > 12)
        {
            list.RemoveRange(12, list.Count - 12);
        }
    }

    private static SkkoaStudioSettings Sanitize(SkkoaStudioSettings settings)
    {
        if (!string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            settings.Theme = "Dark";
        }
        settings.FontFamily = string.IsNullOrWhiteSpace(settings.FontFamily) ? "Consolas" : settings.FontFamily;
        settings.PrimaryColor = IsHexColor(settings.PrimaryColor) ? settings.PrimaryColor : "#a259ff";
        settings.FontSize = Math.Clamp(settings.FontSize, 8, 36);
        settings.TabSize = Math.Clamp(settings.TabSize, 1, 12);
        settings.CompilerPath ??= "";
        settings.LibPath ??= "";
        settings.RecentFiles = CleanRecentList(settings.RecentFiles, File.Exists);
        settings.RecentProjects = CleanRecentList(settings.RecentProjects, File.Exists);
        return settings;
    }

    private static List<string> CleanRecentList(IEnumerable<string>? source, Func<string, bool> exists)
    {
        return (source ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path =>
            {
                try
                {
                    return exists(path);
                }
                catch
                {
                    return false;
                }
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static bool IsHexColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return false;
        }
        for (int i = 1; i < value.Length; i++)
        {
            bool hex = value[i] is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hex)
            {
                return false;
            }
        }
        return true;
    }
}
