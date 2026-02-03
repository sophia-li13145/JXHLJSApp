using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JXHLJSApp.Converters
{
    /// <summary>
    /// 将 decimal? ↔ string 相互转换，自动去掉多余 0
    /// </summary>
    public class DecimalToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            if (value is decimal d)
                return d.ToString("G29", culture); // 去掉末尾 0
            if (value is double dd)
                return ((decimal)dd).ToString("G29", culture);
            return value.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && decimal.TryParse(s, NumberStyles.Any, culture, out var d))
                return d;
            return null; // 无法解析返回 null
        }
    }
}
