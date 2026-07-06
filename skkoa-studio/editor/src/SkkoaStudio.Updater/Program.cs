using System.Diagnostics;
using SkkoaStudio.Core.Updates;

namespace SkkoaStudio.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.Title = "SKKOA Studio Updater";

        try
        {
            UpdaterOptions options = UpdaterOptions.Parse(args);
            if (options.ShowHelp)
            {
                WriteUsage();
                return 0;
            }

            if (options.ManifestUri == null || string.IsNullOrWhiteSpace(options.InstallDirectory))
            {
                WriteUsage();
                return 2;
            }

            CleanupOldUpdaterCopies();
            WaitForStudioToExit(options.CurrentProcessId);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
            SkkoaUpdateService updateService = new(httpClient);
            Console.WriteLine("Fetching update manifest...");
            SkkoaUpdateManifest manifest = await updateService.FetchManifestAsync(options.ManifestUri);

            SkkoaUpdateApplicator applicator = new(httpClient);
            Progress<SkkoaUpdateProgress> progress = new(WriteProgress);
            SkkoaUpdateApplyResult result = await applicator.ApplyAsync(
                manifest,
                options.ManifestUri,
                options.InstallDirectory,
                progress);

            Console.WriteLine();
            Console.WriteLine($"Update complete. Changed files: {result.ChangedFileCount}, downloaded bytes: {result.DownloadedBytes:N0}");
            RestartStudio(options);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("SKKOA Studio update failed.");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Press any key to close this window.");
            try
            {
                Console.ReadKey(intercept: true);
            }
            catch
            {
            }

            return 1;
        }
    }

    private static void WriteProgress(SkkoaUpdateProgress progress)
    {
        string total = progress.Total <= 0 ? "0" : progress.Total.ToString();
        Console.WriteLine($"[{progress.Stage}] {progress.Completed}/{total} {progress.Message}");
    }

    private static void WaitForStudioToExit(int? processId)
    {
        if (processId is not > 0)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(processId.Value);
            if (process.HasExited)
            {
                return;
            }

            Console.WriteLine("Waiting for SKKOA Studio to close...");
            if (!process.WaitForExit(60000))
            {
                throw new InvalidOperationException("SKKOA Studio did not close within 60 seconds.");
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static void RestartStudio(UpdaterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RestartPath))
        {
            return;
        }

        string restartPath = Path.IsPathRooted(options.RestartPath)
            ? options.RestartPath
            : Path.Combine(options.InstallDirectory, options.RestartPath);
        if (!File.Exists(restartPath))
        {
            Console.WriteLine("Updated application was not found for restart: " + restartPath);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = restartPath,
            WorkingDirectory = options.InstallDirectory,
            UseShellExecute = true
        });
    }

    private static void CleanupOldUpdaterCopies()
    {
        string root = Path.Combine(Path.GetTempPath(), "SKKOA Studio", "updater");
        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (string directory in Directory.GetDirectories(root))
            {
                try
                {
                    DirectoryInfo info = new(directory);
                    if (info.CreationTimeUtc < DateTime.UtcNow.AddDays(-2))
                    {
                        info.Delete(recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static void WriteUsage()
    {
        Console.WriteLine("SKKOA Studio Updater");
        Console.WriteLine("Usage:");
        Console.WriteLine("  SkkoaStudio.Updater.exe --manifest-url <url> --install-dir <path> [--current-pid <pid>] [--restart <exe>]");
    }
}

internal sealed class UpdaterOptions
{
    public Uri? ManifestUri { get; private init; }
    public string InstallDirectory { get; private init; } = "";
    public int? CurrentProcessId { get; private init; }
    public string RestartPath { get; private init; } = "";
    public bool ShowHelp { get; private init; }

    public static UpdaterOptions Parse(string[] args)
    {
        Uri? manifestUri = null;
        string installDirectory = "";
        int? currentProcessId = null;
        string restartPath = "";
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "--manifest-url":
                    manifestUri = new Uri(RequireValue(args, ref i, arg), UriKind.Absolute);
                    break;
                case "--install-dir":
                    installDirectory = Path.GetFullPath(RequireValue(args, ref i, arg));
                    break;
                case "--current-pid":
                    if (int.TryParse(RequireValue(args, ref i, arg), out int pid))
                    {
                        currentProcessId = pid;
                    }
                    break;
                case "--restart":
                    restartPath = RequireValue(args, ref i, arg);
                    break;
                default:
                    throw new InvalidOperationException("Unknown updater argument: " + arg);
            }
        }

        return new UpdaterOptions
        {
            ManifestUri = manifestUri,
            InstallDirectory = installDirectory,
            CurrentProcessId = currentProcessId,
            RestartPath = restartPath,
            ShowHelp = showHelp
        };
    }

    private static string RequireValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException(argumentName + " requires a value.");
        }

        index++;
        return args[index];
    }
}
