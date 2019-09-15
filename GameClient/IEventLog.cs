﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameObjects;

namespace GameClient
{
    interface IEventLog
    {
        void PublishPlayCardEvent( Player player, Card card );
        void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid );
        void PublishSlyDealEvent( Player thief, Player victim, Card property );
        void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty );
        void PublishDealbreakerEvent( Player thief, Player victim, List<Card> monopoly );
        void PublishPlayerWonEvent( Player winner );

        void PublishCustomEvent( string eventLogline );
    }
}