using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MonopolyDeal
{
    public class Deck
    {
        public String TextureName { get; set; }
        public List<Card> CardList { get; set; }  // Deck is a list of cards
        
        // This constant is temporary; it will not be needed once we are importing values from a spreadsheet.
        private const int TOTAL_NUMBER_OF_CARDS = 110;

        public Deck()
        {
            // Creates 3 cards with different values for testing
            // Will eventually read in .xls spreadsheet and create each card
            CardList = new List<Card>();
            for (int i = 0; i < TOTAL_NUMBER_OF_CARDS; i++)
            {
                CardList.Add(new Card(i, ".\\Images\\10million.jpg"));
            }
            Shuffle(CardList); // Randomizes the cards in the deck

            foreach (Card card in CardList)
            {
                Debug.WriteLine(card.Value); // Print out list of cards in deck
            }
            // I don't think this is right. I'm doing this late at night. Will revisit.

            TextureName = "cardback";
        }

        public void Shuffle<T>(IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
