using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameObjects
{
    public class ActionData
    {
        // The request should contain the name of the player collecting the rent, the list of players who must pay the rent,
        // the amount of the rent, and a bool that states whether the rent is doubled.
        public class RentRequest
        {
            public string RenterName;
            public List<Player> Rentees;
            public int RentAmount;
            public bool IsDoubled;

            public RentRequest( string renterName, List<Player> rentees, int rentAmount, bool isDoubled )
            {
                this.RenterName = renterName;
                this.Rentees = rentees;
                this.RentAmount = rentAmount;
                this.IsDoubled = isDoubled;
            }
        }

        // The response should contain the name of the player collecting the rent and the list of assets given.
        public class RentResponse
        {
            public string RenterName;
            public string RenteeName;
            public List<Card> AssetsGiven;
            public bool AcceptedDeal;

            public RentResponse( string renterName, string renteeName, List<Card> assetsGiven, bool acceptedDeal )
            {
                this.RenterName = renterName;
                this.RenteeName = renteeName;
                this.AssetsGiven = assetsGiven;
                this.AcceptedDeal = acceptedDeal;
            }
        }

        // The request should contain the name of the thief, the name of the victim, the property the thief is giving,
        // and the list of properties that the thief wants to take.
        public class TheftRequest
        {
            public string ThiefName;
            public string VictimName;
            public int ActionID;
            public Card PropertyToGive;
            public List<Card> PropertiesToTake;

            public TheftRequest( string thiefName, string victimName, int actionID, Card propertyToGive, List<Card> propertyToTake )
            {
                this.ThiefName = thiefName;
                this.VictimName = victimName;
                this.ActionID = actionID;
                this.PropertyToGive = propertyToGive;
                this.PropertiesToTake = propertyToTake;
            }
        }

        // The response should contain the name of the thief, the name of the victim, and the victim's answer (true if allowing the theft,
        // false if used "Just Say No".
        public class TheftResponse
        {
            public string ThiefName;
            public string VictimName;
            public bool AcceptedDeal;

            public TheftResponse( string thiefName, string victimName, bool acceptedDeal )
            {
                this.ThiefName = thiefName;
                this.VictimName = victimName;
                this.AcceptedDeal = acceptedDeal;
            }
        }
    }
}
