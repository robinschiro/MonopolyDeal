using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameObjects
{
    public enum Datatype
    {
        LaunchGame,
        EndTurn,
        UpdateTurn,
        UpdateDeck,
        UpdateDiscardPile,
        UpdatePlayer,
        UpdatePlayerList,
        RequestDeck,
        RequestPlayerList,
        RequestRent,
        GiveRent,
        RequestTheft,
        ReplyToTheft
    }
}
