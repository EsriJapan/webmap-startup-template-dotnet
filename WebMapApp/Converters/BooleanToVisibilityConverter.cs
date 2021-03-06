﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace WebMapApp.Converters
{
    /// <summary>
    /// Boolean と Visibility のコンバーター
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool && (bool)value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }
}
