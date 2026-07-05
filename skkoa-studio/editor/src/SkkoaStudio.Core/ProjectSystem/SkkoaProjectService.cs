using System.Text.Json;
using SkkoaStudio.Core.Language;

namespace SkkoaStudio.Core.ProjectSystem;

public sealed class SkkoaProjectService
{
    public SkkoaProject Create(string name, string directory)
    {
        string safeName = SanitizeName(name);
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, safeName + ".skkoaproj");
        string entryPath = Path.Combine(directory, "main.koa");
        if (!File.Exists(entryPath))
        {
            File.WriteAllText(entryPath, "시작" + Environment.NewLine + "    출력 \"안녕하세요\"" + Environment.NewLine + "끝", System.Text.Encoding.UTF8);
        }

        SkkoaProject project = new()
        {
            Name = safeName,
            Entry = "main.koa",
            Files = ["main.koa"],
            OutputName = safeName,
            ProjectPath = projectPath
        };
        Save(project, projectPath);
        return project;
    }

    public SkkoaProject Load(string path)
    {
        SkkoaProject project;
        try
        {
            project = JsonSerializer.Deserialize<SkkoaProject>(File.ReadAllText(path), JsonOptions())
                ?? new SkkoaProject();
        }
        catch (JsonException)
        {
            project = new SkkoaProject();
        }
        project.ProjectPath = path;
        project.Name = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileNameWithoutExtension(path) : project.Name;
        project.Entry = string.IsNullOrWhiteSpace(project.Entry) ? "main.koa" : project.Entry;
        project.OutputName ??= "";
        project.Files = (project.Files ?? [])
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        project.LibPaths = (project.LibPaths ?? [])
            .Where(libPath => !string.IsNullOrWhiteSpace(libPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!project.Files.Contains(project.Entry, StringComparer.OrdinalIgnoreCase))
        {
            project.Files.Insert(0, project.Entry);
        }
        return project;
    }

    public void Save(SkkoaProject project, string? path = null)
    {
        string target = path ?? project.ProjectPath ?? throw new InvalidOperationException("프로젝트 경로가 없습니다.");
        project.ProjectPath = target;
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
        File.WriteAllText(target, JsonSerializer.Serialize(project, JsonOptions()), System.Text.Encoding.UTF8);
    }

    public string GetEntryPath(SkkoaProject project)
    {
        return Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.Entry));
    }

    public string[] GetLibPaths(SkkoaProject project)
    {
        return project.LibPaths.Select(path => Path.GetFullPath(Path.Combine(project.ProjectDirectory, path))).ToArray();
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string SanitizeName(string name)
    {
        string value = string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }
        return string.IsNullOrWhiteSpace(value) ? "Untitled" : value;
    }
}
