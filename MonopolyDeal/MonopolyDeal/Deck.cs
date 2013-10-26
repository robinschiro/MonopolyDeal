using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonopolyDeal
{
    class Deck
    {
        static Random _random = new Random();

        public Deck()
        {
            // Creates 3 cards with different values for testing
            // Will eventually read in .xls spreadsheet and create each card
            Card card1 = new Card(1);
            Card card2 = new Card(2);
            Card card3 = new Card(3);
            Card[] deck = new Card[2]; // Deck is an array of cards
            Shuffle(deck); // Randomizes the cards in the deck
            // I don't think this is right. I'm doing this late at night. Will revisit.
        }

        public Card getCard(int location)
        {
            //todo
            return null;
        }

        // Known as the Fisher-Yates Shuffle
        public static void Shuffle<T>(T[] array)
        {
            var random = _random;
            for (int i = array.Length; i > 1; i--)
            {
                // Pick random element to swap.
                int j = random.Next(i); // 0 <= j <= i-1
                // Swap.
                T tmp = array[j];
                array[j] = array[i - 1];
                array[i - 1] = tmp;
            }
        }

        public String toString()
        {
            return 
        }
    }
}
