namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaCompilerLocator
{
    private readonly string appBaseDirectory;

    public SkkoaCompilerLocator(string? appBaseDirectory = null)
    {
        this.appBaseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory;
    }

    public string? FindCompiler(string? configuredPath = null)
    {
        foreach (string candidate in CandidateCompilerPaths(configuredPath))
        {
            try
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch
            {
            }
        }
        return null;
    }

    public string? FindLibPath(string? configuredPath = null)
    {
        foreach (string candidate in CandidateLibPaths(configuredPath))
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch
            {
            }
        }
        return null;
    }

    public string[] FindToolchainPathEntries()
    {
        string localToolchain = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SKKOA Studio", "skkoa", "toolchain", "msys64");

        string[] roots =
        [
            Path.Combine(appBaseDirectory, "tools", "skkoa", "toolchain", "msys64"),
            Path.Combine(GetRepositoryEditorDirectory(), "tools", "skkoa", "toolchain", "msys64"),
            localToolchain
        ];

        return roots
            .SelectMany(root => new[]
            {
                Path.Combine(root, "mingw64", "bin"),
                Path.Combine(root, "usr", "bin")
            })
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<string> CandidateCompilerPaths(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        string editorDir = GetRepositoryEditorDirectory();
        yield return Path.Combine(appBaseDirectory, "tools", "skkoa", "skkoa.exe");
        yield return Path.Combine(appBaseDirectory, "tools", "skkoa", "bin", "skkoa.exe");
        yield return Path.Combine(editorDir, "tools", "skkoa", "skkoa.exe");
        yield return Path.Combine(editorDir, "tools", "skkoa", "bin", "skkoa.exe");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "editor", "tools", "skkoa", "skkoa.exe");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "compiler", "build", "skkoa.exe");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "compiler", "skkoa.exe");
    }

    private IEnumerable<string> CandidateLibPaths(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        string editorDir = GetRepositoryEditorDirectory();
        yield return Path.Combine(appBaseDirectory, "tools", "skkoa", "lib");
        yield return Path.Combine(editorDir, "tools", "skkoa", "lib");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "editor", "tools", "skkoa", "lib");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "compiler", "lib");
    }

    private string GetRepositoryEditorDirectory()
    {
        DirectoryInfo? current = new(appBaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "editor");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            if (current.Name.Equals("editor", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "editor");
    }
}
