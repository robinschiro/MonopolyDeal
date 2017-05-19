using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace MonopolyDeal
{
    class MultiBoolConverter : IMultiValueConverter
    {
        public object Convert( object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture )
        {
            bool hasDrawn = System.Convert.ToBoolean(values[0]);
            bool isCurrentTurnOwner = System.Convert.ToBoolean(values[1]);
            
            return !hasDrawn && isCurrentTurnOwner;
        }

        public object[] ConvertBack( object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture )
        {
            throw new NotImplementedException();
        }
    }
}
