using System.Globalization;

namespace IndustrialControlMAUI.Converters
{
    public class BoolToExpandIconConverter : IValueConverter
    {
        // 想用别的符号可改："▼/▶"、"▾/▸"、"⌄/›" 等
        public string Expanded { get; set; } = "▾";
        public string Collapsed { get; set; } = "▸";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Expanded : Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}
