using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SkkoaStudio.Core.Updates;

public sealed class SkkoaUpdateApplicator
{
    private static readonly JsonSerializerOptions ManifestWriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient httpClient;

    public SkkoaUpdateApplicator(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<SkkoaUpdateApplyResult> ApplyAsync(
        SkkoaUpdateManifest manifest,
        Uri manifestUri,
        string installDirectory,
        IProgress<SkkoaUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SkkoaUpdateService.ValidateManifest(manifest);

        string installRoot = Path.GetFullPath(installDirectory);
        string tempRoot = Path.Combine(Path.GetTempPath(), "SKKOA Studio", "updates", Guid.NewGuid().ToString("N"));
        string stagingRoot = Path.Combine(tempRoot, "files");
        string backupRoot = Path.Combine(tempRoot, "backup");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(backupRoot);

        List<SkkoaUpdateFile> filesToUpdate = [];
        try
        {
            progress?.Report(new SkkoaUpdateProgress("scan", "Checking installed files", 0, manifest.Files.Count));
            for (int i = 0; i < manifest.Files.Count; i++)
            {
                SkkoaUpdateFile file = manifest.Files[i];
                string localPath = SkkoaUpdatePaths.GetSafeFullPath(installRoot, file.Path);
                bool needsUpdate = !File.Exists(localPath) ||
                    !await FileMatchesHashAsync(localPath, file.Sha256, cancellationToken);
                if (needsUpdate)
                {
                    filesToUpdate.Add(file);
                }

                progress?.Report(new SkkoaUpdateProgress("scan", file.Path, i + 1, manifest.Files.Count));
            }

            long downloadedBytes = 0;
            for (int i = 0; i < filesToUpdate.Count; i++)
            {
                SkkoaUpdateFile file = filesToUpdate[i];
                progress?.Report(new SkkoaUpdateProgress("download", file.Path, i, filesToUpdate.Count));
                string stagedPath = SkkoaUpdatePaths.GetSafeFullPath(stagingRoot, file.Path);
                string? stagedDirectory = Path.GetDirectoryName(stagedPath);
                if (!string.IsNullOrWhiteSpace(stagedDirectory))
                {
                    Directory.CreateDirectory(stagedDirectory);
                }

                Uri fileUri = SkkoaUpdatePaths.ResolveFileUri(manifest, manifestUri, file);
                downloadedBytes += await DownloadAndVerifyFileAsync(fileUri, stagedPath, file, cancellationToken);
                progress?.Report(new SkkoaUpdateProgress("download", file.Path, i + 1, filesToUpdate.Count));
            }

            IReadOnlyList<string> appliedPaths = await ApplyStagedFilesAsync(
                installRoot,
                stagingRoot,
                backupRoot,
                manifest,
                filesToUpdate,
                progress,
                cancellationToken);

            WriteCurrentManifest(installRoot, manifest);
            return new SkkoaUpdateApplyResult(filesToUpdate.Count, downloadedBytes, appliedPaths);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<bool> FileMatchesHashAsync(string path, string expectedSha256, CancellationToken cancellationToken)
    {
        string hash = await ComputeSha256Async(path, cancellationToken);
        return string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<long> DownloadAndVerifyFileAsync(
        Uri fileUri,
        string targetPath,
        SkkoaUpdateFile file,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, fileUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SKKOA-Studio-Updater", "0.1.0"));

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (FileStream output = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        FileInfo info = new(targetPath);
        if (file.Size >= 0 && info.Length != file.Size)
        {
            throw new InvalidOperationException($"Downloaded file size mismatch for {file.Path}.");
        }

        if (!await FileMatchesHashAsync(targetPath, file.Sha256, cancellationToken))
        {
            throw new InvalidOperationException($"Downloaded file hash mismatch for {file.Path}.");
        }

        return info.Length;
    }

    private static async Task<IReadOnlyList<string>> ApplyStagedFilesAsync(
        string installRoot,
        string stagingRoot,
        string backupRoot,
        SkkoaUpdateManifest manifest,
        IReadOnlyList<SkkoaUpdateFile> filesToUpdate,
        IProgress<SkkoaUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<string> appliedPaths = [];
        HashSet<string> backupPaths = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            int total = filesToUpdate.Count + manifest.Delete.Count;
            int completed = 0;

            foreach (SkkoaUpdateFile file in filesToUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = SkkoaUpdatePaths.NormalizeRelativePath(file.Path);
                string sourcePath = SkkoaUpdatePaths.GetSafeFullPath(stagingRoot, relativePath);
                string targetPath = SkkoaUpdatePaths.GetSafeFullPath(installRoot, relativePath);
                BackupExistingFile(backupRoot, relativePath, targetPath, backupPaths);

                string? targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                appliedPaths.Add(relativePath);
                File.Copy(sourcePath, targetPath, overwrite: true);
                completed++;
                progress?.Report(new SkkoaUpdateProgress("apply", relativePath, completed, total));
            }

            foreach (string deletePath in manifest.Delete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = SkkoaUpdatePaths.NormalizeRelativePath(deletePath);
                string targetPath = SkkoaUpdatePaths.GetSafeFullPath(installRoot, relativePath);
                BackupExistingFile(backupRoot, relativePath, targetPath, backupPaths);
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                    appliedPaths.Add(relativePath);
                }

                completed++;
                progress?.Report(new SkkoaUpdateProgress("apply", relativePath, completed, total));
            }

            return appliedPaths;
        }
        catch
        {
            RollBackAppliedFiles(installRoot, backupRoot, appliedPaths, backupPaths);
            throw;
        }
    }

    private static void BackupExistingFile(
        string backupRoot,
        string relativePath,
        string targetPath,
        HashSet<string> backupPaths)
    {
        if (!File.Exists(targetPath) || !backupPaths.Add(relativePath))
        {
            return;
        }

        string backupPath = SkkoaUpdatePaths.GetSafeFullPath(backupRoot, relativePath);
        string? backupDirectory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        File.Copy(targetPath, backupPath, overwrite: true);
    }

    private static void RollBackAppliedFiles(
        string installRoot,
        string backupRoot,
        IEnumerable<string> appliedPaths,
        HashSet<string> backupPaths)
    {
        foreach (string relativePath in appliedPaths.Reverse())
        {
            try
            {
                string targetPath = SkkoaUpdatePaths.GetSafeFullPath(installRoot, relativePath);
                string backupPath = SkkoaUpdatePaths.GetSafeFullPath(backupRoot, relativePath);
                if (backupPaths.Contains(relativePath) && File.Exists(backupPath))
                {
                    File.Copy(backupPath, targetPath, overwrite: true);
                }
                else if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
            catch
            {
                // Best-effort rollback. The original exception is more useful to the caller.
            }
        }
    }

    private static void WriteCurrentManifest(string installRoot, SkkoaUpdateManifest manifest)
    {
        string manifestPath = Path.Combine(installRoot, "updates", "current-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        string json = JsonSerializer.Serialize(manifest, ManifestWriteOptions);
        File.WriteAllText(manifestPath, json, Encoding.UTF8);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}

public sealed class SkkoaUpdateApplyResult
{
    public SkkoaUpdateApplyResult(int changedFileCount, long downloadedBytes, IReadOnlyList<string> updatedPaths)
    {
        ChangedFileCount = changedFileCount;
        DownloadedBytes = downloadedBytes;
        UpdatedPaths = updatedPaths;
    }

    public int ChangedFileCount { get; }
    public long DownloadedBytes { get; }
    public IReadOnlyList<string> UpdatedPaths { get; }
}

public sealed class SkkoaUpdateProgress
{
    public SkkoaUpdateProgress(string stage, string message, int completed, int total)
    {
        Stage = stage;
        Message = message;
        Completed = completed;
        Total = total;
    }

    public string Stage { get; }
    public string Message { get; }
    public int Completed { get; }
    public int Total { get; }
}
