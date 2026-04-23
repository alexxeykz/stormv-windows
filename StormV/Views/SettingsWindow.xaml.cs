using StormV.ViewModels;

namespace StormV.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var vm = new SettingsViewModel();
        DataContext = vm;
        vm.CloseRequested += () => Close();
        vm.ExitRequested += () => ((App)Application.Current).Exit();
    }
}
