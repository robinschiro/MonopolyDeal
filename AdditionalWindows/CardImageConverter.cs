using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xaml;
using GameObjects;
using Utilities;

namespace AdditionalWindows
{
    class CardImageConverter : MarkupExtension, IValueConverter
    {
        private Control _target;

        public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
        {
            var card = value as Card;
            if ( card == null )
                return null;

            return ClientUtilities.ConvertCardToImage(_target, card);
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
