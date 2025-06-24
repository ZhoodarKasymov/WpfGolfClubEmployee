using System.Globalization;
using System.Windows.Data;

namespace GolfClubSystem.Helpers;

public class TimeOnlyToDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeOnly timeOnly)
        {
            return new DateTime(1, 1, 1, timeOnly.Hour, timeOnly.Minute, timeOnly.Second); // Create a DateTime from TimeOnly
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return new TimeOnly(dateTime.Hour, dateTime.Minute, dateTime.Second); // Convert DateTime back to TimeOnly
        }
        return null;
    }
}