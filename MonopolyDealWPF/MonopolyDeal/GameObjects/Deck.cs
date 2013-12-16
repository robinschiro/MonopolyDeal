using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Collections;

namespace GameObjects
{
    public class Deck
    {
        public String TextureName { get; set; }
        public List<Card> CardList { get; set; }  // Deck is a list of cards
        public String Test { get; set; }

        // This constant is temporary; it will not be needed once we are importing values from a spreadsheet.
        private const int TOTAL_NUMBER_OF_CARDS = 110;

        public Deck()
        {
            // Creates cards with different values for testing. A card is created for each picture in the Images folder.
            // Will eventually read in .xls spreadsheet and create each card
            CardList = new List<Card>();
            string[] files = GetResourcesUnder("Images");
            for ( int i = 0; i < files.Length; i++ )
            {
                string uriPath = "pack://application:,,,/GameObjects;component/Images/" + files[i];
                CardList.Add(new Card(i, uriPath));
            }

            Shuffle(CardList); // Randomizes the cards in the deck

            foreach ( Card card in CardList )
            {
                Debug.WriteLine(card.Value); // Print out list of cards in deck
            }

            TextureName = "cardback";
        }

        public Deck( List<Card> cardList )
        {
            this.CardList = cardList;
        }

        public Deck( bool test )
        {
            Test = "Client Created";
            CardList = new List<Card>();
        }

        // Returns a list a file names inside a folder containing resources for the calling assembly.
        // This is needed in order to properly embed the images in the .exe (Before we were using relative
        // file paths, which caused the .exe to crash when it was run from a different directory).
        // This method was found here: http://tinyurl.com/m8d8dvl
        public static string[] GetResourcesUnder( string strFolder )
        {
            strFolder = strFolder.ToLower() + "/";

            Assembly oAssembly = Assembly.GetCallingAssembly();
            string strResources = oAssembly.GetName().Name + ".g.resources";
            Stream oStream = oAssembly.GetManifestResourceStream(strResources);
            ResourceReader oResourceReader = new ResourceReader(oStream);

            var vResources =
                from p in oResourceReader.OfType<DictionaryEntry>()
                let strTheme = (string)p.Key
                where strTheme.StartsWith(strFolder)
                select strTheme.Substring(strFolder.Length);

            return vResources.ToArray();
        }

        public void Shuffle<T>( IList<T> list )
        {
            Random rng = new Random();
            int n = list.Count;
            while ( n > 1 )
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
