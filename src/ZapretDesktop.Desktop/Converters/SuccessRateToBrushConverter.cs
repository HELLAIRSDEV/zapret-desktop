using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ZapretDesktop.Desktop.Converters;

public class SuccessRateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double successRate)
        {
            return successRate switch
            {
                >= 80 => new SolidColorBrush(Color.FromRgb(46, 204, 113)), // Green
                >= 60 => new SolidColorBrush(Color.FromRgb(241, 196, 15)), // Yellow
                >= 40 => new SolidColorBrush(Color.FromRgb(230, 126, 34)), // Orange
                _ => new SolidColorBrush(Color.FromRgb(231, 76, 60))      // Red
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}