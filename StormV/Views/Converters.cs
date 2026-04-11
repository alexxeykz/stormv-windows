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
