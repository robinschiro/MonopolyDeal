using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

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
            // Creates cards with different values for testing. A card is created for each picture in the Images folder.
            // Will eventually read in .xls spreadsheet and create each card
            CardList = new List<Card>();
            DirectoryInfo imageDirectory = new DirectoryInfo("..\\..\\Images");
            FileInfo[] files = imageDirectory.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                CardList.Add(new Card(i, ".\\Images\\" + files[i].Name));
            }

            Shuffle(CardList); // Randomizes the cards in the deck

            foreach (Card card in CardList)
            {
                Debug.WriteLine(card.Value); // Print out list of cards in deck
            }

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
