using System.Text;

namespace TwitchBotManager.Services;

public sealed class LogTailService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private string _botRootPath = string.Empty;
    private string _configuredLogFile = "logs/bot.log";

    public event EventHandler? LogFilesChanged;

    public string WatchedFilesSummary => string.IsNullOrWhiteSpace(_botRootPath)
        ? "Живой лог ждёт путь к проекту."
        : $"Источники: {string.Join(", ", GetCandidates(_botRootPath, _configuredLogFile).Select(Path.GetFileName))}";

    public void StartWatching(string botRootPath, string configuredLogFile)
    {
        StopWatching();

        if (string.IsNullOrWhiteSpace(botRootPath) || !Directory.Exists(botRootPath))
        {
            _botRootPath = string.Empty;
            _configuredLogFile = "logs/bot.log";
            return;
        }

        _botRootPath = botRootPath;
        _configuredLogFile = string.IsNullOrWhiteSpace(configuredLogFile) ? "logs/bot.log" : configuredLogFile.Trim();
        var directories = GetCandidates(botRootPath, _configuredLogFile)
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };

            watcher.Changed += HandleLogChanged;
            watcher.Created += HandleLogChanged;
            watcher.Deleted += HandleLogChanged;
            watcher.Renamed += HandleLogChanged;
            _watchers.Add(watcher);
        }
    }

    public void StopWatching()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= HandleLogChanged;
            watcher.Created -= HandleLogChanged;
            watcher.Deleted -= HandleLogChanged;
            watcher.Renamed -= HandleLogChanged;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    public async Task<string> ReadTailAsync(
        string botRootPath,
        string configuredLogFile,
        int maxLinesPerFile = 120,
        CancellationToken cancellationToken = default)
    {
        var candidates = GetCandidates(botRootPath, configuredLogFile)
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderBy(file => GetDisplayOrder(file.Name))
            .ToList();

        if (candidates.Count == 0)
        {
            return "Логи пока не найдены. После запуска бота здесь появится живой tail по основному log file, stderr.log и stdout.log.";
        }

        var builder = new StringBuilder();

        foreach (var file in candidates)
        {
            var lines = await ReadLinesSafeAsync(file.FullName, cancellationToken);
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"----- {file.Name} -----");
            foreach (var line in lines.TakeLast(maxLinesPerFile))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public void Dispose()
    {
        StopWatching();
    }

    private static IEnumerable<string> GetCandidates(string botRootPath, string configuredLogFile)
    {
        var primaryLogFile = ResolveLogPath(botRootPath, configuredLogFile);
        return
        [
            primaryLogFile,
            Path.Combine(botRootPath, "runtime", "stderr.log"),
            Path.Combine(botRootPath, "runtime", "stdout.log"),
        ];
    }

    private static string ResolveLogPath(string botRootPath, string configuredLogFile)
    {
        if (string.IsNullOrWhiteSpace(configuredLogFile))
        {
            return Path.Combine(botRootPath, "logs", "bot.log");
        }

        return Path.IsPathRooted(configuredLogFile)
            ? configuredLogFile
            : Path.Combine(botRootPath, configuredLogFile);
    }

    private static async Task<IReadOnlyList<string>> ReadLinesSafeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => line is not null)
            .ToArray();
    }

    private static int GetDisplayOrder(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "bot.log" => 0,
            "stderr.log" => 1,
            "stdout.log" => 2,
            _ => 99,
        };
    }

    private void HandleLogChanged(object sender, FileSystemEventArgs e)
    {
        LogFilesChanged?.Invoke(this, EventArgs.Empty);
    }
}
