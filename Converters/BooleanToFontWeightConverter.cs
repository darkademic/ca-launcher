using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CALauncher.Converters;

public class BooleanToFontWeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? FontWeight.Bold : FontWeight.Normal;
        }
        return FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
