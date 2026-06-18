using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SshManage.Converters;

public class BoolToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "✅" : "❌";
        }
        return "❌";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
