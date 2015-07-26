using System;

namespace GameObjects
{
    public class Turn
    {
        public const int INITIAL_ACTION_COUNT = 3;

        public int CurrentTurnOwner { get; set; }

        public int ActionsRemaining { get; set; }

        public Turn( int numberOfPlayers )
        {
            Random randomNumber = new Random();
            CurrentTurnOwner = randomNumber.Next(numberOfPlayers);

            ActionsRemaining = INITIAL_ACTION_COUNT;
        }

        public Turn( int currentTurnOwner, int numberOfActions )
        {
            CurrentTurnOwner = currentTurnOwner;
            ActionsRemaining = numberOfActions;
        }
    }
}