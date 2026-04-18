using System.Diagnostics;
using System.Management;
using TwitchBotManager.Models;

namespace TwitchBotManager.Services;

public sealed class BotProcessService
{
    private const string ManagedPidFileName = "desktop-manager.pid";

    public async Task<BotProcessStatus> StartAsync(string botRootPath, CancellationToken cancellationToken = default)
    {
        EnsureBotRoot(botRootPath);

        var current = GetStatus(botRootPath);
        if (current.IsRunning)
        {
            return current;
        }

        Directory.CreateDirectory(GetRuntimeDirectory(botRootPath));

        var (fileName, arguments, executablePath) = ResolvePythonLaunch(botRootPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = botRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить процесс Python.");

        await File.WriteAllTextAsync(GetManagedPidPath(botRootPath), process.Id.ToString(), cancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        if (process.HasExited)
        {
            TryDeletePidFile(botRootPath);
            throw new InvalidOperationException(
                $"Бот завершился сразу после запуска. Проверь файл {Path.Combine(botRootPath, "logs", "bot.log")} или настройки .env."
            );
        }

        return new BotProcessStatus
        {
            IsRunning = true,
            ProcessId = process.Id,
            ExecutablePath = executablePath,
        };
    }

    public async Task StopAsync(string botRootPath)
    {
        var status = GetStatus(botRootPath);
        if (!status.IsRunning || status.ProcessId is null)
        {
            TryDeletePidFile(botRootPath);
            return;
        }

        try
        {
            using var process = Process.GetProcessById(status.ProcessId.Value);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch (ArgumentException)
        {
        }
        finally
        {
            TryDeletePidFile(botRootPath);
        }
    }

    public BotProcessStatus GetStatus(string botRootPath)
    {
        var pidPath = GetManagedPidPath(botRootPath);
        if (!File.Exists(pidPath))
        {
            var discovered = FindRunningBotProcess(botRootPath);
            if (discovered is not null)
            {
                TryAdoptPid(botRootPath, discovered.ProcessId!.Value);
                return discovered;
            }

            return new BotProcessStatus();
        }

        if (!int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid))
        {
            TryDeletePidFile(botRootPath);
            return FindRunningBotProcess(botRootPath) ?? new BotProcessStatus();
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
            {
                TryDeletePidFile(botRootPath);
                return FindRunningBotProcess(botRootPath) ?? new BotProcessStatus();
            }

            return new BotProcessStatus
            {
                IsRunning = true,
                ProcessId = pid,
                ExecutablePath = process.MainModule?.FileName ?? string.Empty,
            };
        }
        catch (ArgumentException)
        {
            TryDeletePidFile(botRootPath);
            return FindRunningBotProcess(botRootPath) ?? new BotProcessStatus();
        }
        catch (InvalidOperationException)
        {
            return new BotProcessStatus
            {
                IsRunning = true,
                ProcessId = pid,
            };
        }
    }

    public string GetManagedPidPath(string botRootPath)
        => Path.Combine(GetRuntimeDirectory(botRootPath), ManagedPidFileName);

    private static string GetRuntimeDirectory(string botRootPath) => Path.Combine(botRootPath, "runtime");

    private static void EnsureBotRoot(string botRootPath)
    {
        if (string.IsNullOrWhiteSpace(botRootPath) || !Directory.Exists(botRootPath))
        {
            throw new InvalidOperationException("Укажи существующую папку проекта с ботом.");
        }

        var mainPy = Path.Combine(botRootPath, "main.py");
        if (!File.Exists(mainPy))
        {
            throw new InvalidOperationException("В выбранной папке не найден файл main.py.");
        }
    }

    private static (string FileName, string Arguments, string ExecutablePath) ResolvePythonLaunch(string botRootPath)
    {
        var localPython = Path.Combine(botRootPath, ".venv", "Scripts", "python.exe");
        if (File.Exists(localPython))
        {
            return (localPython, "main.py", localPython);
        }

        return ("py", "-3 main.py", "py -3");
    }

    private static void TryDeletePidFile(string botRootPath)
    {
        var pidPath = Path.Combine(GetRuntimeDirectory(botRootPath), ManagedPidFileName);
        if (File.Exists(pidPath))
        {
            File.Delete(pidPath);
        }
    }

    private static void TryAdoptPid(string botRootPath, int processId)
    {
        Directory.CreateDirectory(GetRuntimeDirectory(botRootPath));
        File.WriteAllText(Path.Combine(GetRuntimeDirectory(botRootPath), ManagedPidFileName), processId.ToString());
    }

    private static BotProcessStatus? FindRunningBotProcess(string botRootPath)
    {
        var normalizedRoot = Path.GetFullPath(botRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, CommandLine, ExecutablePath, Name FROM Win32_Process WHERE Name = 'python.exe' OR Name = 'py.exe'");

        foreach (ManagementObject process in searcher.Get())
        {
            var commandLine = process["CommandLine"]?.ToString() ?? string.Empty;
            var executablePath = process["ExecutablePath"]?.ToString() ?? string.Empty;

            if (!commandLine.Contains("main.py", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var belongsToBot =
                executablePath.Contains(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                commandLine.Contains(normalizedRoot, StringComparison.OrdinalIgnoreCase);

            if (!belongsToBot)
            {
                continue;
            }

            var processId = Convert.ToInt32(process["ProcessId"]);
            return new BotProcessStatus
            {
                IsRunning = true,
                ProcessId = processId,
                ExecutablePath = string.IsNullOrWhiteSpace(executablePath) ? commandLine : executablePath,
            };
        }

        return null;
    }
}
