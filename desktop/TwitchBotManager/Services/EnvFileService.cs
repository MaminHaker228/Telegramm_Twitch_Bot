using System.Text;
using TwitchBotManager.Models;

namespace TwitchBotManager.Services;

public sealed class EnvFileService
{
    public async Task<BotEnvironmentConfig> LoadAsync(string botRootPath, CancellationToken cancellationToken = default)
    {
        var config = new BotEnvironmentConfig
        {
            BotRootPath = botRootPath,
        };

        var envPath = GetEnvFilePath(botRootPath);
        var sourcePath = File.Exists(envPath)
            ? envPath
            : Path.Combine(botRootPath, ".env.example");

        if (!File.Exists(sourcePath))
        {
            return config;
        }

        var lines = await File.ReadAllLinesAsync(sourcePath, cancellationToken);
        var values = Parse(lines);

        config.TelegramBotToken = Get(values, "TELEGRAM_BOT_TOKEN");
        config.TelegramChannelId = Get(values, "TELEGRAM_CHANNEL_ID");
        config.TwitchClientId = Get(values, "TWITCH_CLIENT_ID");
        config.TwitchClientSecret = Get(values, "TWITCH_CLIENT_SECRET");
        config.TwitchUsername = Get(values, "TWITCH_USERNAME");
        config.TwitchUsernames = Get(values, "TWITCH_USERNAMES");
        config.CheckIntervalSeconds = Get(values, "CHECK_INTERVAL_SECONDS", "90");
        config.RequestTimeoutSeconds = Get(values, "REQUEST_TIMEOUT_SECONDS", "15");
        config.MaxRetries = Get(values, "MAX_RETRIES", "3");
        config.LogLevel = Get(values, "LOG_LEVEL", "INFO");
        config.StateFile = Get(values, "STATE_FILE", "data/state.json");
        config.LogFile = Get(values, "LOG_FILE", "logs/bot.log");
        config.TelegramAdminIds = Get(values, "TELEGRAM_ADMIN_IDS");

        return config;
    }

    public async Task SaveAsync(BotEnvironmentConfig config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.BotRootPath))
        {
            throw new InvalidOperationException("Укажи папку с Telegram-ботом перед сохранением.");
        }

        Directory.CreateDirectory(config.BotRootPath);

        Validate(config);

        var lines = new[]
        {
            $"TELEGRAM_BOT_TOKEN={config.TelegramBotToken.Trim()}",
            $"TELEGRAM_CHANNEL_ID={config.TelegramChannelId.Trim()}",
            $"TWITCH_CLIENT_ID={config.TwitchClientId.Trim()}",
            $"TWITCH_CLIENT_SECRET={config.TwitchClientSecret.Trim()}",
            $"TWITCH_USERNAME={config.TwitchUsername.Trim()}",
            $"TWITCH_USERNAMES={config.TwitchUsernames.Trim()}",
            $"CHECK_INTERVAL_SECONDS={NormalizeInt(config.CheckIntervalSeconds, 90)}",
            $"REQUEST_TIMEOUT_SECONDS={NormalizeDouble(config.RequestTimeoutSeconds, 15)}",
            $"MAX_RETRIES={NormalizeInt(config.MaxRetries, 3)}",
            $"LOG_LEVEL={(string.IsNullOrWhiteSpace(config.LogLevel) ? "INFO" : config.LogLevel.Trim().ToUpperInvariant())}",
            $"STATE_FILE={(string.IsNullOrWhiteSpace(config.StateFile) ? "data/state.json" : config.StateFile.Trim())}",
            $"LOG_FILE={(string.IsNullOrWhiteSpace(config.LogFile) ? "logs/bot.log" : config.LogFile.Trim())}",
            $"TELEGRAM_ADMIN_IDS={config.TelegramAdminIds.Trim()}",
        };

        var output = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(GetEnvFilePath(config.BotRootPath), output, utf8WithoutBom, cancellationToken);
    }

    public string GetEnvFilePath(string botRootPath) => Path.Combine(botRootPath, ".env");

    private static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string defaultValue = "")
        => values.TryGetValue(key, out var value) ? value : defaultValue;

    private static void Validate(BotEnvironmentConfig config)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(config.TelegramBotToken)) missing.Add("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(config.TelegramChannelId)) missing.Add("TELEGRAM_CHANNEL_ID");
        if (string.IsNullOrWhiteSpace(config.TwitchClientId)) missing.Add("TWITCH_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(config.TwitchClientSecret)) missing.Add("TWITCH_CLIENT_SECRET");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Заполни обязательные поля: {string.Join(", ", missing)}.");
        }

        var interval = NormalizeInt(config.CheckIntervalSeconds, 90);
        if (interval < 60 || interval > 120)
        {
            throw new InvalidOperationException("CHECK_INTERVAL_SECONDS должен быть в диапазоне от 60 до 120.");
        }

        if (NormalizeDouble(config.RequestTimeoutSeconds, 15) < 5)
        {
            throw new InvalidOperationException("REQUEST_TIMEOUT_SECONDS должен быть не меньше 5.");
        }

        if (NormalizeInt(config.MaxRetries, 3) < 1)
        {
            throw new InvalidOperationException("MAX_RETRIES должен быть не меньше 1.");
        }
    }

    private static int NormalizeInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double NormalizeDouble(string value, double fallback)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
