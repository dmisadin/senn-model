using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace SENNModel.Converters;

public class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return parameter;

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
