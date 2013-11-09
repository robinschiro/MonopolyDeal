using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MonopolyDeal
{
    public class Deck
    {
        public String textureName = "cardback";
        public List<Card> cardList = new List<Card>(); // Deck is a list of cards

        public Deck()
        {
            // Creates 3 cards with different values for testing
            // Will eventually read in .xls spreadsheet and create each card
            for (int i = 0;i<10;i++)
            {
                Card card = new Card(i);
                cardList.Add(card);
            }
            Shuffle(cardList); // Randomizes the cards in the deck

            foreach (Card card in cardList)
            {
                Debug.WriteLine(card.getValue()); // Print out list of cards in deck
            }
            // I don't think this is right. I'm doing this late at night. Will revisit.
        }

        public Card getCard(int location)
        {
            return this.cardList[location];
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
