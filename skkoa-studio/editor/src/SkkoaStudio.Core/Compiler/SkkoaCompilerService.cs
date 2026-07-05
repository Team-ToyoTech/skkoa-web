namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaCompilerService
{
    private readonly SkkoaCompilerLocator locator;
    private readonly SkkoaProcessRunner runner;

    public SkkoaCompilerService(SkkoaCompilerLocator? locator = null, SkkoaProcessRunner? runner = null)
    {
        this.locator = locator ?? new SkkoaCompilerLocator();
        this.runner = runner ?? new SkkoaProcessRunner();
    }

    public string? CompilerPath { get; set; }
    public string? LibPath { get; set; }

    public string? ResolveCompiler() => locator.FindCompiler(CompilerPath);
    public string? ResolveLibPath() => locator.FindLibPath(LibPath);

    public async Task<SkkoaCompileResult> CheckAsync(
        SkkoaCompileOptions options,
        Action<string>? stdout = null,
        Action<string>? stderr = null,
        CancellationToken cancellationToken = default)
    {
        SkkoaCompileOptions checkOptions = new()
        {
            SourcePath = options.SourcePath,
            WorkingDirectory = options.WorkingDirectory,
            LibPath = options.LibPath,
            CheckOnly = true,
            DiagnosticsJson = true
        };
        return await RunCompilerAsync(checkOptions, stdout, stderr, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SkkoaCompileResult> CompileAsync(
        SkkoaCompileOptions options,
        Action<string>? stdout = null,
        Action<string>? stderr = null,
        CancellationToken cancellationToken = default)
    {
        return await RunCompilerAsync(options, stdout, stderr, cancellationToken).ConfigureAwait(false);
    }

    public SkkoaRunningProcess RunExecutable(
        string executablePath,
        string? workingDirectory,
        Action<string> stdout,
        Action<string> stderr,
        Action<int, TimeSpan> exited)
    {
        return runner.StartInteractive(executablePath, [], workingDirectory, BuildEnvironment(), stdout, stderr, exited);
    }

    public async Task<IReadOnlyList<string>> FindMissingRuntimeToolsAsync(CancellationToken cancellationToken = default)
    {
        List<string> missing = [];
        foreach (string tool in new[] { "nasm", "gcc" })
        {
            try
            {
                SkkoaProcessResult result = await runner.RunAsync("where.exe", [tool], environment: BuildEnvironment(), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    missing.Add(tool);
                }
            }
            catch
            {
                missing.Add(tool);
            }
        }
        return missing;
    }

    private async Task<SkkoaCompileResult> RunCompilerAsync(
        SkkoaCompileOptions options,
        Action<string>? stdout,
        Action<string>? stderr,
        CancellationToken cancellationToken)
    {
        string? compiler = ResolveCompiler();
        if (compiler == null)
        {
            return new SkkoaCompileResult
            {
                ExitCode = 9009,
                StandardError = "내장 skkoa.exe를 찾을 수 없습니다.",
                Diagnostics =
                [
                    new SkkoaDiagnostic
                    {
                        Severity = "error",
                        Code = "SKKCOMP",
                        Message = "내장 skkoa.exe를 찾을 수 없습니다. editor/tools/skkoa/skkoa.exe 또는 설정의 compilerPath를 확인하세요.",
                        File = options.SourcePath
                    }
                ]
            };
        }

        List<string> arguments = BuildArguments(options);
        SkkoaProcessResult processResult = await runner.RunAsync(
            compiler,
            arguments,
            options.WorkingDirectory,
            BuildEnvironment(options.LibPath),
            stdout,
            stderr,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<SkkoaDiagnostic> diagnostics = options.DiagnosticsJson
            ? SkkoaDiagnostic.ParseJson(processResult.StandardOutput)
            : [];

        return new SkkoaCompileResult
        {
            ExitCode = processResult.ExitCode,
            StandardOutput = processResult.StandardOutput,
            StandardError = processResult.StandardError,
            OutputPath = options.OutputPath,
            Diagnostics = diagnostics
        };
    }

    private List<string> BuildArguments(SkkoaCompileOptions options)
    {
        List<string> args = [options.SourcePath];
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            args.Add("-o");
            args.Add(options.OutputPath);
        }
        if (options.CheckOnly)
        {
            args.Add("--check");
        }
        if (options.DiagnosticsJson)
        {
            args.Add("--diagnostics-json");
        }
        if (options.EmitAstJson)
        {
            args.Add("--emit-ast-json");
        }
        if (options.NoLink)
        {
            args.Add("--no-link");
        }
        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            args.Add("--working-dir");
            args.Add(options.WorkingDirectory);
        }
        string? lib = options.LibPath ?? ResolveLibPath();
        if (!string.IsNullOrWhiteSpace(lib))
        {
            args.Add("--lib-path");
            args.Add(lib);
        }
        return args;
    }

    private IReadOnlyDictionary<string, string> BuildEnvironment(string? libPath = null)
    {
        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase);
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] toolPaths = locator.FindToolchainPathEntries();
        if (toolPaths.Length > 0)
        {
            environment["PATH"] = string.Join(Path.PathSeparator, toolPaths.Concat([path]));
        }
        string? resolvedLib = libPath ?? ResolveLibPath();
        if (!string.IsNullOrWhiteSpace(resolvedLib))
        {
            environment["SKKOA_LIB_PATH"] = resolvedLib;
        }
        return environment;
    }
}
