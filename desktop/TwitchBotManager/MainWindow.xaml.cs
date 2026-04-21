using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TwitchBotManager.ViewModels;

namespace TwitchBotManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _stickLogToBottom = true;
    private bool _isUpdatingLogText;
    private bool _isSyncingLogScrollBar;
    private ScrollViewer? _logScrollViewer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        CacheLogScrollViewer();
        LogTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnLogScrollChanged));
        await _viewModel.InitializeAsync();
        UpdateLogText();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        LogTextBox.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnLogScrollChanged));
        await _viewModel.DisposeAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.LogContent))
        {
            return;
        }

        Dispatcher.BeginInvoke(UpdateLogText, DispatcherPriority.Background);
    }

    private void UpdateLogText()
    {
        CacheLogScrollViewer();

        if (LogTextBox.Text == _viewModel.LogContent)
        {
            SyncLogScrollBar();
            return;
        }

        var verticalOffset = _logScrollViewer?.VerticalOffset ?? 0;
        var horizontalOffset = _logScrollViewer?.HorizontalOffset ?? 0;
        var shouldStick = _stickLogToBottom;

        _isUpdatingLogText = true;
        try
        {
            LogTextBox.Text = _viewModel.LogContent;
            LogTextBox.UpdateLayout();

            if (_logScrollViewer is null)
            {
                if (shouldStick)
                {
                    LogTextBox.ScrollToEnd();
                }
                SyncLogScrollBar();
                return;
            }

            if (shouldStick)
            {
                _logScrollViewer.ScrollToEnd();
                LogTextBox.ScrollToEnd();
                SyncLogScrollBar();
                return;
            }

            _logScrollViewer.ScrollToVerticalOffset(Math.Min(verticalOffset, _logScrollViewer.ScrollableHeight));
            _logScrollViewer.ScrollToHorizontalOffset(Math.Min(horizontalOffset, _logScrollViewer.ScrollableWidth));
            SyncLogScrollBar();
        }
        finally
        {
            _isUpdatingLogText = false;
        }
    }

    private void OnLogScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isUpdatingLogText)
        {
            return;
        }

        var scrollableHeight = Math.Max(0, e.ExtentHeight - e.ViewportHeight);
        var verticalOffset = e.VerticalOffset;
        _stickLogToBottom = scrollableHeight <= 1 || verticalOffset >= scrollableHeight - 8;
        SyncLogScrollBar();
    }

    private void OnScrollLogToEndClick(object sender, RoutedEventArgs e)
    {
        _stickLogToBottom = true;
        CacheLogScrollViewer();
        if (_logScrollViewer is not null)
        {
            _logScrollViewer.ScrollToEnd();
        }
        LogTextBox.ScrollToEnd();
        SyncLogScrollBar();
    }

    private void OnLogScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingLogScrollBar)
        {
            return;
        }

        CacheLogScrollViewer();
        if (_logScrollViewer is null)
        {
            return;
        }

        _logScrollViewer.ScrollToVerticalOffset(e.NewValue);
        _stickLogToBottom = e.NewValue >= _logScrollViewer.ScrollableHeight - 8;
    }

    private void SyncLogScrollBar()
    {
        CacheLogScrollViewer();
        if (_logScrollViewer is null)
        {
            return;
        }

        _isSyncingLogScrollBar = true;
        try
        {
            LogScrollBar.Maximum = Math.Max(0, _logScrollViewer.ScrollableHeight);
            LogScrollBar.ViewportSize = Math.Max(1, _logScrollViewer.ViewportHeight);
            LogScrollBar.LargeChange = Math.Max(8, _logScrollViewer.ViewportHeight * 0.8);
            LogScrollBar.SmallChange = 24;
            LogScrollBar.IsEnabled = _logScrollViewer.ScrollableHeight > 0;
            LogScrollBar.Value = Math.Min(_logScrollViewer.VerticalOffset, LogScrollBar.Maximum);
        }
        finally
        {
            _isSyncingLogScrollBar = false;
        }
    }

    private void CacheLogScrollViewer()
    {
        _logScrollViewer ??= FindDescendant<ScrollViewer>(LogTextBox);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
