using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace IndustrialControlMAUI.Converters
{
    public class DecimalTrimConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";

            if (value is decimal dec)
                return dec.ToString("0.####"); // 去掉多余 0

            if (value is double dob)
                return dob.ToString("0.####");

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

