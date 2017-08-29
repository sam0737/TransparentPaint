using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Hellosam.Net.TransparentPaint
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) value = false;
            if (value is bool?)
            {
                value = ((bool?)value).Value;
            }
            if (value is bool)
            {
                return (bool)value ? Visibility.Visible : (parameter != null ? Visibility.Hidden : Visibility.Collapsed);
            }
            else if (value is string)
            {
                return !(string.IsNullOrEmpty((string)value)) ? Visibility.Visible : (parameter != null ? Visibility.Hidden : Visibility.Collapsed);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    class BooleanNotVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) value = false;
            if (value is bool?)
            {
                value = ((bool?)value).Value;
            }
            if (value is bool)
            {
                return (!(bool)value) ? Visibility.Visible : (parameter != null ? Visibility.Hidden : Visibility.Collapsed);
            }
            else if (value is string)
            {
                return (string.IsNullOrEmpty((string)value)) ? Visibility.Visible : (parameter != null ? Visibility.Hidden : Visibility.Collapsed);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
