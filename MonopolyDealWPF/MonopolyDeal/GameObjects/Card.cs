﻿using System;
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

    
    public class Card
    {       
        public string Name { get; set; }
        public CardType Type { get; set; }
        public int Value { get; set; }
        public PropertyType Color { get; set; }
        public PropertyType AltColor { get; set; }
        public string CardImageUriPath { get; set; }
        public int ActionID { get; set; }
        public int CardID { get; set; }
        public bool IsFlipped { get; set; }

        // Constants
        public const int BIRTHDAY_AMOUNT = 2;
        public const int DEBT_AMOUNT = 5;

        public Card()
        {
            this.Name = String.Empty;
            this.CardImageUriPath = String.Empty;
        }

        public Card( string name, CardType type, int value, PropertyType color, PropertyType altColor, string uriPath, int actionID, int cardID, bool isFlipped = false ) // Create a card given a type, name, and value
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
            this.Color = color;
            this.AltColor = altColor;
            this.CardImageUriPath = uriPath;
            this.ActionID = actionID;
            this.CardID = cardID;
            this.IsFlipped = isFlipped;
        }

        //Standard card for testing purposes
        public Card( int value ) // Takes in value to differentiate between cards
        {
            this.Type = CardType.Action;
            this.Name = "Test Card";
            this.Value = value;
        }

        // Create a card from a string representing the file path to the card's image.
        public Card( int value, String path )
        {
            this.Value = value;
            this.CardImageUriPath = path;
            this.Type = CardType.None;
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
