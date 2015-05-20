using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameObjects;
using System.Windows.Controls;
using System.Windows.Input;

namespace MonopolyDeal
{
    public interface IGameClient
    {
        #region Card Manipulation Methods

        void DisplayOpponentCards( Object filler = null );
        void AddCardToGrid( Card card, Grid grid, Player player, bool isHand, int position );
        void RemoveCardButtonFromHand( Button cardButton );
        void SelectCard( Button cardButton );
        void DeselectCard( Button cardButton );

        #endregion

        #region Events

        void SelectCardEvent( object sender, MouseButtonEventArgs args );
        void PlayCardEvent( object sender, MouseButtonEventArgs args );

        #endregion
    }
}
