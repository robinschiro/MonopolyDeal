using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows.Controls;
using System.Globalization;
using System.Windows;
using System.Xaml;

namespace AdditionalWindows
{
    class StaticResourceConverter : MarkupExtension, IValueConverter
    {
        private Control _target;

        public StaticResourceConverter()
        {
            
        }

        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            var resourceKey = (string)value;

            object element = null;
            if ( null != _target )
            {
                element = _target.FindResource(resourceKey);
            }

            if (null == element)
            {
                element = Application.Current.FindResource(resourceKey);
            }

            return element;
        }

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
        {
            throw new NotSupportedException();
        }

        public override object ProvideValue( IServiceProvider serviceProvider )
        {
            var rootObjectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            if ( rootObjectProvider == null )
                return this;

            _target = rootObjectProvider.RootObject as Control;
            return this;
        }
    }
}