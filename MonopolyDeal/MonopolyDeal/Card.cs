using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace MonopolyDeal
{
    class Card
    {
        public enum Type { money, property, action }; // Creates enum for different card types
        public Type type;
        String name;
        int value;
        
        
        public Card(Type type, String name, int value) // Create a card given a type, name, and value
        {
            this.type = type;
            this.name = name;
            this.value = value;
        }


        // Allows other code to read a card's properties
        public Type getType()
        {
            return this.type;
        }
        
        public String getName()
        {
            return this.name;
        }

        public int getValue()
        {
            return this.value;
        }
    }
}
