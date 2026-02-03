using System.Globalization;
using System.Collections;

namespace JXHLJSApp.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value: 当前这一行绑定的 item
            // parameter: 传进来的 CollectionView (x:Reference)
            if (value == null || parameter is not CollectionView cv) return "";

            var items = cv.ItemsSource as IEnumerable;
            if (items == null) return "";

            int i = 0;
            foreach (var it in items)
            {
                // 用 ReferenceEquals 或 Equals 其中一个命中即可
                if (ReferenceEquals(it, value) || Equals(it, value))
                    return (i + 1).ToString();
                i++;
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
