using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MonopolyDeal
{
    public class Card
    {
        public enum cardType { money, property, action }; // Creates enum for different card types
        public cardType type;
        String name;
        int value;
        String textureName;
        
        public Card(cardType type, String name, int value, String textureName) // Create a card given a type, name, and value
        {
            this.type = type;
            this.name = name;
            this.value = value;
            this.textureName = textureName;
        }

        //Standard card for testing purposes
        public Card(int value) // Takes in value to differentiate between cards
        {
            this.type = cardType.action;
            this.name = "Test Card";
            this.value = value;
        }


        // Allows other code to read a card's properties
        public cardType getType()
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
