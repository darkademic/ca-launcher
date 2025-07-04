using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CALauncher.ViewModels;

namespace CALauncher.Converters;

public class StatusTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StatusType statusType)
        {
            return statusType switch
            {
                StatusType.Success => SolidColorBrush.Parse("#4CAF50"), // Green
                StatusType.Warning => SolidColorBrush.Parse("#FFDD00"), // Orange/Yellow
                StatusType.Error => SolidColorBrush.Parse("#FF0000"),   // Red
                StatusType.Normal => SolidColorBrush.Parse("#888"),     // Light gray (default)
                _ => SolidColorBrush.Parse("#888")
            };
        }

        return SolidColorBrush.Parse("#888");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
