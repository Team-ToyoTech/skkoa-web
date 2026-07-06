namespace SkkoaStudio.Core.Updates;

public static class SkkoaUpdatePaths
{
    public static string NormalizeRelativePath(string path)
    {
        return (path ?? "").Replace('\\', '/').Trim('/');
    }

    public static bool IsSafeRelativePath(string path)
    {
        string normalized = NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (Path.IsPathRooted(normalized) || normalized.Contains(':'))
        {
            return false;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.All(segment => segment != "." && segment != "..");
    }

    public static string GetSafeFullPath(string rootDirectory, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            throw new InvalidOperationException("Update manifest contains an unsafe path: " + relativePath);
        }

        string root = Path.GetFullPath(rootDirectory);
        string combined = Path.GetFullPath(Path.Combine(
            root,
            NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar)));

        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !combined.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update manifest path escapes the install directory: " + relativePath);
        }

        return combined;
    }

    public static Uri ResolveFileUri(SkkoaUpdateManifest manifest, Uri manifestUri, SkkoaUpdateFile file)
    {
        string relativeUrl = NormalizeRelativePath(string.IsNullOrWhiteSpace(file.Url) ? file.Path : file.Url);
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri;
        }

        Uri baseUri = ResolveBaseUri(manifest, manifestUri);
        return new Uri(baseUri, relativeUrl);
    }

    private static Uri ResolveBaseUri(SkkoaUpdateManifest manifest, Uri manifestUri)
    {
        if (!string.IsNullOrWhiteSpace(manifest.FilesBaseUrl))
        {
            string value = manifest.FilesBaseUrl.Replace('\\', '/');
            Uri baseUri = Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute)
                ? absolute
                : new Uri(new Uri(manifestUri, "."), value);
            return EnsureTrailingSlash(baseUri);
        }

        return new Uri(manifestUri, ".");
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        string value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(value + "/");
    }
}
