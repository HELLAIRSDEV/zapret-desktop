using System.Globalization;
using System.Windows.Data;

namespace ZapretDesktop.Desktop.Converters;

public class BooleanToStartStopConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Остановить" : "Запустить";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}