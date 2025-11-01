using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrpfdManager
{
    /// <summary>
    /// true => 2*, false => 0 (collapsed row). Keeps content row as * so GridSplitter can resize.
    /// </summary>
    public sealed class BoolToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var on = value is bool b && b;
            return on ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is GridLength gl) && gl.Value > 0;
    }
}