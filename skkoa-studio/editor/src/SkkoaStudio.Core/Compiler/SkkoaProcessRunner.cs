using System.Diagnostics;
using System.Text;

namespace SkkoaStudio.Core.Compiler;

public sealed class SkkoaProcessRunner
{
    private const int ProcessStartFailureExitCode = 9009;

    public async Task<SkkoaProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        Action<string>? stdout = null,
        Action<string>? stderr = null,
        CancellationToken cancellationToken = default)
    {
        StringBuilder output = new();
        StringBuilder error = new();
        object outputLock = new();
        object errorLock = new();
        DateTimeOffset started = DateTimeOffset.UtcNow;

        using Process process = new();
        ProcessStartInfo startInfo;
        try
        {
            startInfo = BuildStartInfo(fileName, arguments, workingDirectory, environment);
        }
        catch (Exception ex) when (IsProcessStartException(ex))
        {
            return BuildStartFailure(fileName, started, ex);
        }
        process.StartInfo = startInfo;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }
            lock (outputLock)
            {
                output.AppendLine(e.Data);
            }
            stdout?.Invoke(e.Data + Environment.NewLine);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }
            lock (errorLock)
            {
                error.AppendLine(e.Data);
            }
            stderr?.Invoke(e.Data + Environment.NewLine);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (IsProcessStartException(ex))
        {
            return BuildStartFailure(fileName, started, ex);
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new SkkoaProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = ReadBuffer(output, outputLock),
            StandardError = ReadBuffer(error, errorLock),
            Duration = DateTimeOffset.UtcNow - started
        };
    }

    public SkkoaRunningProcess StartInteractive(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        Action<string> stdout,
        Action<string> stderr,
        Action<int, TimeSpan> exited)
    {
        Process process = new();
        process.StartInfo = BuildStartInfo(fileName, arguments, workingDirectory, environment);
        process.EnableRaisingEvents = true;
        DateTimeOffset started = DateTimeOffset.UtcNow;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout(e.Data + Environment.NewLine);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr(e.Data + Environment.NewLine);
            }
        };
        process.Exited += (_, _) =>
        {
            int exitCode;
            try
            {
                exitCode = process.ExitCode;
            }
            catch
            {
                exitCode = -1;
            }
            exited(exitCode, DateTimeOffset.UtcNow - started);
        };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new SkkoaRunningProcess(process);
    }

    private static ProcessStartInfo BuildStartInfo(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        ProcessStartInfo psi = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = ResolveWorkingDirectory(workingDirectory)
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }
        if (environment != null)
        {
            foreach ((string key, string value) in environment)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    psi.Environment[key] = value ?? "";
                }
            }
        }
        return psi;
    }

    private static string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Directory.GetCurrentDirectory();
        }

        string fullPath = Path.GetFullPath(workingDirectory);
        return Directory.Exists(fullPath) ? fullPath : Directory.GetCurrentDirectory();
    }

    private static SkkoaProcessResult BuildStartFailure(string fileName, DateTimeOffset started, Exception ex)
    {
        return new SkkoaProcessResult
        {
            ExitCode = ProcessStartFailureExitCode,
            StandardError = $"프로세스를 시작할 수 없습니다: {fileName}{Environment.NewLine}{ex.Message}",
            Duration = DateTimeOffset.UtcNow - started
        };
    }

    private static string ReadBuffer(StringBuilder builder, object syncRoot)
    {
        lock (syncRoot)
        {
            return builder.ToString();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static bool IsProcessStartException(Exception ex)
    {
        return ex is System.ComponentModel.Win32Exception ||
               ex is FileNotFoundException ||
               ex is DirectoryNotFoundException ||
               ex is UnauthorizedAccessException ||
               ex is InvalidOperationException;
    }
}

public sealed class SkkoaRunningProcess : IDisposable
{
    private readonly Process process;

    internal SkkoaRunningProcess(Process process)
    {
        this.process = process;
    }

    public bool HasExited
    {
        get
        {
            try
            {
                return process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }

    public bool SendInput(string text)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
            process.StandardInput.WriteLine(text);
            process.StandardInput.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Stop()
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            process.Dispose();
        }
        catch
        {
        }
    }
}
