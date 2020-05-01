using System;
using System.Collections.Generic;
using System.Linq;
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
        public List<Card> CardList { get; set; }

        public Deck(tvProfile profile)
        {
            this.CardList = GenerateCards(profile);
        }

        // Typically used when updating deck data over the network.
        public Deck(List<Card> cardList)
        {
            this.CardList = cardList;
        }

        public static Deck CreateDeckFromDiscardPile(List<Card> discardPile, Dictionary<int, Card> allCards)
        {
            List<Card> deckCards = discardPile;

            // The 'Type' attribute of each card must be refreshed when cards from the discard pile
            // are used to make a new deck. This is because some cards may have been played as money or a property
            // and thus had their 'Type' attribute changed.
            foreach (Card card in deckCards)
            {
                card.Type = allCards[card.CardID].Type;
            }

            Shuffle(deckCards);
            return new Deck(deckCards);
        }

        // Scan data from the profile file to generate the cards for the deck.
        private List<Card> GenerateCards(tvProfile profile)
        {
            List<Card> cardList = new List<Card>();

            if (null == profile)
            {
                Console.WriteLine("Deck profile file is null");
                return cardList;
            }

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
            
            // Each card must have a unique ID.
            int cardID = 0;
            
            foreach (DictionaryEntry keyVal in profile)
            {
                string resourceName = keyVal.Key as string;
                tvProfile cardProfile = profile.oProfile(resourceName);

                for (int a = 0; a < cardProfile.iValue("-Count", 0); ++a)
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

            Shuffle(cardList);

            Console.WriteLine($"Number of cards parsed: {cardList.Count}");

            return cardList;
        }

        private static void Shuffle<T>( IList<T> list )
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
