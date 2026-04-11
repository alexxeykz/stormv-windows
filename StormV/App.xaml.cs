namespace StormV;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Перехватываем все необработанные исключения
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"Ошибка запуска:\n\n{ex.Exception.GetType().Name}\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "StormV — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"Критическая ошибка:\n\n{ex.ExceptionObject}",
                "StormV — Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }
}
