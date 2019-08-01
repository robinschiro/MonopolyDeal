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
using ResourceList = GameObjects.Properties.Resources;

namespace GameObjects
{
    public class Deck
    {
        public List<Card> CardList { get; set; }  // Deck is a list of cards
        static private tvProfile Profile;

        public Deck(tvProfile profile)
        {
            Profile = profile;
            this.CardList = GenerateDeck();

            // Randomize the cards in the deck
            Shuffle(this.CardList);
        }

        public Deck( List<Card> cardList, bool refreshType = false )
        {
            this.CardList = cardList;

            // The 'Type' attribute of each card must be refreshed when cards from the discard pile
            // are used to make a new deck. This is because some cards may have been played as money or a property
            // and thus had their 'Type' attribute changed.
            if (refreshType)
            {
                List<Card> fullDeck = GenerateDeck();

                foreach ( Card card in this.CardList )
                {
                    card.Type = fullDeck[card.CardID].Type;
                }
            }

            // Randomize the cards in the deck
            Shuffle(this.CardList);
        }

        // Scan data from the profile file to generate the cards for the deck.
        // ROBIN TODO: Generalize this method so that it works for cards with any properties (not just those specific to Monopoly Deal).
        public List<Card> GenerateDeck()
        {
            // Variables to hold temporary data.
            string name;
            CardType cardType;
            EnhancementType enhanceType;
            int value;
            PropertyType propertyType;
            PropertyType altPropertyType;
            string uriPath;
            string soundUriPath;
            int actionID;

            List<Card> cardList = new List<Card>();
            
            // Each card must have a unique ID.
            int cardID = 0;

            foreach ( DictionaryEntry keyVal in Profile )
            {
                string resourceName = keyVal.Key as string;
                tvProfile cardProfile = Profile.oProfile(resourceName);

                for ( int a = 0; a < cardProfile.iValue("-Count", 0); ++a )
                {
                    name = cardProfile.sValue("-Name", "");
                    cardType = (CardType)Enum.Parse(typeof(CardType), cardProfile.sValue("-CardType", ""));
                    enhanceType = (EnhancementType)Enum.Parse(typeof(EnhancementType), cardProfile.sValue("-EnhancementType", "None"));
                    value = cardProfile.iValue("-Value", 0);
                    propertyType = (cardProfile.sValue("-PropertyType", "") == "") ? PropertyType.None : (PropertyType)Enum.Parse(typeof(PropertyType), cardProfile.sValue("-PropertyType", ""));
                    altPropertyType = (cardProfile.sValue("-AltPropertyType", "") == "") ? PropertyType.None : (PropertyType)Enum.Parse(typeof(PropertyType), cardProfile.sValue("-AltPropertyType", ""));
                    uriPath = resourceName.Replace("-", string.Empty) + "DrawingImage";

                    string soundEffectFileName = cardProfile.sValue("-SoundEffectFile", string.Empty);
                    soundUriPath = string.IsNullOrWhiteSpace(soundEffectFileName) ? ResourceList.UriPathEmpty : ResourceList.UriPathAudioFolder + soundEffectFileName;
                    
                    actionID = (cardProfile.sValue("-ActionID", "") == "") ? -1 : Convert.ToInt32((cardProfile.sValue("-ActionID", "")));

                    cardList.Add(new Card(name, cardType, value, propertyType, altPropertyType, uriPath, soundUriPath, actionID, cardID));

                    // Iterate the card ID so that it is different for the next card.
                    cardID++;
                }
            }

            return cardList;
        }

        // Returns a list of file names inside a folder containing resources for the calling assembly.
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
