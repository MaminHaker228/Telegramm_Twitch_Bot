using System.Diagnostics;
using System.Windows.Forms;

namespace TwitchBotManager.Services;

public sealed class FolderPickerService
{
    public string? PickFolder(string? initialPath)
    {
        using var dialog = new FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = "Выбери папку проекта Telegram/Twitch бота",
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : Environment.CurrentDirectory,
            ShowNewFolderButton = false,
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    public void OpenInExplorer(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }
}
