using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace SkkoaStudio.Core.Updates;

public sealed class SkkoaUpdateService
{
    public const string DefaultWebManifestUrl = "https://skkoa.toyotech.dev/download/studio/updates/win-x64/manifest.json";
    public const string DefaultGitHubManifestUrl = "https://raw.githubusercontent.com/Team-ToyoTech/skkoa-web/main/download/studio/updates/win-x64/manifest.json";
    public const string ManifestUrlEnvironmentVariable = "SKKOA_STUDIO_UPDATE_MANIFEST_URL";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly HttpClient httpClient;

    public SkkoaUpdateService(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
    }

    public static string GetCurrentVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(SkkoaUpdateService).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return TrimVersionMetadata(informationalVersion);
        }

        Version? version = assembly.GetName().Version;
        if (version == null)
        {
            return "0.0.0";
        }

        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    public static IReadOnlyList<Uri> GetManifestUris(string? configuredManifestUrl)
    {
        List<string> candidates = [];
        string? environmentValue = Environment.GetEnvironmentVariable(ManifestUrlEnvironmentVariable);
        AddConfiguredUrls(candidates, environmentValue);
        AddConfiguredUrls(candidates, configuredManifestUrl);
        candidates.Add(DefaultWebManifestUrl);
        candidates.Add(DefaultGitHubManifestUrl);

        return candidates
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null)
            .Where(uri => uri != null)
            .Select(uri => uri!)
            .DistinctBy(uri => uri.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<SkkoaUpdateCheckResult> CheckForUpdatesAsync(
        IEnumerable<Uri> manifestUris,
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        List<string> errors = [];
        SkkoaUpdateManifest? bestManifest = null;
        Uri? bestManifestUri = null;

        foreach (Uri manifestUri in manifestUris)
        {
            try
            {
                SkkoaUpdateManifest manifest = await FetchManifestAsync(manifestUri, cancellationToken);
                ValidateManifest(manifest);
                if (!IsNewerVersion(manifest.Version, currentVersion))
                {
                    continue;
                }

                if (bestManifest == null || CompareVersions(manifest.Version, bestManifest.Version) > 0)
                {
                    bestManifest = manifest;
                    bestManifestUri = manifestUri;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{manifestUri}: {ex.Message}");
            }
        }

        return new SkkoaUpdateCheckResult(currentVersion, bestManifest, bestManifestUri, errors);
    }

    public async Task<SkkoaUpdateManifest> FetchManifestAsync(Uri manifestUri, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, manifestUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SKKOA-Studio", GetCurrentVersion()));

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        SkkoaUpdateManifest? manifest = await JsonSerializer.DeserializeAsync<SkkoaUpdateManifest>(
            stream,
            JsonOptions,
            cancellationToken);
        return manifest ?? throw new InvalidOperationException("Update manifest is empty.");
    }

    public static void ValidateManifest(SkkoaUpdateManifest manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidOperationException("Unsupported update manifest schema version.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Update manifest does not contain a version.");
        }

        if (!TryParseVersion(manifest.Version, out _))
        {
            throw new InvalidOperationException("Update manifest version is invalid: " + manifest.Version);
        }

        if (manifest.Files.Count == 0)
        {
            throw new InvalidOperationException("Update manifest does not contain files.");
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SkkoaUpdateFile file in manifest.Files)
        {
            if (!SkkoaUpdatePaths.IsSafeRelativePath(file.Path))
            {
                throw new InvalidOperationException("Update manifest contains an unsafe file path: " + file.Path);
            }

            if (!paths.Add(SkkoaUpdatePaths.NormalizeRelativePath(file.Path)))
            {
                throw new InvalidOperationException("Update manifest contains a duplicate file path: " + file.Path);
            }

            if (!IsSha256(file.Sha256))
            {
                throw new InvalidOperationException("Update manifest contains an invalid SHA-256 hash for " + file.Path);
            }
        }

        foreach (string path in manifest.Delete)
        {
            if (!SkkoaUpdatePaths.IsSafeRelativePath(path))
            {
                throw new InvalidOperationException("Update manifest contains an unsafe delete path: " + path);
            }
        }
    }

    public static bool IsNewerVersion(string candidateVersion, string currentVersion)
    {
        return CompareVersions(candidateVersion, currentVersion) > 0;
    }

    public static int CompareVersions(string left, string right)
    {
        if (TryParseVersion(left, out Version? leftVersion) && TryParseVersion(right, out Version? rightVersion))
        {
            return leftVersion!.CompareTo(rightVersion!);
        }

        return string.Compare(TrimVersionMetadata(left), TrimVersionMetadata(right), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddConfiguredUrls(List<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        candidates.AddRange(value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool TryParseVersion(string value, out Version? version)
    {
        return Version.TryParse(TrimVersionMetadata(value), out version);
    }

    private static string TrimVersionMetadata(string value)
    {
        string result = value.Trim();
        int metadataIndex = result.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
        {
            result = result[..metadataIndex];
        }

        int prereleaseIndex = result.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            result = result[..prereleaseIndex];
        }

        return result;
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        return value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    }
}

public sealed class SkkoaUpdateCheckResult
{
    public SkkoaUpdateCheckResult(
        string currentVersion,
        SkkoaUpdateManifest? manifest,
        Uri? manifestUri,
        IReadOnlyList<string> errors)
    {
        CurrentVersion = currentVersion;
        Manifest = manifest;
        ManifestUri = manifestUri;
        Errors = errors;
    }

    public string CurrentVersion { get; }
    public SkkoaUpdateManifest? Manifest { get; }
    public Uri? ManifestUri { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsUpdateAvailable => Manifest != null && ManifestUri != null;
}
