using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MineSweeper.Converters;

/// <summary>
/// Converts an adjacent mine count (1–8) to a <see cref="SolidColorBrush"/> by
/// looking up the key <c>Num{n}</c> from <see cref="Application.Resources"/>.
/// Each theme ResourceDictionary defines its own Num1…Num8 palette so the colours
/// automatically update when the user switches themes at runtime.
/// </summary>
[ValueConversion(typeof(int), typeof(SolidColorBrush))]
public sealed class NumberToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Fallback =
        new(Color.FromRgb(180, 180, 180));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int n and >= 1 and <= 8)
        {
            if (Application.Current?.Resources[$"Num{n}"] is SolidColorBrush brush)
                return brush;
        }
        return Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException($"{nameof(NumberToColorConverter)} is one-way only.");
}
