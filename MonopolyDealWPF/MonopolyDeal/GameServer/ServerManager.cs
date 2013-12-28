using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameObjects;
using Lidgren.Network;

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

        // Game objects.
        static Deck Deck;
        static List<Player> PlayerList;

        [STAThread]
        static void Main( string[] args )
        {
            // Create a new deck.
            Deck = new Deck();
            Deck.Test = "Server Created";

            // Create a new list of players.
            PlayerList = new List<Player>();

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
                        // Data type is all messages manually sent from client
                        // ( Approval is automated process )
                        case NetIncomingMessageType.Data:
                        {
                            Datatype messageType = (Datatype)inc.ReadByte();

                            switch ( messageType )
                            {
                                case Datatype.UpdateDeck:
                                {
                                    Deck = (Deck)ServerUtilities.ReceiveMessage(inc, messageType);
                                    break;
                                }

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

                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdatePlayerList, PlayerList);
                                    }
                                    break;
                                }

                                case Datatype.RequestDeck:
                                {
                                    if ( Server.ConnectionsCount != 0 )
                                    {
                                        ServerUtilities.SendMessage(Server, Datatype.UpdateDeck, Deck);
                                    }

                                    break;
                                }

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
                //System.Threading.Thread.Sleep(1);
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
