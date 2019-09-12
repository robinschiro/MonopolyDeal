using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameObjects;
using Lidgren.Network;

namespace GameServer
{
    public class ServerUtilities
    {
        public const int PORT_NUMBER = 14242;

        // Receive an update from either a client or the server, depending on where this method is called.
        public static object ReceiveMessage( NetIncomingMessage inc, Datatype messageType )
        {
            switch ( messageType )
            {
                case Datatype.UpdateDeck:
                {
                    return new Deck(ReadCards(inc));
                }

                case Datatype.UpdateDiscardPile:
                {
                    return ReadCards(inc);
                }

                case Datatype.UpdatePlayer:
                {
                    return ReadPlayer(inc);
                }

                case Datatype.UpdatePlayerList:
                {
                    return ReadPlayerList(inc);
                }

                case Datatype.RequestRent:
                {
                    return ReadRentRequest(inc);
                }

                case Datatype.GiveRent:
                {
                    return ReadRentResponse(inc);
                }

                case Datatype.RequestTheft:
                {
                    return ReadTheftRequest(inc);
                }

                case Datatype.ReplyToTheft:
                {
                    return ReadTheftResponse(inc);
                }

                case Datatype.LaunchGame:
                case Datatype.UpdateTurn:
                {
                    return ReadTurn(inc);
                }

                case Datatype.TimeToConnect:
                {
                    return inc.ReadString();
                }

                case Datatype.EndTurn:
                {
                    return ReadTurn(inc);
                }

                case Datatype.PlaySound:
                {
                    return ReadPlaySoundRequest(inc);
                }
            }

            return null;
        }

        public static Turn ReadTurn( NetIncomingMessage inc )
        {
            int currentTurnOwner = inc.ReadInt32();
            int numberOfActions = inc.ReadInt32();

            Turn turn = new Turn(currentTurnOwner, numberOfActions);

            return turn;
        }

        public static Player ReadPlayer( NetIncomingMessage inc )
        {
            // Read the name of the player.
            string name = inc.ReadString();

            // Read the CardsInPlay list.
            List<List<Card>> cardsInPlay = new List<List<Card>>();
            int count = inc.ReadInt32();
            for ( int i = 0; i < count; ++i )
            {
                cardsInPlay.Add(ReadCards(inc));
            }

            // Read the CardsInHand list.
            List<Card> cardsInHand = ReadCards(inc);

            // This first string in the message is the player's name.
            // The first list of cards is the Player's CardsInPlay list.
            // The second list of cards is the Player's CardsInHand list.
            return new Player(name, cardsInPlay, cardsInHand);
        }

        public static List<Player> ReadPlayerList( NetIncomingMessage inc )
        {
            // Read the size of the list of players.
            int size = inc.ReadInt32();

            List<Player> playerList = new List<Player>();

            // Read each name in the message and add it to the list.
            for ( int i = 0; i < size; ++i )
            {
                playerList.Add(ReadPlayer(inc));
            }

            return playerList;
        }

        // Read in a list of cards from an incoming message.
        public static List<Card> ReadCards( NetIncomingMessage inc )
        {
            // Read the size of the list of cards.
            int size = inc.ReadInt32();

            List<Card> cards = new List<Card>();

            // Read the values of the properties of each card.
            for ( int i = 0; i < size; ++i )
            {
                string name = inc.ReadString();
                CardType type = (CardType)inc.ReadByte();
                string value = inc.ReadString();
                PropertyType color = (PropertyType)inc.ReadByte();
                PropertyType altColor = (PropertyType)inc.ReadByte();
                string uriPath = inc.ReadString();
                string soundUriPath = inc.ReadString();
                string actionID = inc.ReadString();
                string cardID = inc.ReadString();
                bool isFlipped = inc.ReadBoolean();
                cards.Add(new Card(name, type, Convert.ToInt32(value), color, altColor, uriPath, soundUriPath, Convert.ToInt32(actionID), Convert.ToInt32(cardID), isFlipped));
            }

            return cards;
        }

