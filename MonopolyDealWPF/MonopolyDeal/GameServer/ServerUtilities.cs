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
        // Receive an update from either a client or the server, depending on where this method is called.
        public static object ReceiveUpdate( NetIncomingMessage inc, Datatype messageType )
        {
            switch ( messageType )
            {
                case Datatype.UpdateDeck:
                {
                    return new Deck(ReadCards(inc));
                }

                case Datatype.UpdateSelectedCard:
                {
                    // Return the current value of 'SelectedCard'.
                    return inc.ReadInt32();
                }

                case Datatype.UpdatePlayer:
                {
                    // This first string in the message is the player's name.
                    // The first list of cards is the Player's CardsInPlay list.
                    // The second list of cards is the Player's CardsInHand list.
                    return new Player(inc.ReadString(), ReadCards(inc), ReadCards(inc));
                }

                case Datatype.UpdatePlayerNames:
                {
                    // Read the size of the list of names.
                    int size = inc.ReadInt32();

                    List<string> playerNames = new List<string>();

                    // Read each name in the message and add it to the list.
                    for ( int i = 0; i < size; ++i )
                    {
                        playerNames.Add(inc.ReadString());
                    }

                    return playerNames;
                }

                case Datatype.UpdatePlayerList:
                {
                    // Read the size of the list of players.
                    int size = inc.ReadInt32();

                    List<Player> playerList = new List<Player>();

                    // Read each name in the message and add it to the list.
                    for ( int i = 0; i < size; ++i )
                    {
                        playerList.Add(new Player(inc.ReadString(), ReadCards(inc), ReadCards(inc)));
                    }

                    return playerList;
                }
            }

            return null;
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
                string value = "";
                string path = "";
                inc.ReadString(out value);
                inc.ReadString(out path);
                cards.Add(new Card(Convert.ToInt32(value), path));
            }

            return cards;
        }

        public static void WritePlayer( NetOutgoingMessage outmsg, Player player )
        {
            // Write the player's name.
            outmsg.Write(player.Name);

            // Write the count of the Player's CardsInPlay.
            outmsg.Write(player.CardsInPlay.Count);

            // Write the properties of each card.
            foreach ( Card card in player.CardsInPlay )
            {
                outmsg.Write(card.Value.ToString());
                outmsg.Write(((BitmapImage)card.CardImage.Source).UriSource.OriginalString);
            }

            // Write the count of the Player's CardsInHand.
            outmsg.Write(player.CardsInHand.Count);

            // Write the properties of each card.
            foreach ( Card card in player.CardsInHand )
            {
                outmsg.Write(card.Value.ToString());
                outmsg.Write(((BitmapImage)card.CardImage.Source).UriSource.OriginalString);
            }
        }

        // Send an update to either a client or the server, depending on where this method is called.
        public static void SendUpdate( NetPeer netPeer, Datatype messageType, object updatedObject )
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

            switch ( messageType )
            {
                case Datatype.UpdateDeck:
                {
                    outmsg.Write((updatedObject as Deck).CardList.Count);

                    // Write the properties of each card in the deck.
                    foreach ( Card card in (updatedObject as Deck).CardList )
                    {
                        outmsg.Write(card.Value.ToString());
                        outmsg.Write(((BitmapImage)card.CardImage.Source).UriSource.OriginalString);
                    }

                    break;
                }

                case Datatype.UpdateSelectedCard:
                {
                    // Write the value of the variable 'selectedCard'.
                    outmsg.Write((int)updatedObject);

                    break;
                }

                case Datatype.UpdatePlayer:
                {
                    Player player = (Player)updatedObject;

                    // Write the player's name.
                    outmsg.Write(player.Name);

                    // Write the count of the Player's CardsInPlay.
                    outmsg.Write(player.CardsInPlay.Count);

                    // Write the properties of each card.
                    foreach ( Card card in player.CardsInPlay )
                    {
                        outmsg.Write(card.Value.ToString());
                        outmsg.Write(((BitmapImage)card.CardImage.Source).UriSource.OriginalString);
                    }

                    // Write the count of the Player's CardsInHand.
                    outmsg.Write(player.CardsInHand.Count);

                    // Write the properties of each card.
                    foreach ( Card card in player.CardsInHand )
                    {
                        outmsg.Write(card.Value.ToString());
                        outmsg.Write(((BitmapImage)card.CardImage.Source).UriSource.OriginalString);
                    }

                    break;
                }

                case Datatype.UpdatePlayerNames:
                {
                    List<Player> players = (List<Player>)updatedObject;

                    // Write the count of playerNames.
                    outmsg.Write(players.Count);

                    // Write the name of each player in the game.
                    foreach ( Player player in players )
                    {
                        outmsg.Write(player.Name);
                    }
                    break;
                }

                case Datatype.UpdatePlayerList:
                {
                    List<Player> players = (List<Player>)updatedObject;

                    // Write the count of players.
                    outmsg.Write(players.Count);

                    // Write the name of each player in the game.
                    foreach ( Player player in players )
                    {
                        WritePlayer(outmsg, player);
                    }
                    break;
                }

                case Datatype.FirstMessage:
                {
                    // Only the type of the message is necessary for this case. That is why there is no code here.
                    break;
                }
            }

            // Send it to the server or a client.
            if ( netPeer is NetServer )
            {
                NetServer server = netPeer as NetServer;

                if ( server.Connections.Count > 0 )
                {
                    server.SendMessage(outmsg, server.Connections, NetDeliveryMethod.ReliableOrdered, 0);
                }
            }
            else
            {
                (netPeer as NetClient).SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
            }
        }
    }
}
