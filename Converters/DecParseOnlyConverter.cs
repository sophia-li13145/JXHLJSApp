using System;
using System.Globalization;
using Microsoft.Maui.Controls;
namespace JXHLJSApp.Converters;
public class DecParseOnlyConverter : IValueConverter
{
    // 模型 -> 界面
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;

        // 有值时 decimal? 会被装箱成 decimal，所以直接判 decimal 即可
        if (value is decimal d)
            return d.ToString("G29", culture);

        // 其它 IFormattable（如 double、int），原样格式化；否则 ToString()
        if (value is IFormattable f)
            return f.ToString(null, culture);

        return value.ToString();
    }

    // 界面 -> 模型
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value as string)?.Trim();
        if (string.IsNullOrEmpty(s))
            return null;

        if (decimal.TryParse(s, NumberStyles.Any, culture, out var d))
            return d;

        // 半输入（如 "1."）时不更新源，避免打断输入法
        return Binding.DoNothing;
    }
}