        // Parse the information from the rent request.
        public static ActionData.RentRequest ReadRentRequest( NetIncomingMessage inc )
        {
            string renterName = inc.ReadString();
            List<Player> rentees = ReadPlayerList(inc);
            int rentAmount = Convert.ToInt32(inc.ReadString());
            bool isDoubled = inc.ReadBoolean();

            return new ActionData.RentRequest(renterName, rentees, rentAmount, isDoubled);
        }

        // Parse the information from the rent response.
        public static ActionData.RentResponse ReadRentResponse( NetIncomingMessage inc )
        {
            string renterName = inc.ReadString();
            string renteeName = inc.ReadString();
            List<Card> assetsGiven = ReadCards(inc);
            bool acceptedDeal = inc.ReadBoolean();

            return new ActionData.RentResponse(renterName, renteeName, assetsGiven, acceptedDeal);
        }

        // Parse the information from the theft request.
        public static ActionData.TheftRequest ReadTheftRequest( NetIncomingMessage inc )
        {
            string thiefName = inc.ReadString();
            string victimName = inc.ReadString();
            string actionID = inc.ReadString();
            Card propertyToGive = ReadCards(inc)[0];
            List<Card> propertiesToTake = ReadCards(inc);

            return new ActionData.TheftRequest(thiefName, victimName, Convert.ToInt32(actionID), propertyToGive, propertiesToTake);
        }

        // Parse the information from the theft response.
        public static ActionData.TheftResponse ReadTheftResponse( NetIncomingMessage inc )
        {
            string thiefName = inc.ReadString();
            string victimName = inc.ReadString();
            bool answer = inc.ReadBoolean();

            return new ActionData.TheftResponse(thiefName, victimName, answer);
        }

        public static string ReadPlaySoundRequest( NetIncomingMessage inc )
        {
            return inc.ReadString();
        }

        public static void WriteCards( NetOutgoingMessage outmsg, List<Card> cardList )
        {
            if ( cardList != null )
            {
                // Write the count of the list of cards.
                outmsg.Write(cardList.Count);

                // Write the properties of each card.
                foreach ( Card card in cardList )
                {
                    outmsg.Write(card.Name);
                    outmsg.Write((byte)card.Type);
                    outmsg.Write(card.Value.ToString());
                    outmsg.Write((byte)card.Color);
                    outmsg.Write((byte)card.AltColor);
                    outmsg.Write(card.CardImageUriPath);
                    outmsg.Write(card.CardSoundUriPath);
                    outmsg.Write(card.ActionID.ToString());
                    outmsg.Write(card.CardID.ToString());
                    outmsg.Write(card.IsFlipped);
                }
            }
        }

        // Write a rent request. 
        public static void WriteRentRequest( NetOutgoingMessage outmsg, ActionData.RentRequest request)
        {
            outmsg.Write(request.RenterName);
            WritePlayerList(outmsg, request.Rentees);
            outmsg.Write(request.RentAmount.ToString());
            outmsg.Write(request.IsDoubled);
        }

        // Write a rent response. 
        public static void WriteRentResponse( NetOutgoingMessage outmsg, ActionData.RentResponse request )
        {
            outmsg.Write(request.RenterName);
            outmsg.Write(request.RenteeName);
            WriteCards(outmsg, request.AssetsGiven);
            outmsg.Write(request.AcceptedDeal);
        }

        // Write a theft request. 
        public static void WriteTheftRequest( NetOutgoingMessage outmsg, ActionData.TheftRequest request )
        {
            outmsg.Write(request.ThiefName);
            outmsg.Write(request.VictimName);
            outmsg.Write(request.ActionID.ToString());
            WriteCards(outmsg, new List<Card>() { request.PropertyToGive });
            WriteCards(outmsg, request.PropertiesToTake);
        }

        // Write a theft response. 
        public static void WriteTheftResponse( NetOutgoingMessage outmsg, ActionData.TheftResponse request )
        {
            outmsg.Write(request.ThiefName);
            outmsg.Write(request.VictimName);
            outmsg.Write(request.AcceptedDeal);
        }

