using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameObjects;
using Lidgren.Network;
using System.Windows;
using System.Media;
using System.IO;
using System.Diagnostics;
using tvToolbox;

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

        private static int NumberMonopoliesRequiredToWin = 3;

        private static SoundPlayer UtilitySoundPlayer = new SoundPlayer();

        #endregion

        #region Card List Functions

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

        // Determine if a card with a given name is in a card list.
        public static bool IsCardInCardList( string name, List<Card> cardList )
        {
            foreach ( Card cardInMonopoly in cardList )
            {
                if ( name == cardInMonopoly.Name )
                {
                    return true;
                }
            }

            return false;
        }

        // Determine if a provided card list is a monopoly.
        public static bool IsCardListMonopoly( List<Card> cardList )
        {
            PropertyType monopolyColor = GetCardListColor(cardList);
            int countOfProperties = 0;

            // First count the number of properties in the list. This algorithm excludes houses and hotels from the count.
            foreach ( Card card in cardList )
            {
                if ( card.Type != CardType.Enhancement )
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

        // Determine if the given card can be added to the given list of cards.
        public static bool CanCardBeAddedToList(Card card, List<Card> cardList)
        {
            return !IsCardListMonopoly(cardList) && ((card.Color == PropertyType.Wild) || (GetCardListColor(cardList) == card.Color));
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

        // Find a monopoly that does not have a house on it.
        public static List<Card> FindMonopolyWithoutHouse( Player player )
        {
            List<List<Card>> monopolies = ClientUtilities.FindMonopolies(player);

            foreach ( List<Card> monopoly in monopolies )
            {
                if ( !ClientUtilities.IsCardInCardList("House", monopoly) )
                {
                    return monopoly;
                }
            }
            return null;
        }

        // Find a monopoly that does not have a hotel on it (but does have a house).
        public static List<Card> FindMonopolyWithoutHotel( Player player )
        {
            List<List<Card>> monopolies = ClientUtilities.FindMonopolies(player);

            foreach ( List<Card> monopoly in monopolies )
            {
                if ( ClientUtilities.IsCardInCardList("House", monopoly) && !ClientUtilities.IsCardInCardList("Hotel", monopoly) )
                {
                    return monopoly;
                }
            }
            return null;
        }
        #endregion

        #region Just Say No Handling

        public static bool AskPlayerAboutJustSayNo(string title, string baseMessage, bool playerHasJustSayNo)
        {
            MessageBoxResult result;
            bool useJustSayNo = false;
            if (playerHasJustSayNo)
            {
                result = MessageBox.Show(baseMessage + "\n\nWould you like to use your \"Just Say No!\" card?", title, MessageBoxButton.OKCancel);  
                if (MessageBoxResult.OK == result)
                {
                    result = MessageBox.Show("Are you sure you want to use your \"Just Say No!\" card?", "Confirmation", MessageBoxButton.OKCancel);
                    useJustSayNo = MessageBoxResult.OK == result;
                }
            }
            else
            {
                MessageBox.Show(baseMessage + "\n\nPress OK to continue.", title, MessageBoxButton.OK);
            }

            return useJustSayNo;
        }

        #endregion

        #region Sound

        public static void PlaySound( string uriPath )
        {
            try
            {
                var streamResourceInfo = Application.GetResourceStream(new Uri(uriPath));
                Stream audioStream = streamResourceInfo.Stream;

                UtilitySoundPlayer.Stream = audioStream;
                UtilitySoundPlayer.Play();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Failed to find audio file for sound. Be sure to add the file to the 'GameObjects' project as a resource and rebuild the solution. " +
                    $"Exception Details: {ex.Message}");
            }
        }

        public static void SetClientVolume( int volume )
        {
            int currentProcessPid = Process.GetCurrentProcess().Id;
            VolumeMixer.SetApplicationVolume(currentProcessPid, Convert.ToSingle(volume));
        }

        #endregion

        #region Client Settings

        public static tvProfile GetClientSettings(string settingsPath)
        {
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), settingsPath);
            return new tvProfile(settingsFilePath, tvProfileFileCreateActions.NoPromptCreateFile, abUseXmlFiles: true);
        }

        public static bool DetermineIfPlayerHasWon( Player player )
        {
            int numberOfMonopolies = player.CardsInPlay.Count(cardList => IsCardListMonopoly(cardList));
            return numberOfMonopolies >= NumberMonopoliesRequiredToWin;
        }

        #endregion
    }
}
