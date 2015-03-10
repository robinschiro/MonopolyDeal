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
        ReceiveRentRequest,
        RequestDeck,
        RequestPlayerList,
        RequestRent

    }
}
