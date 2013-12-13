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
                    // Read the size of the cardlist of the deck.
                    int size = inc.ReadInt32();

                    List<Card> tempCardList = new List<Card>();
                    Deck oldDeck = new Deck();
                    for (int i = 0; i < size; ++i)
                    {
                        string value = "";
                        string path = "";
                        inc.ReadString(out value);
                        inc.ReadString(out path);
                        tempCardList.Add(new Card(Convert.ToInt32(value), path));
                    }
                    oldDeck.CardList = tempCardList;

                    return oldDeck;
                }

                case Datatype.UpdateSelectedCard:
                {
                    // Return the current value of 'SelectedCard'.
                    return inc.ReadInt32();
                }
            }

            return null;
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

            switch ( messageType )
            {
                case Datatype.UpdateDeck:
                {
                    // Write the type of the message that is being sent.
                    outmsg.Write((byte)Datatype.UpdateDeck);

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
                    // Write the type of the message that is being sent.
                    outmsg.Write((byte)Datatype.UpdateSelectedCard);

                    // Write the value of the variable 'selectedCard'.
                    outmsg.Write((int)updatedObject);

                    break;
                }

                case Datatype.FirstMessage:
                {
                    // Write the type of the message that is being sent.
                    outmsg.Write((byte)Datatype.FirstMessage);
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
