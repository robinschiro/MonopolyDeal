using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameObjects
{
    public class Turn
    {
        public int CurrentTurnOwner { get; set; }
        public int NumberOfActions { get; set; }
        
        public Turn( int numberOfPlayers )
        {
            Random randomNumber = new Random();
            CurrentTurnOwner = randomNumber.Next(numberOfPlayers);

            NumberOfActions = 0;
        }

        public Turn( int currentTurnOwner, int numberOfActions )
        {
            CurrentTurnOwner = currentTurnOwner;
            NumberOfActions = numberOfActions;
        }
    }
}
