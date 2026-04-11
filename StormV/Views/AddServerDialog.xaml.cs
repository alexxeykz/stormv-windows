using StormV.Models;
using StormV.ViewModels;

namespace StormV.Views;

public partial class AddServerDialog : Window
{
    private readonly AddServerViewModel _vm;
    public ServerConfig? Result => _vm.Result;

    public AddServerDialog()
    {
        InitializeComponent();
        _vm = new AddServerViewModel();
        DataContext = _vm;

        _vm.CloseRequested += () =>
        {
            DialogResult = _vm.Confirmed;
            Close();
        };

        Loaded += (_, _) => UrlBox.Focus();
    }
}
