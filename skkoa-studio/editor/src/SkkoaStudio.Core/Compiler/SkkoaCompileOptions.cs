namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaCompileOptions
{
    public required string SourcePath { get; init; }
    public string? OutputPath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? LibPath { get; init; }
    public bool CheckOnly { get; init; }
    public bool DiagnosticsJson { get; init; }
    public bool EmitAstJson { get; init; }
    public bool NoLink { get; init; }
}

public sealed class SkkoaCompileResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public string? OutputPath { get; init; }
    public IReadOnlyList<SkkoaDiagnostic> Diagnostics { get; init; } = [];
    public bool Success => ExitCode == 0 && Diagnostics.All(d => d.IsWarning);
}

public sealed class SkkoaProcessResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public TimeSpan Duration { get; init; }
}
