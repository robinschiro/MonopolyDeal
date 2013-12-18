﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameObjects
{
    public class Player
    {
        const int INITIAL_SIZE_OF_HAND = 5;

        // This represents the cards that the player has played on the playing field.
        public List<Card> CardsInPlay { get; set; }

        // This list represents the player's hand.
        public List<Card> CardsInHand { get; set; }
        
        public string Name { get; set; }

        // Use this constructor when generating a player for the first time.
        public Player( Deck deck, string name )
        {
            this.CardsInPlay = new List<Card>();
            this.CardsInHand = new List<Card>();

            // Initialize the player's hand
            for ( int i = 0; i < INITIAL_SIZE_OF_HAND; ++i )
            {
                CardsInHand.Add(deck.CardList[0]);
                deck.CardList.Remove(deck.CardList[0]);
            }

            this.Name = name;
        }

        public Player( string name, List<Card> cardsInPlay, List<Card> cardsInHand )
        {
            this.Name = name;
            this.CardsInPlay = cardsInPlay;
            this.CardsInHand = cardsInHand;
        }

        public Player()
        {
        }
    }
}