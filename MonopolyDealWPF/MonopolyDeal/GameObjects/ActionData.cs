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
            public List<Card> AssetsGiven;

            public RentResponse( string renterName, List<Card> assetsGiven )
            {
                this.RenterName = renterName;
                this.AssetsGiven = assetsGiven;
            }
        }
    }
}
