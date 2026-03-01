using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections;


namespace GameObjects
{
    // Creates enum for different card types
    public enum CardType
    {
        Money,
        Property,
        Action,
        Enhancement,
        None
    }

    public enum PropertyType
    {
        None,
        Brown,
        LightBlue,
        Pink,
        Orange,
        Red,
        Yellow,
        Green,
        Blue,
        Railroad,
        Utility,
        Wild
    }

    public enum EnhancementType
    {
        None,
        House,
        Hotel
    }
    
    public class Card
    {       
        public string Name { get; set; }
        public CardType Type { get; set; }
        public int Value { get; set; }
        public PropertyType Color { get; set; }
        public PropertyType AltColor { get; set; }
        public EnhancementType EnhancementType { get; set; }
        public string CardImageUriPath { get; set; }
        public string CardSoundUriPath { get; set; }
        public int ActionID { get; set; }
        public int CardID { get; set; }
        public bool IsFlipped { get; set; }
        
        /// <summary>
        /// The total count of this card in the game
        /// </summary>
        public int TotalCount { get; set; }

        // Constants
        public const int BIRTHDAY_AMOUNT = 2;
        public const int DEBT_AMOUNT = 5;

        public Card()
        {
            this.Name = String.Empty;
            this.CardImageUriPath = String.Empty;
        }

        public Card(
            string name,
            CardType type,
            int value,
            PropertyType color,
            PropertyType altColor,
            string uriPath,
            string soundUriPath,
            int actionID,
            int cardID,
            int totalCount,
            bool isFlipped = false)
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
            this.Color = color;
            this.AltColor = altColor;
            this.CardImageUriPath = uriPath;
            this.CardSoundUriPath = soundUriPath;
            this.ActionID = actionID;
            this.CardID = cardID;
            this.TotalCount = totalCount;
            this.IsFlipped = isFlipped;
        }

        public static int SumOfCardValues(IList cards)
        {
            int sum = 0;

            foreach ( Card card in cards )
            {
                sum += card.Value;
            }

            return sum;
        }
    }
}
