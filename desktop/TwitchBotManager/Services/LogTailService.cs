namespace TwitchBotManager.Services;

public sealed class LogTailService
{
    public async Task<string> ReadTailAsync(string botRootPath, int maxLines = 200, CancellationToken cancellationToken = default)
    {
        var candidates = new[]
        {
            Path.Combine(botRootPath, "logs", "bot.log"),
            Path.Combine(botRootPath, "runtime", "stderr.log"),
            Path.Combine(botRootPath, "runtime", "stdout.log"),
        };

        var target = candidates.FirstOrDefault(File.Exists);
        if (target is null)
        {
            return "Логи пока не найдены. После запуска бота здесь появится содержимое bot.log.";
        }

        var lines = await File.ReadAllLinesAsync(target, cancellationToken);
        return string.Join(Environment.NewLine, lines.TakeLast(maxLines));
    }
}
