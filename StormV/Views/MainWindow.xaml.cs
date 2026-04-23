using StormV.ViewModels;
using StormV.Models;

namespace StormV.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.AddServerRequested += OnAddServerRequested;
    }

    private void OnAddServerRequested()
    {
        var dialog = new AddServerDialog { Owner = this };

        dialog.ViewModel.SubscriptionLoaded += (servers, url) =>
        {
            Application.Current.Dispatcher.Invoke(() => _vm.AddSubscriptionServers(servers, url));
        };

        if (dialog.ShowDialog() == true)
        {
            if (dialog.Result != null)
                _vm.AddServerConfig(dialog.Result);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
    }

    private LogWindow? _logWindow;
    private void LogButton_Click(object sender, RoutedEventArgs e)
    {
        if (_logWindow is { IsLoaded: true })
        {
            _logWindow.Activate();
            return;
        }
        _logWindow = new LogWindow { Owner = this };
        _logWindow.Show();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.OnClosing();
    }
}
