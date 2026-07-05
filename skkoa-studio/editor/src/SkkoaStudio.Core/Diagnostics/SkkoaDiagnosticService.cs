using SkkoaStudio.Core.Compiler;

namespace SkkoaStudio.Core.Diagnostics;

public sealed class SkkoaDiagnosticService
{
    private readonly SkkoaCompilerService compilerService;

    public SkkoaDiagnosticService(SkkoaCompilerService compilerService)
    {
        this.compilerService = compilerService;
    }

    public async Task<IReadOnlyList<SkkoaDiagnostic>> GetDiagnosticsForTextAsync(
        string text,
        string displayPath,
        string? workingDirectory,
        string? libPath,
        CancellationToken cancellationToken = default)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "skkoa-studio-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            string tempFile = Path.Combine(tempRoot, Path.GetFileName(string.IsNullOrWhiteSpace(displayPath) ? "untitled.koa" : displayPath));
            if (!tempFile.EndsWith(".koa", StringComparison.OrdinalIgnoreCase))
            {
                tempFile += ".koa";
            }

            await File.WriteAllTextAsync(tempFile, text, System.Text.Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            SkkoaCompileResult result = await compilerService.CheckAsync(new SkkoaCompileOptions
            {
                SourcePath = tempFile,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(tempFile),
                LibPath = libPath,
                CheckOnly = true,
                DiagnosticsJson = true
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            return result.Diagnostics
                .Select(d =>
                {
                    d.File = displayPath;
                    return d;
                })
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return
            [
                new SkkoaDiagnostic
                {
                    Severity = "warning",
                    Code = "SKKDIAG",
                    Message = "실시간 진단을 실행할 수 없습니다: " + ex.Message,
                    File = displayPath,
                    Line = 1,
                    Column = 1,
                    Length = 1
                }
            ];
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
