using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;


namespace GameObjects
{
    public class Card
    {
        public enum cardType { money, property, action }; // Creates enum for different card types
        public cardType Type { get; set; }
        public String Name { get; set; }
        public int Value { get; set; }
        public String TextureName { get; set; }
        public Image CardImage { get; set; }

        public Card( cardType type, String name, int value, String textureName ) // Create a card given a type, name, and value
        {
            this.Type = type;
            this.Name = name;
            this.Value = value;
            this.TextureName = textureName;
        }

        //Standard card for testing purposes
        public Card( int value ) // Takes in value to differentiate between cards
        {
            this.Type = cardType.action;
            this.Name = "Test Card";
            this.Value = value;
        }

        // Create a card from a string representing the file path to the card's image.
        public Card( int value, String path )
        {
            this.Value = value;
            this.CardImage = new Image();
            this.CardImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
        }
    }
}
