using System.Globalization;

namespace IndustrialControlMAUI.Converters
{
    public class InspectResultToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? result = value?.ToString()?.Trim();

            if (string.Equals(result, "合格", StringComparison.OrdinalIgnoreCase))
                return Colors.Green; // 合格 → 绿色

            if (string.Equals(result, "不合格", StringComparison.OrdinalIgnoreCase))
                return Colors.Red;   // 不合格 → 红色

            return Colors.Black; // 其他 → 黑色（默认）
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
