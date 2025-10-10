using System.Globalization;


namespace IndustrialControlMAUI.Converters
{
    public class ProcessBoolToColorConverter : IValueConverter
    {
        // ConverterParameter: "ColorWhenTrue|ColorWhenFalse"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            var parts = (parameter as string)?.Split('|');
            return Color.FromArgb(flag ? (parts?[0] ?? "#FFFFFF") : (parts?[1] ?? "#5B3FF3"));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }

}
