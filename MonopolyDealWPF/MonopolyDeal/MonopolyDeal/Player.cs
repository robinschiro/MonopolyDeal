using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameServer;

namespace MonopolyDeal
{
    class Player
    {
        const int INITIAL_SIZE_OF_HAND = 5;

        // This list represents the player's hand.
        public List<Card> CardsInHand { get; set; }

        // This represents the cards that the player has played on the playing field.
        public List<Card> CardsInPlay { get; set; }

        public String Name { get; set; }

        public Player( Deck deck, String name )
        {
            this.CardsInHand = new List<Card>();
            this.CardsInPlay = new List<Card>();

            // Initialize the player's hand
            for (int i = 0; i < INITIAL_SIZE_OF_HAND; ++i)
            {
                CardsInHand.Add(deck.CardList[0]);
                deck.CardList.Remove(deck.CardList[0]);
            }

            this.Name = name;
        }
    }
}
