using Application = System.Windows.Application;

namespace StormV;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public static bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
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
        InitTrayIcon();
    }

    private void InitTrayIcon()
    {
        var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                   ?? System.Drawing.SystemIcons.Application;

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Показать StormV", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => Exit());

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "StormV",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static void ShowMainWindow()
    {
        var window = Current.MainWindow;
        if (window == null) return;
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public new void Exit()
    {
        IsExiting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
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
