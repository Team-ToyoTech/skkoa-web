using System.Net;
using System.Security.Cryptography;
using System.Text;
using SkkoaStudio.Core.Updates;
using Xunit;

namespace SkkoaStudio.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("0.1.1", "0.1.0", true)]
    [InlineData("0.2.0", "0.1.9", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("0.0.9", "0.1.0", false)]
    public void IsNewerVersion_ComparesSemanticVersions(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, SkkoaUpdateService.IsNewerVersion(candidate, current));
    }

    [Fact]
    public void GetSafeFullPath_RejectsTraversal()
    {
        string root = Path.Combine(Path.GetTempPath(), "skkoa-update-test");

        Assert.True(SkkoaUpdatePaths.IsSafeRelativePath("tools/skkoa/skkoa.exe"));
        Assert.False(SkkoaUpdatePaths.IsSafeRelativePath("../SkkoaStudio.exe"));
        Assert.Throws<InvalidOperationException>(() => SkkoaUpdatePaths.GetSafeFullPath(root, "../SkkoaStudio.exe"));
    }

    [Fact]
    public async Task ApplyAsync_DownloadsOnlyChangedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "skkoa-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "same.txt"), Encoding.UTF8.GetBytes("same"));
            await File.WriteAllBytesAsync(Path.Combine(root, "changed.txt"), Encoding.UTF8.GetBytes("old"));

            byte[] changedBytes = Encoding.UTF8.GetBytes("new");
            byte[] addedBytes = Encoding.UTF8.GetBytes("added");
            SkkoaUpdateManifest manifest = new()
            {
                Version = "0.1.1",
                FilesBaseUrl = "files/",
                Files =
                [
                    FileEntry("same.txt", Encoding.UTF8.GetBytes("same")),
                    FileEntry("changed.txt", changedBytes),
                    FileEntry("nested/added.txt", addedBytes)
                ]
            };

            StubUpdateHandler handler = new(new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["files/changed.txt"] = changedBytes,
                ["files/nested/added.txt"] = addedBytes
            });
            using HttpClient httpClient = new(handler);
            SkkoaUpdateApplicator applicator = new(httpClient);

            SkkoaUpdateApplyResult result = await applicator.ApplyAsync(
                manifest,
                new Uri("https://updates.example/manifest.json"),
                root);

            Assert.Equal(2, result.ChangedFileCount);
            Assert.Equal(["files/changed.txt", "files/nested/added.txt"], handler.RequestPaths);
            Assert.Equal("same", await File.ReadAllTextAsync(Path.Combine(root, "same.txt"), Encoding.UTF8));
            Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(root, "changed.txt"), Encoding.UTF8));
            Assert.Equal("added", await File.ReadAllTextAsync(Path.Combine(root, "nested", "added.txt"), Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static SkkoaUpdateFile FileEntry(string path, byte[] bytes)
    {
        return new SkkoaUpdateFile
        {
            Path = path,
            Url = path,
            Size = bytes.Length,
            Sha256 = Sha256(bytes)
        };
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class StubUpdateHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> files;

        public StubUpdateHandler(Dictionary<string, byte[]> files)
        {
            this.files = files;
        }

        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
            RequestPaths.Add(path);
            if (!files.TryGetValue(path, out byte[]? bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        }
    }
}
