using TwitchBotManager.Infrastructure;

namespace TwitchBotManager.Models;

public sealed class BotEnvironmentConfig : ObservableObject
{
    private string _botRootPath = string.Empty;
    private string _telegramBotToken = string.Empty;
    private string _telegramChannelId = string.Empty;
    private string _twitchClientId = string.Empty;
    private string _twitchClientSecret = string.Empty;
    private string _twitchUsername = string.Empty;
    private string _twitchUsernames = string.Empty;
    private string _checkIntervalSeconds = "90";
    private string _requestTimeoutSeconds = "15";
    private string _maxRetries = "3";
    private string _logLevel = "INFO";
    private string _stateFile = "data/state.json";
    private string _logFile = "logs/bot.log";
    private string _telegramAdminIds = string.Empty;

    public string BotRootPath
    {
        get => _botRootPath;
        set => SetProperty(ref _botRootPath, value);
    }

    public string TelegramBotToken
    {
        get => _telegramBotToken;
        set => SetProperty(ref _telegramBotToken, value);
    }

    public string TelegramChannelId
    {
        get => _telegramChannelId;
        set => SetProperty(ref _telegramChannelId, value);
    }

    public string TwitchClientId
    {
        get => _twitchClientId;
        set => SetProperty(ref _twitchClientId, value);
    }

    public string TwitchClientSecret
    {
        get => _twitchClientSecret;
        set => SetProperty(ref _twitchClientSecret, value);
    }

    public string TwitchUsername
    {
        get => _twitchUsername;
        set => SetProperty(ref _twitchUsername, value);
    }

    public string TwitchUsernames
    {
        get => _twitchUsernames;
        set => SetProperty(ref _twitchUsernames, value);
    }

    public string CheckIntervalSeconds
    {
        get => _checkIntervalSeconds;
        set => SetProperty(ref _checkIntervalSeconds, value);
    }

    public string RequestTimeoutSeconds
    {
        get => _requestTimeoutSeconds;
        set => SetProperty(ref _requestTimeoutSeconds, value);
    }

    public string MaxRetries
    {
        get => _maxRetries;
        set => SetProperty(ref _maxRetries, value);
    }

    public string LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    public string StateFile
    {
        get => _stateFile;
        set => SetProperty(ref _stateFile, value);
    }

    public string LogFile
    {
        get => _logFile;
        set => SetProperty(ref _logFile, value);
    }

    public string TelegramAdminIds
    {
        get => _telegramAdminIds;
        set => SetProperty(ref _telegramAdminIds, value);
    }

    public IReadOnlyList<string> GetTrackedStreamers()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var streamers = new List<string>();

        foreach (var source in new[] { TwitchUsername, TwitchUsernames })
        {
            foreach (var value in SplitCsv(source))
            {
                if (seen.Add(value))
                {
                    streamers.Add(value);
                }
            }
        }

        return streamers;
    }

    public static IEnumerable<string> SplitCsv(string raw)
    {
        return raw
            .Replace("\r", string.Empty)
            .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }
}
