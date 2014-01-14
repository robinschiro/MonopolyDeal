using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;


namespace GameObjects
{
    // Creates enum for different card types
    public enum CardType
    {
        Money,
        Property,
        Action
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

        public Card( CardType type, String name, int value ) // Create a card given a type, name, and value
        {
            this.Type = type;
            this.Name = name;
            this.Value = value;
        }

        public Card( string name, CardType type, int value, PropertyType color, PropertyType altColor, string uriPath ) // Create a card given a type, name, and value
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
            this.Color = color;
            this.AltColor = altColor;
            this.CardImageUriPath = uriPath;
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
        }
    }
}
