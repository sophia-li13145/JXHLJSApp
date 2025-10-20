using System.Globalization;

namespace IndustrialControlMAUI.Converters
{
    public sealed class NullOrEmptyToBoolInverseConverter : IValueConverter
    {
        // 非空 => true；为空 => false
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // 如果顺手也需要正向版（空=>true），一起放这
    public sealed class NullOrEmptyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

