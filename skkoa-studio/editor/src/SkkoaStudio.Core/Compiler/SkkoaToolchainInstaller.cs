namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaToolchainInstaller
{
    private readonly SkkoaProcessRunner runner = new();

    public async Task<SkkoaProcessResult> InstallAsync(
        string scriptPath,
        string installRoot,
        Action<string>? output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(scriptPath))
            {
                return new SkkoaProcessResult
                {
                    ExitCode = 2,
                    StandardError = "설치 스크립트를 찾을 수 없습니다: " + scriptPath
                };
            }

            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return new SkkoaProcessResult
                {
                    ExitCode = 2,
                    StandardError = "설치 경로가 비어 있습니다."
                };
            }

            Directory.CreateDirectory(installRoot);
        }
        catch (Exception ex)
        {
            return new SkkoaProcessResult
            {
                ExitCode = 2,
                StandardError = "도구 체인 설치를 준비할 수 없습니다: " + ex.Message
            };
        }

        string binDir = Path.Combine(installRoot, "bin");
        string toolchainRoot = Path.Combine(installRoot, "toolchain");
        Dictionary<string, string> env = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SKKOA_INSTALL_ROOT"] = installRoot,
            ["SKKOA_BIN_DIR"] = binDir,
            ["SKKOA_TOOLCHAIN_ROOT"] = toolchainRoot,
            ["SKKOA_SKIP_PATH_UPDATE"] = "1"
        };

        return await runner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath],
            Path.GetDirectoryName(scriptPath),
            env,
            output,
            output,
            cancellationToken).ConfigureAwait(false);
    }
}
