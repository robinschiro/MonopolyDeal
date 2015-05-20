using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameObjects;
using tvToolbox;
using Lidgren.Network;
using Utilities;


// Lidgren Network example
// Made by: Riku Koskinen
// http://xnacoding.blogspot.com/
// Download LidgreNetwork at: http://code.google.com/p/lidgren-network-gen3/
//
// You can use this code in anyway you want
// Code is not perfect, but it works
// It's example of console based game, where new players can join and move
// Movement is updated to all clients.


// THIS IS VERY VERY VERY BASIC EXAMPLE OF NETWORKING IN GAMES
// NO PREDICTION, NO LAG COMPENSATION OF ANYKIND

namespace GameServer
{
    public class ServerManager
    {
        // Server object
        static NetServer Server;
        // Configuration object
        static NetPeerConfiguration Config;
        static tvProfile Profile;

        // Game objects.
        static Deck Deck;
        static List<Player> PlayerList;
        static Turn Turn;
        static List<Card> DiscardPile;

        [STAThread]
        static void Main( string[] args )
        {
            // Generate a Profile object. When this project is near the end of its development, 
            // we will need to remove the leading "..\\"'s from this file path.
            Profile = new tvProfile("..\\..\\..\\Profile.txt", false);

            // Create a new deck.
            Deck = new Deck(Profile);

            // Create a new list of players.
            PlayerList = new List<Player>();

            // Create a new Dicard Pile.
            DiscardPile = new List<Card>();

            // Create new instance of configs. Parameter is "application Id". It has to be same on client and server.
            Config = new NetPeerConfiguration("game");

            // Set server port
            Config.Port = 14242;

            // Max client amount
            Config.MaximumConnections = 200;

            // Enable New messagetype. Explained later
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            // Create new server based on the configs just defined
            Server = new NetServer(Config);

            // Start it
            Server.Start();

            // Eh..
            Console.WriteLine("Server Started");

            // Object that can be used to store and read messages
            NetIncomingMessage inc;

            // Check time
            DateTime time = DateTime.Now;

            // Create timespan of 30ms
            TimeSpan timetopass = new TimeSpan(0, 0, 0, 0, 30);

            // Write to con..
            Console.WriteLine("Waiting for new connections");

            // Main loop
            // This kind of loop can't be made in XNA. In there, its basically same, but without while
            // Or maybe it could be while(new messages)
            while ( true )
            {
                // Server.ReadMessage() Returns new messages, that have not yet been read.
                // If "inc" is null -> ReadMessage returned null -> Its null, so dont do this :)
                if ( (inc = Server.ReadMessage()) != null )
                {
                    // Theres few different types of messages. To simplify this process, i left only 2 of em here
                    switch ( inc.MessageType )
                    {
                        // If incoming message is Request for connection approval
                        // This is the very first packet/message that is sent from client
                        // Here you can do new player initialisation stuff
                        case NetIncomingMessageType.ConnectionApproval:
                        {
                            // Read the first byte of the packet
                            // ( Enums can be casted to bytes, so it be used to make bytes human readable )
                            if ( inc.ReadByte() == (byte)PacketTypes.LOGIN )
                            {
                                Console.WriteLine("Incoming LOGIN");

                                // Approve clients connection ( Its sort of agreenment. "You can be my client and i will host you" )
                                inc.SenderConnection.Approve();

                                // Debug
                                Console.WriteLine("Approved new connection and updated the world status");
                            }

                            break;
                        }
                        // All messages manually sent from clients are considered "Data" messages.
                        // ( Approval is an automated process )
                        case NetIncomingMessageType.Data:
                        {
                            Datatype messageType = (Datatype)inc.ReadByte();

                            switch ( messageType )
                            {
                                // Receive an updated Deck from a client.
                                case Datatype.UpdateDeck:
                                {
                                    Deck = (Deck)ServerUtilities.ReceiveMessage(inc, messageType);
                                    break;
                                }

                                // Receive an updated DiscardPile from a client.
                                case Datatype.UpdateDiscardPile:
                                {
                                    DiscardPile = (List<Card>)ServerUtilities.ReceiveMessage(inc, messageType);

                                    // Send the updated DiscardPile to all clients.
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdateDiscardPile, DiscardPile);
                                    }

                                    break;
                                }

                                // Add or modify a player in the PlayerList.
                                case Datatype.UpdatePlayer:
                                {
                                    Player updatedPlayer = (Player)ServerUtilities.ReceiveMessage(inc, messageType);
                                    bool isPlayerInList = false;

                                    // If the updated Player is already in the server's list, update that's Player's properties.
                                    // Note: This search only works if players have unique names.
                                    foreach ( Player player in PlayerList )
                                    {
                                        if ( updatedPlayer.Name == player.Name )
                                        {
                                            player.CardsInHand = updatedPlayer.CardsInHand;
                                            player.CardsInPlay = updatedPlayer.CardsInPlay;
                                            isPlayerInList = true;
                                            break;
                                        }
                                    }

                                    // If the Player is not on the list, add it.
                                    if ( !isPlayerInList )
                                    {
                                        PlayerList.Add(updatedPlayer);
                                    }

                                    // Send the updated PlayerList to all clients.
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    }
                                    break;
                                }

                                // Update the server's Player List and send it to the clients.
                                case Datatype.UpdatePlayerList:
                                {
                                    PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    }

                                    break;
                                }

                                // Set up the players for the game. This case should be hit only when a client launches the game.
                                case Datatype.LaunchGame:
                                {
                                    // Deal the initial hands to the players.
                                    for ( int i = 0; i < PlayerList.Count; ++i )
                                    {
                                        PlayerList[i] = new Player(Deck, PlayerList[i].Name);
                                    }

                                    // Send the Player List to the clients.
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    }

                                    // Generate the Turn object to keep track of the current turn.
                                    Turn = new Turn(PlayerList.Count);

                                    // Tell all clients to launch the game and send them the Turn object.
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.LaunchGame, Turn);
                                    }

