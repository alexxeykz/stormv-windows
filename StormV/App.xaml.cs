namespace StormV;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Перехватываем все необработанные исключения
        DispatcherUnhandledException += (_, ex) =>
        {
            var msg = FormatException(ex.Exception);
            WriteErrorLog(msg);
            MessageBox.Show(
                msg + $"\n\nЛог: {ErrorLogPath}",
                "StormV — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject is Exception e ? FormatException(e) : ex.ExceptionObject?.ToString() ?? "?";
            WriteErrorLog(msg);
            MessageBox.Show(msg + $"\n\nЛог: {ErrorLogPath}", "StormV — Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        base.OnStartup(e);
    }

    private static string ErrorLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "StormV-error.txt");

    private static void WriteErrorLog(string text)
    {
        try { File.WriteAllText(ErrorLogPath, text); } catch { }
    }

    private static string FormatException(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var e = ex;
        int depth = 0;
        while (e != null && depth < 10)
        {
            if (depth > 0) sb.AppendLine("\n─── Inner Exception ───");
            sb.AppendLine(e.GetType().FullName);
            sb.AppendLine(e.Message);
            sb.AppendLine(e.StackTrace);
            e = e.InnerException;
            depth++;
        }
        return sb.ToString();
    }
}
