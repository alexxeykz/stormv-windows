using StormV.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace StormV.Views;

public partial class LogWindow : Window
{
    private readonly ObservableCollection<LogEntry> _displayed = new();
    private LogLevel _minLevel = LogLevel.Info;
    private string _search = string.Empty;

    public LogWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _displayed;
        LogFilePathRun.Text = Logger.Instance.LogFilePath;

        // Загружаем существующие записи
        Reload();

        // Подписываемся на новые
        Logger.Instance.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(LogEntry entry)
    {
        if (!Matches(entry)) return;
        Dispatcher.InvokeAsync(() =>
        {
            _displayed.Add(entry);
            UpdateStatus();
            if (AutoScrollToggle.IsChecked == true)
                LogList.ScrollIntoView(_displayed[^1]);
        });
    }

    private bool Matches(LogEntry e)
    {
        if (e.Level < _minLevel) return false;
        if (!string.IsNullOrEmpty(_search) &&
            !e.Message.Contains(_search, StringComparison.OrdinalIgnoreCase) &&
            !e.Tag.Contains(_search, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void Reload()
    {
        _displayed.Clear();
        foreach (var e in Logger.Instance.GetFiltered(_minLevel, _search))
            _displayed.Add(e);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = $"Записей: {_displayed.Count}  |  Всего: {Logger.Instance.GetAll().Count}";
    }

    private void LevelFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _minLevel = LevelFilter.SelectedIndex switch
        {
            0 => LogLevel.Debug,
            1 => LogLevel.Info,
            2 => LogLevel.Warning,
            3 => LogLevel.Error,
            _ => LogLevel.Info
        };
        Reload();
    }

    private void SearchBox_Changed(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        Reload();
    }

    private void ScrollToBottom_Click(object sender, RoutedEventArgs e)
    {
        if (_displayed.Count > 0)
            LogList.ScrollIntoView(_displayed[^1]);
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join("\n", _displayed.Select(x => x.Formatted));
        Clipboard.SetText(text);
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start("explorer.exe", $"/select,\"{Logger.Instance.LogFilePath}\""); }
        catch { }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Logger.Instance.Clear();
        _displayed.Clear();
        UpdateStatus();
        Logger.Instance.Info("UI", "Лог очищен пользователем");
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Logger.Instance.EntryAdded -= OnEntryAdded;
    }
}
