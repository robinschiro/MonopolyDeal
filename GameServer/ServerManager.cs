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
using System.IO;
using System.Windows;
using System.Windows.Resources;
using System.IO.Packaging;

// The server/client architecture of MonopolyDeal is built on the Lidgren Networking framework.
// Lidgren Networking Resources:
// Download LidgreNetwork at: http://code.google.com/p/lidgren-network-gen3/
// Blog: http://xnacoding.blogspot.com/
namespace GameServer
{
    public enum PacketTypes
    {
        LOGIN,
    }

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
        static String PATH_TO_PROFILE = "pack://application:,,,/GameObjects;component/Resources/Profile.txt";

        [STAThread]
        static void Main( string[] args )
        {
            // Attempt to load the game configuration from a config file.
            if ( File.Exists("Profile.txt"))
            {
                Profile = new tvProfile("Profile.txt", false);
            }
            // If it doesn't exist locally, use the embedded one.
            else
            {
                // Ensure that the pack scheme is known: http://stackoverflow.com/questions/6005398/uriformatexception-invalid-uri-invalid-port-specified
                string ensurePackSchemeIsKnown = PackUriHelper.UriSchemePack;

                StreamResourceInfo stream = Application.GetResourceStream(new Uri(PATH_TO_PROFILE, UriKind.Absolute));
                StreamReader reader = new StreamReader(stream.Stream);
                Profile = new tvProfile(reader.ReadToEnd());
            }

            // Create a new deck.
            Deck = new Deck(Profile);

            // Create a new list of players.
            PlayerList = new List<Player>();

            // Create a new Dicard Pile.
            DiscardPile = new List<Card>();

            // Create new instance of configs. Parameter is "application Id". It has to be same on client and server.
            Config = new NetPeerConfiguration("game");

            // Set server port
            Config.Port = ServerUtilities.PORT_NUMBER;

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
                                Console.WriteLine("Approved new connection.");
                            }

                            break;
                        }
                        // All messages manually sent from clients are considered "Data" messages.
                        // ( Approval is an automated process )
                        case NetIncomingMessageType.Data:
                        {
                            Datatype messageType = (Datatype)inc.ReadByte();
                            
                            // Get all connections excluding the sender connection.
                            long idOfSender = inc.SenderConnection.RemoteUniqueIdentifier;
                            List<NetConnection> connectionsForFanout = Server.Connections.Where(connection => connection.RemoteUniqueIdentifier != idOfSender).ToList();

                            switch ( messageType )
                            {
                                // Receive an updated Deck from a client.
                                case Datatype.UpdateDeck:
                                {
                                    Deck = (Deck)ServerUtilities.ReceiveMessage(inc, messageType);
                                    ServerUtilities.SendMessage(Server, Datatype.UpdateDeck, Deck);
                                    
                                    break;
                                }

                                // Receive an updated DiscardPile from a client.
                                case Datatype.UpdateDiscardPile:
                                {
                                    DiscardPile = (List<Card>)ServerUtilities.ReceiveMessage(inc, messageType);
                                    ServerUtilities.SendMessage(Server, Datatype.UpdateDiscardPile, DiscardPile);

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
                                    ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    
                                    break;
                                }

                                // Update the server's Player List and send it to the clients.
                                case Datatype.UpdatePlayerList:
                                {
                                    PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);
                                    ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);

                                    break;
                                }

                                // Send the updated turn to all players.
                                case Datatype.UpdateTurn:
                                {
                                    Turn = (Turn)ServerUtilities.ReceiveMessage(inc, messageType);
                                    ServerUtilities.SendMessage(Server, Datatype.UpdateTurn, Turn);

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

                                    // Generate the Turn object to keep track of the current turn.
                                    Turn = new Turn(PlayerList.Count);

                                    // Tell all clients to launch the game and send them the Turn object.
                                    ServerUtilities.SendMessage(Server, Datatype.LaunchGame, Turn);                                   

                                    break;
                                }

                                case Datatype.TimeToConnect:
                                {
                                    string playerToConnect = (String)ServerUtilities.ReceiveMessage(inc, messageType);

                                    // Broadcast a message that tells a specific client to launch the game.
                                    ServerUtilities.SendMessage(Server, Datatype.TimeToConnect, playerToConnect);                                    

                                    break;
                                }

                                // Send the rent request to all clients.
                                case Datatype.RequestRent:
                                {
                                    ActionData.RentRequest request = (ActionData.RentRequest)ServerUtilities.ReadRentRequest(inc);
                                    ServerUtilities.SendMessage(Server, Datatype.RequestRent, request);

                                    break;
                                }

                                // Send the rent response to all clients.
                                case Datatype.GiveRent:
                                {
                                    ActionData.RentResponse response = (ActionData.RentResponse)ServerUtilities.ReadRentResponse(inc);
                                    ServerUtilities.SendMessage(Server, Datatype.GiveRent, response);

                                    break;
                                }

                                // Send the theft request to all clients.
                                case Datatype.RequestTheft:
                                {
                                    ActionData.TheftRequest request = (ActionData.TheftRequest)ServerUtilities.ReadTheftRequest(inc);
                                    ServerUtilities.SendMessage(Server, Datatype.RequestTheft, request);

                                    break;
                                }

                                // Send the theft response to all clients.
                                case Datatype.ReplyToTheft:
                                {
                                    ActionData.TheftResponse response = (ActionData.TheftResponse)ServerUtilities.ReadTheftResponse(inc);
                                    ServerUtilities.SendMessage(Server, Datatype.ReplyToTheft, response);

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
                                    ServerUtilities.SendMessage(Server, Datatype.UpdateDeck, Deck);
                                    break;
                                }

                                // Send the server's PlayerList to all clients.
                                case Datatype.RequestPlayerList:
                                {
                                    ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
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
                                Console.WriteLine("Client was disconnected.");
                            }
                            break;
                        }
                        default:
                        {
                            Console.WriteLine("Message of type: " + inc.MessageType + " received");
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
}