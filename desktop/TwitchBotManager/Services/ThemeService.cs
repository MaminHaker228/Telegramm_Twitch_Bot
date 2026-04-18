namespace TwitchBotManager.Services;

public sealed class ThemeService
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";

    public string Normalize(string? themeName)
    {
        return string.Equals(themeName, LightTheme, StringComparison.OrdinalIgnoreCase)
            ? LightTheme
            : DarkTheme;
    }

    public void ApplyTheme(string? themeName)
    {
        var normalized = Normalize(themeName);
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        var existingTheme = merged.FirstOrDefault(dict =>
            dict.Source is not null && dict.Source.OriginalString.Contains("Resources/Themes/", StringComparison.OrdinalIgnoreCase));

        var themeDictionary = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"Resources/Themes/{normalized}Theme.xaml", UriKind.Relative),
        };

        if (existingTheme is null)
        {
            merged.Insert(0, themeDictionary);
            return;
        }

        var index = merged.IndexOf(existingTheme);
        merged.RemoveAt(index);
        merged.Insert(index, themeDictionary);
    }
}
