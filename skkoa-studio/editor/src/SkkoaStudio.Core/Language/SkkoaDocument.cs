namespace SkkoaStudio.Core.Language;

public sealed class SkkoaDocument
{
    public string? FilePath { get; set; }
    public string DisplayName { get; set; } = "untitled.koa";
    public string Text { get; set; } = "";
    public bool IsDirty { get; set; }
    public bool IsUntitled => string.IsNullOrWhiteSpace(FilePath);
}

public sealed class SkkoaProject
{
    public string Name { get; set; } = "Untitled";
    public string Entry { get; set; } = "main.koa";
    public List<string> Files { get; set; } = [];
    public List<string> LibPaths { get; set; } = [];
    public string OutputName { get; set; } = "";
    public string? ProjectPath { get; set; }
    public string ProjectDirectory => string.IsNullOrWhiteSpace(ProjectPath)
        ? Environment.CurrentDirectory
        : Path.GetDirectoryName(ProjectPath) ?? Environment.CurrentDirectory;
}
