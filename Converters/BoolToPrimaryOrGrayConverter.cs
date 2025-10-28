using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialControlMAUI.Converters
{
    public class BoolToPrimaryOrGrayConverter : IValueConverter
    {
        static readonly Color Primary = Color.FromArgb("#3B82F6");
        static readonly Color Gray = Color.FromArgb("#E5E7EB");
        public object Convert(object v, Type t, object p, CultureInfo c) => (v is bool b && b) ? Primary : Gray;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