                                    break;
                                }

                                // Send the rent request to all clients.
                                case Datatype.RequestRent:
                                {
                                    ActionData.RentRequest request = (ActionData.RentRequest)ServerUtilities.ReadRentRequest(inc);

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.RequestRent, request);
                                    }

                                    break;
                                }

                                // Send the rent response to all clients.
                                case Datatype.GiveRent:
                                {
                                    ActionData.RentResponse response = (ActionData.RentResponse)ServerUtilities.ReadRentResponse(inc);

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.GiveRent, response);
                                    }

                                    break;
                                }

                                // Send the theft request to all clients.
                                case Datatype.RequestTheft:
                                {
                                    ActionData.TheftRequest request = (ActionData.TheftRequest)ServerUtilities.ReadTheftRequest(inc);

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.RequestTheft, request);
                                    }

                                    break;
                                }

                                // Send the theft response to all clients.
                                case Datatype.ReplyToTheft:
                                {
                                    ActionData.TheftResponse response = (ActionData.TheftResponse)ServerUtilities.ReadTheftResponse(inc);

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.ReplyToTheft, response);
                                    }

                                    break;
                                }

                                case Datatype.EndTurn:
                                {
                                    Turn = (Turn)ServerUtilities.ReceiveMessage(inc, Datatype.EndTurn);

                                    // Send the updated Turn object to the clients.
                                    ServerUtilities.SendMessage(Server, Datatype.EndTurn, Turn);

                                    break;
                                }

                                // Send the server's Deck to all clients.
                                case Datatype.RequestDeck:
                                {
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdateDeck, Deck);
                                    }

                                    break;
                                }

                                // Send the server's PlayerList to all clients.
                                case Datatype.RequestPlayerList:
                                {
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    }

                                    break;
                                }                                    
                            }

                            break;
                        }
                        case NetIncomingMessageType.StatusChanged:
                        {
                            // In case status changed
                            // It can be one of these
                            // NetConnectionStatus.Connected;
                            // NetConnectionStatus.Connecting;
                            // NetConnectionStatus.Disconnected;
                            // NetConnectionStatus.Disconnecting;
                            // NetConnectionStatus.None;

                            // NOTE: Disconnecting and Disconnected are not instant unless client is shutdown with disconnect()
                            Console.WriteLine(inc.SenderConnection.ToString() + " status changed. " + (NetConnectionStatus)inc.SenderConnection.Status);
                            if ( inc.SenderConnection.Status == NetConnectionStatus.Disconnected || inc.SenderConnection.Status == NetConnectionStatus.Disconnecting )
                            {

                            }
                            break;
                        }
                        default:
                        {
                            // As i statet previously, theres few other kind of messages also, but i dont cover those in this example
                            // Uncommenting next line, informs you, when ever some other kind of message is received
                            //Console.WriteLine("Not Important Message");
                            break;
                        }
                    }
                } // If New messages

                // if 30ms has passed
                if ( (time + timetopass) < DateTime.Now )
                {
                    time = DateTime.Now;
                }

                // While loops run as fast as your computer lets. While(true) can lock your computer up. Even 1ms sleep, lets other programs have piece of your CPU time
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    // This is good way to handle different kind of packets
    // there has to be some way, to detect, what kind of packet/message is incoming.
    // With this, you can read message in correct order ( ie. Can't read int, if its string etc )

    // Best thing about this method is, that even if you change the order of the entrys in enum, the system won't break up
    // Enum can be casted ( converted ) to byte
    public enum PacketTypes
    {
        LOGIN,
    }
}
