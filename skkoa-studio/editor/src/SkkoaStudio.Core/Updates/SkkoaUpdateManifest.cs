namespace SkkoaStudio.Core.Updates;

public sealed class SkkoaUpdateManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Application { get; set; } = "SKKOA Studio";
    public string Version { get; set; } = "";
    public string Runtime { get; set; } = "win-x64";
    public DateTimeOffset? PublishedAt { get; set; }
    public string MinimumUpdaterVersion { get; set; } = "";
    public string FilesBaseUrl { get; set; } = "";
    public string InstallerUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public List<SkkoaUpdateFile> Files { get; set; } = [];
    public List<string> Delete { get; set; } = [];
}

public sealed class SkkoaUpdateFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}
