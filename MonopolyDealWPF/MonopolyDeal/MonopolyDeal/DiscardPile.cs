using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonopolyDeal
{
    class DiscardPile
    {
        public List<Card> CardList { get; set; }

        public DiscardPile()
        {
            this.CardList = new List<Card>();
        }
    }
}
