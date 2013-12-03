using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GameServer;
using GameObjects;
using Lidgren.Network;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window
    {
        private Deck Deck;
        private Player Player;
        private String ServerIP;
        private int SelectedCard;

        // Client Object
        static NetClient Client;

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow(string ipAddress)
        {
            InitializeComponent();

            this.ServerIP = ipAddress;
            this.SelectedCard = -1;

            // Connect the client to the server.
            InitializeClient();

            // Initialize the deck.
            this.Deck = new Deck();

            UpdateServer();
            ReceiveUpdate();

            this.Player = new Player(Deck, "Player");

            // Display the cards in this player's hand.
            for (int i = 0; i < Player.CardsInHand.Count; ++i)
            {
                DisplayCardInHand(Player.CardsInHand, i);
            }
        }

        private void InitializeClient()
        {
            // Create new instance of configs. Parameter is "application Id". It has to be same on client and server.
            NetPeerConfiguration Config = new NetPeerConfiguration("game");

            // Create new client, with previously created configs
            Client = new NetClient(Config);

            // Create new outgoing message
            NetOutgoingMessage outmsg = Client.CreateMessage();

            // Start client
            Client.Start();

            // Write byte ( first byte informs server about the message type ) ( This way we know, what kind of variables to read )
            outmsg.Write((byte)PacketTypes.LOGIN);

            // Write String "Name" . Not used, but just showing how to do it
            outmsg.Write("MyName");

            // Connect client, to ip previously requested from user.
            Client.Connect(ServerIP, 14242, outmsg);
        }

        // Display a card in a player's hand.
        // Right now, this method only works for player one's hand.
        public void DisplayCardInHand(List<Card> cardsInHand, int position)
        {
            Button cardButton = new Button();
            cardButton.Name = "CardButton" + position;
            cardButton.Content = cardsInHand[position].CardImage;
            cardButton.Style = (Style)FindResource("NoChromeButton");
            cardButton.Click +=new RoutedEventHandler(cardButton_Click);

            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition col2 = new ColumnDefinition();
            col2.Width = new GridLength(16, GridUnitType.Star);
            ColumnDefinition col3 = new ColumnDefinition();
            col3.Width = new GridLength(1, GridUnitType.Star);

            Grid cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(col1);
            cardGrid.ColumnDefinitions.Add(col2);
            cardGrid.ColumnDefinitions.Add(col3);
            cardGrid.Children.Add(cardButton);
            Grid.SetColumn(cardButton, 1);

            PlayerOneHand.Children.Add(cardGrid);
            Grid.SetColumn(cardGrid, position);
        }

        // Increase the size of a card when it is selected and decrease its size when another card is selected.
        public void cardButton_Click(object sender, RoutedEventArgs args)
        {
            // Deselect the currently selected card.
            DeselectCard(FindButton(SelectedCard));

            // Update the value of SelectedCard.
            for (int i = 0; i < PlayerOneHand.Children.Count; ++i)
            {
                foreach (FrameworkElement element in (PlayerOneHand.Children[i] as Grid).Children)
                {
                    if (element.Name == (sender as Button).Name)
                    {
                        if (i != SelectedCard)
                        {
                            SelectedCard = i;
                            SelectCard(sender as Button);
                        }
                        else
                        {
                            SelectedCard = -1;
                            DeselectCard(sender as Button);
                        }
                        break;
                    }
                }
            }
        }

        // Find a card in player one's hand given its position in the grid displaying the hand.
        private Button FindButton(int buttonIndex)
        {
            if (SelectedCard != -1)
            {
                for (int i = 0; i < PlayerOneHand.Children.Count; ++i)
                {
                    foreach (FrameworkElement element in (PlayerOneHand.Children[i] as Grid).Children)
                    {
                        if (element.Name == "CardButton" + buttonIndex)
                        {
                            return (Button)element;
                        }
                    }
                }
            }
            return null;
        }

        // Increase the size of the currently selected card.
        private void SelectCard( Button cardButton )
        {
            if (cardButton != null)
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1.2;
                myScaleTransform.ScaleX = 1.2;

                cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                cardButton.RenderTransform = myScaleTransform;
            }
        }

        // Set the currently selected card to its normal size.
        private void DeselectCard(Button cardButton)
        {
            if (cardButton != null)
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1;
                myScaleTransform.ScaleX = 1;

                cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                cardButton.RenderTransform = myScaleTransform;
            }
        }

        // Again, testing AttachedProperties. Will need to discuss.
        private void setInfoBox(TriggerBase target, int location)
        {
            target.SetValue(InfoBoxAttachedProperty, Player.CardsInHand[location]);
        }

        // Receive an update from the server, setting this client's SelectedCard property equal to the value
        // of the server's SelectedCard property.
        private void ReceiveUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            ReceiveUpdate();
        }

        private void ReceiveUpdate()
        {
            NetIncomingMessage inc;

            while ((inc = Client.ReadMessage()) != null)
            {
                if (inc.MessageType == NetIncomingMessageType.Data)
                {
                    // Deselect the currently selected card.
                    DeselectCard(FindButton(SelectedCard));
                    SelectedCard = inc.ReadInt32();
                    SelectCard(FindButton(SelectedCard));

                    //// Read the size of the cardlist of the deck.
                    //int size = inc.ReadInt32();

                    //// Update the deck.
                    //for (int i = 0; i < size; ++i)
                    //{
                    //    // This try-catch is only necessary because we are currently not updating the
                    //    // server's deck when a change is made to the client's deck. As a result, the client's
                    //    // deck ends up being smaller than the server's deck when the client receives an update,\
                    //    // causing an index-out-of-bounds exception.
                    //    try
                    //    {
                    //        inc.ReadAllProperties(Deck.CardList[i]);
                    //    }
                    //    catch { break; }
                    //}
                    //// I believe one of these break statements is causing the client to disconnect from the server.
                    //break;
                }
            }
        }

        // Set the server's SelectedCard property equal to the value of this client's SelectedCard property.
        private void UpdateServerButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateServer();
        }

        private void UpdateServer()
        {
            // Create new message
            NetOutgoingMessage outmsg = Client.CreateMessage();

            // Write the value of SelectedCard into the message.
            outmsg.Write(SelectedCard);
            
            //// Since the server's deck should not always be updated, use an enum to notify the server 
            //// when the deck should be updated (Similar to how the GameNetworkingExample works).
            //// Write the values of the cards in the deck to the message.
            //outmsg.Write(Deck.CardList.Count);
            //foreach (Card card in Deck.CardList)
            //{
            //    outmsg.WriteAllProperties(card);
            //}

            // Send it to server
            Client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
        }
        
        // 
        private void UpdateServerDeck()
        {
            // Create new message
            NetOutgoingMessage outmsg = Client.CreateMessage();

            

            // Send it to server
            Client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
        }

        // It seems that clients disconnect randomly from the server. 
        // This allows the connection to be reinitialized.
        private void ReinitializeConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            InitializeClient();
        }

        // This event is not used in the application; it was created a test a component of XAML.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //int deltaX = 0, deltaY = 0;

            //if (e.Key == Key.Left)
            //{
            //    deltaX = -5;
            //}
            //if (e.Key == Key.Right)
            //{
            //    deltaX = 5;
            //}
            //if (e.Key == Key.Up)
            //{
            //    deltaY = -5;
            //}
            //if (e.Key == Key.Down)
            //{
            //    deltaY = 5;
            //}

            //foreach (FrameworkElement element in this.GameCanvas.Children)
            //{
            //    if ( element.IsFocused )
            //    {
            //        double left = (double)element.GetValue(Canvas.LeftProperty);
            //        element.SetValue(Canvas.LeftProperty, left + deltaX);

            //        double top = (double)element.GetValue(Canvas.TopProperty);
            //        element.SetValue(Canvas.TopProperty, top + deltaY);
            //    }
            //}
            //e.Handled = true;
        }
    }
}
