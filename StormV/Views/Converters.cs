using System.Globalization;
using StormV.Services;

namespace StormV.Views;

/// <summary>
/// Маскирует IP-адрес для отображения в UI: 185.199.108.153 → 185.***.***.*
/// </summary>
public class IpMaskConverter : System.Windows.Data.IValueConverter
{
    public static readonly IpMaskConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s) return EncryptionService.MaskSensitive(s);
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Инвертирует bool: true → false, false → true. Для блокировки кнопки во время загрузки.
/// </summary>
public class InverseBoolConverter : System.Windows.Data.IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// true (проверяем) → "Проверяю...", false → "Проверить обновления"
/// </summary>
public class BoolToCheckTextConverter : System.Windows.Data.IValueConverter
{
    public static readonly BoolToCheckTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Проверяю..." : "Проверить обновления";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
