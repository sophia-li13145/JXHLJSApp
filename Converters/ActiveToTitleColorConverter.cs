using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialControlMAUI.Converters
{
    public class ActiveToTitleColorConverter : IValueConverter
    {
        static readonly Color Active = Color.FromArgb("#333333");
        static readonly Color Inactive = Color.FromArgb("#9CA3AF"); // 灰字
        public object Convert(object v, Type t, object p, CultureInfo c) => (v is bool b && b) ? Active : Inactive;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
