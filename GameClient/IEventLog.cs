using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameObjects;

namespace GameClient
{
    interface IEventLog
    {
        #region Card Events

        void PublishPlayCardEvent( Player player, Card card );
        
        void PublishPlayTargetedCardEvent( Player player, Player target, Card card );
        
        void PublishDiscardEvent( Player player, Card card );
        
        void PublishJustSayNoEvent( Player playerSayingNo );
        
        void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid );
       
        void PublishSlyDealEvent( Player thief, Player victim, Card property );
        
        void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty );
        
        void PublishDealbreakerEvent( Player thief, Player victim, List<Card> monopoly );

        #endregion

        void PublishNewTurnEvent( Player player ); 

        void PublishPlayerWonEvent( Player winner );

        void PublishDeckEmptyEvent(bool discardPileReshuffled);

        void PublishBellRungEvent( Player ringee );
    }
}
