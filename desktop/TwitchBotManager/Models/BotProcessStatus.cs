namespace TwitchBotManager.Models;

public sealed class BotProcessStatus
{
    public bool IsRunning { get; init; }

    public int? ProcessId { get; init; }

    public string ExecutablePath { get; init; } = string.Empty;

    public string StatusText => IsRunning
        ? $"Работает{(ProcessId is int pid ? $" (PID {pid})" : string.Empty)}"
        : "Остановлен";
}