        public static void WritePlayer( NetOutgoingMessage outmsg, Player player )
        {
            // Write the Player's name.
            outmsg.Write(player.Name);
            
            // Write the Player's CardsInPlay (which is a list of card lists).
            outmsg.Write(player.CardsInPlay.Count);

            foreach ( List<Card> cardList in player.CardsInPlay )
            {
                WriteCards(outmsg, cardList);
            }

            // Write the Player's CardsInHand.
            WriteCards(outmsg, player.CardsInHand);
        }

        public static void WritePlayerList( NetOutgoingMessage outmsg, List<Player> playerList )
        {
            // Write the count of players.
            outmsg.Write(playerList.Count);

            // Write the name of each player in the game.
            foreach ( Player player in playerList )
            {
                WritePlayer(outmsg, player);
            }
        }

        public static void WriteTurn( NetOutgoingMessage outmsg, Turn turn )
        {
            // Write the data related to the current turn.
            outmsg.Write(turn.CurrentTurnOwner);
            outmsg.Write(turn.ActionsRemaining);
        }

        public static void WritePlaySoundRequest( NetOutgoingMessage outmsg, string uriPath )
        {
            outmsg.Write(uriPath);
        }

        // Send an update to either a client or the server, depending on where this method is called.
        public static void SendMessage( NetPeer netPeer, Datatype messageType, object messageData = null, long idOfClientToExclude = -1 )
        {
            NetOutgoingMessage outmsg;

            // Create new message
            if ( netPeer is NetServer )
            {
                outmsg = (netPeer as NetServer).CreateMessage();
            }
            else
            {
                outmsg = (netPeer as NetClient).CreateMessage();
            }

            // Write the type of the message that is being sent.
            outmsg.Write((byte)messageType);

            if ( messageData != null )
            {
                switch ( messageType )
                {
                    case Datatype.UpdateDeck:
                    {
                        WriteCards(outmsg, (messageData as Deck).CardList);

                        break;
                    }

                    case Datatype.UpdateDiscardPile:
                    {
                        WriteCards(outmsg, (messageData as List<Card>));
                        break;
                    }

                    case Datatype.UpdatePlayer:
                    {
                        WritePlayer(outmsg, (messageData as Player));

                        break;
                    }

                    case Datatype.UpdatePlayerList:
                    {
                        WritePlayerList(outmsg, (messageData as List<Player>));
                        break;
                    }

                    case Datatype.RequestRent:
                    {
                        WriteRentRequest(outmsg, (messageData as ActionData.RentRequest));
                        break;
                    }

                    case Datatype.GiveRent:
                    {
                        WriteRentResponse(outmsg, (messageData as ActionData.RentResponse));
                        break;
                    }

                    case Datatype.RequestTheft:
                    {
                        WriteTheftRequest(outmsg, (messageData as ActionData.TheftRequest));
                        break;
                    }

                    case Datatype.ReplyToTheft:
                    {
                        WriteTheftResponse(outmsg, (messageData as ActionData.TheftResponse));
                        break;
                    }

                    case Datatype.LaunchGame:
                    case Datatype.UpdateTurn:
                    {
                        Turn currentTurn = (Turn)messageData;

                        WriteTurn(outmsg, currentTurn);

                        break;
                    }

                    case Datatype.TimeToConnect:
                    {
                        String playerToConnect = (String)messageData;
                        outmsg.Write(playerToConnect);

                        break;
                    }

                    case Datatype.EndTurn:
                    {
                        Turn currentTurn = (Turn)messageData;

                        WriteTurn(outmsg, currentTurn);

                        break;
                    }

                    case Datatype.PlaySound:
                    {
                        WritePlaySoundRequest(outmsg, messageData as string);

                        break;
                    }
                }
            }

            // Send it to the server or the clients.
            if ( netPeer is NetServer )
            {
                NetServer server = netPeer as NetServer;
                List<NetConnection> connectionsForFanout = server.Connections.Where(connection => connection.RemoteUniqueIdentifier != idOfClientToExclude).ToList();

                if ( connectionsForFanout.Count > 0 )
                {
                    server.SendMessage(outmsg, connectionsForFanout, NetDeliveryMethod.ReliableOrdered, 0);
                }
            }
            else
            {
                (netPeer as NetClient).SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
            }
        }
    }
}
