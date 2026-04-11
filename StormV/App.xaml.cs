namespace StormV;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Перехватываем все необработанные исключения
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                FormatException(ex.Exception),
                "StormV — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject is Exception e ? FormatException(e) : ex.ExceptionObject?.ToString() ?? "?";
            MessageBox.Show(msg, "StormV — Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }

    private static string FormatException(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var e = ex;
        int depth = 0;
        while (e != null && depth < 5)
        {
            if (depth > 0) sb.AppendLine("\n─── Inner Exception ───");
            sb.AppendLine(e.GetType().FullName);
            sb.AppendLine(e.Message);
            if (depth == 0) sb.AppendLine("\n" + e.StackTrace);
            e = e.InnerException;
            depth++;
        }
        return sb.ToString();
    }
}
