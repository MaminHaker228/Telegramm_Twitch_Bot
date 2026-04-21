using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using TwitchBotManager.Infrastructure;
using TwitchBotManager.Models;
using TwitchBotManager.Services;

namespace TwitchBotManager.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    private static readonly Brush StoppedBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22));
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));

    private readonly EnvFileService _envFileService;
    private readonly BotProcessService _botProcessService;
    private readonly LogTailService _logTailService;
    private readonly FolderPickerService _folderPickerService;
    private readonly DesktopSettingsService _desktopSettingsService;
    private readonly ThemeService _themeService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private BotEnvironmentConfig _config;
    private string _destinationSummary = "Канал не указан";
    private string _streamersSummary = "Стримеры не указаны";
    private string _streamersCountSummary = "0 стримеров";
    private string _botStatus = "Остановлен";
    private string _botExecutable = "Python еще не запускался";
    private string _statusMessage = "Заполни настройки и нажми «Запустить бота».";
    private string _logContent = "Логи пока не загружены.";
    private string _lastRefreshText = "Еще не обновлялось";
    private string _liveLogStatus = "Живой лог ожидает путь к проекту";
    private string _liveLogMode = "AUTO";
    private string _logSourceSummary = "Источники: logs/bot.log, runtime/stderr.log, runtime/stdout.log";
    private string _currentThemeName = ThemeService.DarkTheme;
    private Brush _statusBrush = StoppedBrush;
    private bool _isInitialized;

    public MainViewModel()
        : this(
            new EnvFileService(),
            new BotProcessService(),
            new LogTailService(),
            new FolderPickerService(),
            new DesktopSettingsService(),
            new ThemeService())
    {
    }

    public MainViewModel(
        EnvFileService envFileService,
        BotProcessService botProcessService,
        LogTailService logTailService,
        FolderPickerService folderPickerService,
        DesktopSettingsService desktopSettingsService,
        ThemeService themeService)
    {
        _envFileService = envFileService;
        _botProcessService = botProcessService;
        _logTailService = logTailService;
        _folderPickerService = folderPickerService;
        _desktopSettingsService = desktopSettingsService;
        _themeService = themeService;
        _logTailService.LogFilesChanged += HandleLogFilesChanged;

        _config = new BotEnvironmentConfig
        {
            BotRootPath = DetectBotRootPath(),
        };
        _config.PropertyChanged += HandleConfigPropertyChanged;
        UpdateSummaries();

        BrowseBotFolderCommand = new RelayCommand(BrowseBotFolder);
        OpenProjectFolderCommand = new RelayCommand(OpenProjectFolder, () => Directory.Exists(Config.BotRootPath));
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder, () => Directory.Exists(Config.BotRootPath));
        LoadConfigCommand = new AsyncRelayCommand(LoadConfigAsync, () => Directory.Exists(Config.BotRootPath));
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync, () => Directory.Exists(Config.BotRootPath));
        StartBotCommand = new AsyncRelayCommand(StartBotAsync, () => Directory.Exists(Config.BotRootPath));
        StopBotCommand = new AsyncRelayCommand(StopBotAsync, () => Directory.Exists(Config.BotRootPath));
        RestartBotCommand = new AsyncRelayCommand(RestartBotAsync, () => Directory.Exists(Config.BotRootPath));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => Directory.Exists(Config.BotRootPath));
        UseDarkThemeCommand = new AsyncRelayCommand(() => ApplyThemeAsync(ThemeService.DarkTheme));
        UseLightThemeCommand = new AsyncRelayCommand(() => ApplyThemeAsync(ThemeService.LightTheme));

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _refreshTimer.Tick += RefreshTimerOnTick;
    }

    public BotEnvironmentConfig Config
    {
        get => _config;
        private set
        {
            if (ReferenceEquals(_config, value))
            {
                return;
            }

            _config.PropertyChanged -= HandleConfigPropertyChanged;
            _config = value;
            _config.PropertyChanged += HandleConfigPropertyChanged;
            OnPropertyChanged();
            UpdateSummaries();
            RaiseCommandStates();
        }
    }

    public string DestinationSummary
    {
        get => _destinationSummary;
        private set => SetProperty(ref _destinationSummary, value);
    }

    public string StreamersSummary
    {
        get => _streamersSummary;
        private set => SetProperty(ref _streamersSummary, value);
    }

    public string StreamersCountSummary
    {
        get => _streamersCountSummary;
        private set => SetProperty(ref _streamersCountSummary, value);
    }

    public string BotStatus
    {
        get => _botStatus;
        private set => SetProperty(ref _botStatus, value);
    }

    public string BotExecutable
    {
        get => _botExecutable;
        private set => SetProperty(ref _botExecutable, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LogContent
    {
        get => _logContent;
        private set => SetProperty(ref _logContent, value);
    }

    public string LastRefreshText
    {
        get => _lastRefreshText;
        private set => SetProperty(ref _lastRefreshText, value);
    }

    public string LiveLogStatus
    {
        get => _liveLogStatus;
        private set => SetProperty(ref _liveLogStatus, value);
    }

    public string LiveLogMode
    {
        get => _liveLogMode;
        private set => SetProperty(ref _liveLogMode, value);
    }

    public string LogSourceSummary
    {
        get => _logSourceSummary;
        private set => SetProperty(ref _logSourceSummary, value);
    }

    public string CurrentThemeName
    {
        get => _currentThemeName;
        private set
        {
            if (SetProperty(ref _currentThemeName, value))
            {
                OnPropertyChanged(nameof(ThemeSummary));
                OnPropertyChanged(nameof(ThemeDescription));
                OnPropertyChanged(nameof(DarkThemeButtonText));
                OnPropertyChanged(nameof(LightThemeButtonText));
            }
        }
    }

    public string ThemeSummary => CurrentThemeName == ThemeService.LightTheme ? "Светлая" : "Тёмная";

    public string ThemeDescription => CurrentThemeName == ThemeService.LightTheme
        ? "Лёгкая дневная тема с чистыми карточками"
        : "Контрастная ночная тема для долгой работы";

    public string DarkThemeButtonText => CurrentThemeName == ThemeService.DarkTheme ? "Тёмная ✓" : "Тёмная";

    public string LightThemeButtonText => CurrentThemeName == ThemeService.LightTheme ? "Светлая ✓" : "Светлая";

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public ICommand BrowseBotFolderCommand { get; }

    public ICommand OpenProjectFolderCommand { get; }

    public ICommand OpenLogsFolderCommand { get; }

    public ICommand LoadConfigCommand { get; }

    public ICommand SaveConfigCommand { get; }

    public ICommand StartBotCommand { get; }

    public ICommand StopBotCommand { get; }

    public ICommand RestartBotCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand UseDarkThemeCommand { get; }

    public ICommand UseLightThemeCommand { get; }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        var settings = await _desktopSettingsService.LoadAsync();
        await ApplyThemeAsync(settings.Theme, announce: false, persist: false);

        var candidateRoot = Directory.Exists(settings.BotRootPath)
            ? settings.BotRootPath
            : Config.BotRootPath;

        Config.BotRootPath = candidateRoot;
        await LoadConfigAsync();
        _refreshTimer.Start();
    }

    public async ValueTask DisposeAsync()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimerOnTick;
        _config.PropertyChanged -= HandleConfigPropertyChanged;
        _logTailService.LogFilesChanged -= HandleLogFilesChanged;
        _logTailService.StopWatching();
        _logTailService.Dispose();
        _refreshLock.Dispose();
        await Task.CompletedTask;
    }

    private async void RefreshTimerOnTick(object? sender, EventArgs e)
    {
        await RefreshAsync(silent: true);
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            EnsureBotRootExists();
            var loaded = await _envFileService.LoadAsync(Config.BotRootPath);
            loaded.BotRootPath = Config.BotRootPath;
            Config = loaded;
            RestartLogMonitoring();
            await SaveDesktopSettingsAsync();
            await RefreshAsync(silent: true);
            SetInfo("Конфиг загружен. Всё готово: проверь канал, стримеров и запускай бота.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            EnsureBotRootExists();
            await _envFileService.SaveAsync(Config);
            await SaveDesktopSettingsAsync();
            RestartLogMonitoring();
            UpdateSummaries();
            SetInfo($"Настройки сохранены в {_envFileService.GetEnvFilePath(Config.BotRootPath)}.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task StartBotAsync()
    {
        try
        {
            await _envFileService.SaveAsync(Config);
            await SaveDesktopSettingsAsync();
            RestartLogMonitoring();
            var status = await _botProcessService.StartAsync(Config.BotRootPath);
            ApplyProcessStatus(status);
            await RefreshAsync(silent: true);
            SetInfo("Бот запущен. Уведомления уйдут в выбранный Telegram-канал.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            await RefreshAsync(silent: true);
        }
    }

    private async Task StopBotAsync()
    {
        try
        {
            EnsureBotRootExists();
            await _botProcessService.StopAsync(Config.BotRootPath);
            await RefreshAsync(silent: true);
            SetInfo("Бот остановлен.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task RestartBotAsync()
    {
        try
        {
            EnsureBotRootExists();
            await _envFileService.SaveAsync(Config);
            await SaveDesktopSettingsAsync();
            RestartLogMonitoring();
            await _botProcessService.StopAsync(Config.BotRootPath);
            var status = await _botProcessService.StartAsync(Config.BotRootPath);
            ApplyProcessStatus(status);
            await RefreshAsync(silent: true);
            SetInfo("Бот перезапущен с новыми настройками.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            await RefreshAsync(silent: true);
        }
    }

    private Task RefreshAsync() => RefreshAsync(silent: false);

    private async Task RefreshAsync(bool silent)
    {
        if (!Directory.Exists(Config.BotRootPath))
        {
            if (!silent)
            {
                SetError("Папка проекта не найдена. Выбери корректный путь к боту.");
            }
            return;
        }

        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var status = _botProcessService.GetStatus(Config.BotRootPath);
            ApplyProcessStatus(status);
            LogContent = await _logTailService.ReadTailAsync(Config.BotRootPath, Config.LogFile);
            LogSourceSummary = _logTailService.WatchedFilesSummary;
            LastRefreshText = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            if (!silent)
            {
                SetInfo("Состояние обновлено.");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void BrowseBotFolder()
    {
        var selectedPath = _folderPickerService.PickFolder(Config.BotRootPath);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        Config.BotRootPath = selectedPath;
        _ = LoadConfigAsync();
    }

    private void OpenProjectFolder()
    {
        _folderPickerService.OpenInExplorer(Config.BotRootPath);
    }

    private void OpenLogsFolder()
    {
        var path = Path.Combine(Config.BotRootPath, "logs");
        Directory.CreateDirectory(path);
        _folderPickerService.OpenInExplorer(path);
    }

    private async Task ApplyThemeAsync(string? themeName, bool announce = true, bool persist = true)
    {
        var normalized = _themeService.Normalize(themeName);
        CurrentThemeName = normalized;
        _themeService.ApplyTheme(normalized);

        if (persist)
        {
            await SaveDesktopSettingsAsync();
        }

        if (announce)
        {
            SetInfo($"Тема переключена: {ThemeSummary.ToLowerInvariant()}.");
        }
    }

    private void ApplyProcessStatus(BotProcessStatus status)
    {
        BotStatus = status.StatusText;
        BotExecutable = string.IsNullOrWhiteSpace(status.ExecutablePath)
            ? "Python будет показан после запуска"
            : status.ExecutablePath;
        StatusBrush = status.IsRunning ? RunningBrush : StoppedBrush;
    }

    private void UpdateSummaries()
    {
        DestinationSummary = string.IsNullOrWhiteSpace(Config.TelegramChannelId)
            ? "Канал не указан"
            : Config.TelegramChannelId.Trim();

        var streamers = Config.GetTrackedStreamers();
        StreamersSummary = streamers.Count == 0
            ? "Стримеры не указаны"
            : string.Join(", ", streamers);
        StreamersCountSummary = streamers.Count switch
        {
            0 => "0 стримеров",
            1 => "1 стример",
            _ => $"{streamers.Count} стримера(ов)",
        };
    }

    private void HandleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateSummaries();
        if (e.PropertyName is nameof(BotEnvironmentConfig.BotRootPath) or nameof(BotEnvironmentConfig.LogFile))
        {
            RestartLogMonitoring();
            RaiseCommandStates();
        }
    }

    private void RestartLogMonitoring()
    {
        if (Directory.Exists(Config.BotRootPath))
        {
            _logTailService.StartWatching(Config.BotRootPath, Config.LogFile);
            LiveLogMode = "LIVE";
            LiveLogStatus = "Лог обновляется автоматически при изменении основного log file, stderr.log и stdout.log.";
            LogSourceSummary = _logTailService.WatchedFilesSummary;
            return;
        }

        _logTailService.StopWatching();
        LiveLogMode = "AUTO";
        LiveLogStatus = "Живой лог ожидает корректный путь к проекту.";
        LogSourceSummary = "Источники: logs/bot.log, runtime/stderr.log, runtime/stdout.log";
    }

    private void HandleLogFilesChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(async () =>
        {
            await RefreshAsync(silent: true);
            LastRefreshText = $"Живой лог: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        });
    }

    private async Task SaveDesktopSettingsAsync()
    {
        await _desktopSettingsService.SaveAsync(new DesktopSettings
        {
            BotRootPath = Config.BotRootPath,
            Theme = CurrentThemeName,
        });
    }

    private void EnsureBotRootExists()
    {
        if (string.IsNullOrWhiteSpace(Config.BotRootPath) || !Directory.Exists(Config.BotRootPath))
        {
            throw new InvalidOperationException("Выбери существующую папку проекта с main.py.");
        }
    }

    private void SetInfo(string message)
    {
        StatusMessage = message;
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        StatusBrush = ErrorBrush;
    }

    private void RaiseCommandStates()
    {
        foreach (var command in new ICommand[]
                 {
                     OpenProjectFolderCommand,
                     OpenLogsFolderCommand,
                     LoadConfigCommand,
                     SaveConfigCommand,
                     StartBotCommand,
                     StopBotCommand,
                     RestartBotCommand,
                     RefreshCommand,
                 })
        {
            switch (command)
            {
                case RelayCommand relayCommand:
                    relayCommand.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand asyncRelayCommand:
                    asyncRelayCommand.RaiseCanExecuteChanged();
                    break;
            }
        }
    }

    private static string DetectBotRootPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "main.py")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Environment.CurrentDirectory;
    }
}
