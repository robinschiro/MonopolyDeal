using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameObjects;

namespace Utilities
{

    #region Enums

    // The integer values of the elements in this enum correspond to the ActionIDs of the associated cards.
    public enum TheftType
    {
        Dealbreaker = 5,
        SlyDeal,
        ForcedDeal
    }

    #endregion
    

    public class ClientUtilities
    {
        #region Static Variables

        // For each type of property, store the number of cards of that property type that make up a complete monopoly
        // ROBIN TODO: Think of a way to do this without hardcoding the data.
        private static Dictionary<PropertyType, int> MonopolyData = new Dictionary<PropertyType, int>()
        {
            {PropertyType.Blue, 2},
            {PropertyType.Brown, 2},
            {PropertyType.Green, 3},
            {PropertyType.LightBlue, 3},
            {PropertyType.Orange, 3},
            {PropertyType.Pink, 3},
            {PropertyType.Railroad, 4},
            {PropertyType.Red, 3},
            {PropertyType.Utility, 2},
            {PropertyType.Wild, 0},
            {PropertyType.Yellow, 3},
            {PropertyType.None, -1}
        };

        public static Dictionary<PropertyType, Dictionary<int, int>> RentData = new Dictionary<PropertyType, Dictionary<int, int>>()
        {
            {PropertyType.Blue, new Dictionary<int, int>() 
                                    {
                                        {1, 3},
                                        {2, 8}
                                    }},
            {PropertyType.Brown, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2}
                                    }},
            {PropertyType.Green, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 4},
                                        {3, 7}
                                    }},
            {PropertyType.LightBlue, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 3}
                                    }},
            {PropertyType.Orange, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 3},
                                        {3, 5}
                                    }},
            {PropertyType.Pink, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 4}
                                    }},
            {PropertyType.Railroad, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 3},
                                        {4, 4}
                                    }},
            {PropertyType.Red, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 3},
                                        {3, 6},
                                    }},
            {PropertyType.Utility, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2}
                                    }},
            {PropertyType.Yellow, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 4},
                                        {3, 6},
                                    }},
        };

        #endregion

        #region Functions

        // Given a list of cards, determine the color of the monopoly being formed by the cards.
        public static PropertyType GetCardListColor( List<Card> cardList )
        {
            if ( cardList.Count > 0 )
            {
                foreach ( Card card in cardList )
                {
                    if ( PropertyType.None != card.Color && PropertyType.Wild != card.Color )
                    {
                        return card.Color;
                    }
                }

                return PropertyType.Wild;
            }
            else
            {
                return PropertyType.None;
            }
        }

        // Determine if a provided card list is a monopoly.
        public static bool IsCardListMonopoly( List<Card> cardList )
        {
            PropertyType monopolyColor = GetCardListColor(cardList);
            int countOfProperties = 0;

            // First count the number of properties in the list. This algorithm excludes houses and hotels from the count.
            foreach ( Card card in cardList )
            {
                if ( card.Color != PropertyType.None )
                {
                    countOfProperties++;
                }
            }

            // If the number of properties in the list matches the number required for a monopoly of that color, it is a monopoly.
            // If the monopolyColor is 'Wild', then the list must contain only Multicolor Property Wild Card(s).
            if ( PropertyType.Wild != monopolyColor && countOfProperties == MonopolyData[monopolyColor] )
            {
                return true;
            }

            return false;
        }

        // Return a list of all the card groups in the player's CardsInPlay that are monopolies.
        public static List<List<Card>> FindMonopolies( Player player )
        {
            List<List<Card>> monopolies = new List<List<Card>>();

            // Iterate through the card lists. Always skip the first one, since it is reserved for money.
            for ( int i = 1; i < player.CardsInPlay.Count; ++i )
            {
                if ( ClientUtilities.IsCardListMonopoly(player.CardsInPlay[i]) )
                {
                    monopolies.Add(player.CardsInPlay[i]);
                }
            }

            return monopolies;
        }

        #endregion

    }
}
