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
using Lidgren.Network;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window
    {
        private Deck Deck;
        private List<Player> Players;
        private String ServerIP;
        private int SelectedCard;

        // Client Object
        static NetClient Client;

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow(int numberOfPlayers, string ipAddress)
        {
            InitializeComponent();

            // Initialize the deck.
            this.Deck = new Deck();

            this.ServerIP = ipAddress;
            this.SelectedCard = -1;

            // Initialize the player collection.
            this.Players = new List<Player>();
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                Players.Add(new Player(Deck, "Player " + i));
            }

            for (int i = 0; i < Players[0].CardsInHand.Count; ++i)
            {
                DisplayCardInHand(Players[0].CardsInHand, i);
            }

            InitializeClient();
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
            if (SelectedCard != -1)
            {
                DeselectCard(FindButton(SelectedCard));
            }

            // Update the value of SelectedCard.
            for (int i = 0; i < PlayerOneHand.Children.Count; ++i)
            {
                foreach (FrameworkElement element in (PlayerOneHand.Children[i] as Grid).Children)
                {
                    if (element.Name == (sender as Button).Name)
                    {
                        SelectedCard = i;
                        break;
                    }
                }
            }

            SelectCard(sender as Button);
        }

        // Find a card in player one's hand given its position in the grid displaying the hand.
        private Button FindButton(int buttonIndex)
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
            return null;
        }

        // This increases the size of the currently selected card.
        private void SelectCard( Button cardButton )
        {
            ScaleTransform myScaleTransform = new ScaleTransform();
            myScaleTransform.ScaleY = 1.2;
            myScaleTransform.ScaleX = 1.2;

            cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
            cardButton.RenderTransform = myScaleTransform;
        }

        // This returns the previously selected card to its normal size.
        private void DeselectCard(Button cardButton)
        {
            ScaleTransform myScaleTransform = new ScaleTransform();
            myScaleTransform.ScaleY = 1;
            myScaleTransform.ScaleX = 1;

            cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
            cardButton.RenderTransform = myScaleTransform;
        }

        // Again, testing AttachedProperties. Will need to discuss.
        private void setInfoBox(TriggerBase target, int location)
        {
            target.SetValue(InfoBoxAttachedProperty, Players[0].CardsInHand[location]);
        }

        // Receive an update from the server, setting this client's SelectedCard property equal to the value
        // of the server's SelectedCard property.
        private void ReceiveUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            NetIncomingMessage inc;
            while ((inc = Client.ReadMessage()) != null)
            {
                if (inc.MessageType == NetIncomingMessageType.Data)
                {
                    // Deselect the currently selected card.
                    if (SelectedCard != -1)
                    {
                        DeselectCard(FindButton(SelectedCard));
                    }

                    SelectedCard = inc.ReadInt32();

                    SelectCard(FindButton(SelectedCard));
                }
            }
        }

        // Set the server's SelectedCard property equal to the value of this client's SelectedCard property.
        private void UpdateServerButton_Click(object sender, RoutedEventArgs e)
        {
            // Create new message
            NetOutgoingMessage outmsg = Client.CreateMessage();

            // Write the value of SelectedCard into the message.
            outmsg.Write(SelectedCard);

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
