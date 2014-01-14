using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Collections;
using tvToolbox;

namespace GameObjects
{
    public class Deck
    {
        public String TextureName { get; set; }
        public List<Card> CardList { get; set; }  // Deck is a list of cards
        private tvProfile Profile;

        public Deck(tvProfile profile)
        {
            this.Profile = profile;
            GenerateDeck();
        }

        public Deck( List<Card> cardList )
        {
            this.CardList = cardList;
        }

        // Creates cards with different values for testing. A card is created for each picture in the Images folder.
        // Will eventually read in .xls spreadsheet and create each card
        public void GenerateDeck()
        {
            CardList = new List<Card>();
            string[] files = GetResourcesInFolder("Images");

            // Alphabetize the file names.
            files = files.OrderBy(d => d).ToArray();
            for ( int i = 0; i < files.Length; i++ )
            {
                tvProfile cardProfile = Profile.oProfile("-" + files[i]);

                for ( int a = 0; a < cardProfile.iValue("-Count", 0); ++a )
                {
                    string name = cardProfile.sValue("-Name", "");
                    CardType cardType = (CardType)Enum.Parse(typeof(CardType), cardProfile.sValue("-CardType", ""));
                    int value = cardProfile.iValue("-Value", 0);
                    PropertyType propertyType = cardProfile.sValue("-PropertyType", "") == "" ? PropertyType.None : (PropertyType)Enum.Parse(typeof(PropertyType), cardProfile.sValue("-PropertyType", ""));
                    PropertyType altPropertyType = cardProfile.sValue("-AltPropertyType", "") == "" ? PropertyType.None : (PropertyType)Enum.Parse(typeof(PropertyType), cardProfile.sValue("-AltPropertyType", ""));
                    string uriPath = "pack://application:,,,/GameObjects;component/Images/" + files[i];

                    CardList.Add(new Card(name, cardType, value, propertyType, altPropertyType, uriPath));                  
                }
            }

            Shuffle(CardList); // Randomizes the cards in the deck

            foreach ( Card card in CardList )
            {
                Debug.WriteLine(card.Value); // Print out list of cards in deck
            }

            TextureName = "cardback";
        }

        // Returns a list a file names inside a folder containing resources for the calling assembly.
        // This is needed in order to properly embed the images in the .exe (Before we were using relative
        // file paths, which caused the .exe to crash when it was run from a different directory).
        // This method was found here: http://tinyurl.com/m8d8dvl
        public static string[] GetResourcesInFolder( string strFolder )
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
