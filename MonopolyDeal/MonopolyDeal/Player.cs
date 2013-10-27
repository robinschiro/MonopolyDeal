using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonopolyDeal
{
    class Player
    {
        const int SIZE_OF_HAND = 5; //Note to Robin: size of hand changes as cards are drawn and played. Calling this constant size of hand may not be the best thing. Maybe initial hand size or something.

        // This list represents the player's hand.
        public List<Card> cardsInHand = new List<Card>();

        // This represents the cards that the player has played on the playing field.
        public List<Card> cardsInPlay = new List<Card>();

        public String name;

        public Player( Deck deck, String name )
        {
            // Initialize the player's hand
            for (int i = 0; i < SIZE_OF_HAND; ++i)
            {
                cardsInHand.Add(deck.cardList[i]);
            }

            this.name = name;
        }
    }
}
