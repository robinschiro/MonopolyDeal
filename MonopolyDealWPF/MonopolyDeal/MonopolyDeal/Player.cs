using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonopolyDeal
{
    class Player
    {
        const int INITIAL_SIZE_OF_HAND = 5;

        // This list represents the player's hand.
        public List<Card> cardsInHand = new List<Card>();

        // This represents the cards that the player has played on the playing field.
        public List<Card> cardsInPlay = new List<Card>();

        public String name;

        public Player( Deck deck, String name )
        {
            // Initialize the player's hand
            for (int i = 0; i < INITIAL_SIZE_OF_HAND; ++i)
            {
                cardsInHand.Add(deck.CardList[0]);
                deck.CardList.Remove(deck.CardList[0]);
            }

            this.name = name;
        }
    }
}
