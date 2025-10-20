using System.Globalization;


namespace IndustrialControlMAUI.Converters
{
    public class StringToNullableDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            if (decimal.TryParse(value.ToString(), out var dec))
            {
                // 格式化并去掉多余的 0
                return dec.ToString("0.####################", culture);
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            return decimal.TryParse(s, NumberStyles.Number, culture, out var dec) ? dec : (decimal?)null;
        }
    }


}
