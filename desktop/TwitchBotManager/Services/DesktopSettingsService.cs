using System.Text.Json;
using TwitchBotManager.Models;

namespace TwitchBotManager.Services;

public sealed class DesktopSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(GetSettingsPath()))
        {
            return new DesktopSettings();
        }

        await using var stream = File.OpenRead(GetSettingsPath());
        var settings = await JsonSerializer.DeserializeAsync<DesktopSettings>(stream, JsonOptions, cancellationToken);
        return settings ?? new DesktopSettings();
    }

    public async Task SaveAsync(DesktopSettings settings, CancellationToken cancellationToken = default)
    {
        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TwitchBotManager", "settings.json");
    }
}
