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
using System.Timers;
using System.Threading;
using System.ComponentModel;

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
        public delegate void DoWorkDelegate( object sender, DoWorkEventArgs e );

        // Client Object
        private volatile NetClient Client;

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow( string ipAddress )
        {
            InitializeComponent();

            this.ServerIP = ipAddress;
            this.SelectedCard = -1;

            // Connect the client to the server.
            InitializeClient();

            // Do not do anything until the server finalizes its connection with the client.
            while ( !ReceiveUpdate(Datatype.FirstMessage) ) ;

            // Initialize the deck.
            //this.Deck = new Deck();

            //UpdateServer();

            RequestUpdateFromServer(Datatype.RequestDeck);

            //// Testing use of background worker.
            //BackgroundWorker test = new BackgroundWorker();
            //test.DoWork += new DoWorkEventHandler(new DoWorkDelegate(RequestUpdateFromServer));
            //test.RunWorkerAsync();

            //Thread messageThread = new Thread(RequestUpdateFromServer);
            //messageThread.Name = "Message Thread";
            //messageThread.Start();

            //// Loop until worker thread activates. 
            //while (!messageThread.IsAlive)
            //{
            //    Console.WriteLine("Thread not yet alive");
            //};

            // Put the main thread to sleep for 1 millisecond to 
            // allow the worker thread to do some work:
            //Thread.Sleep(1);

            while ( null == Deck )
            {
                ReceiveUpdate(Datatype.UpdateDeck);
            }

            //messageThread.Abort();
            //messageThread.Join();

            //ReceiveUpdate(Datatype.UpdateDeck);

            this.Player = new Player(Deck, "Player");

            // Display the cards in this player's hand.
            for ( int i = 0; i < Player.CardsInHand.Count; ++i )
            {
                DisplayCardInHand(Player.CardsInHand, i);
            }

            // Send the updated deck back to the server.
            //ServerUtilities.SendUpdatedDeck(Client, Deck);
            ServerUtilities.SendUpdate(Client, Datatype.UpdateDeck, Deck);
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

            // Set timer to tick every 50ms
            System.Timers.Timer update = new System.Timers.Timer(1000);

            // When time has elapsed ( 50ms in this case ), call "update_Elapsed" funtion
            update.Elapsed += new ElapsedEventHandler(update_Elapsed);

            // Start the timer
            update.Start();
        }

        // Display a card in a player's hand.
        // Right now, this method only works for player one's hand.
        public void DisplayCardInHand( List<Card> cardsInHand, int position )
        {
            Button cardButton = new Button();
            cardButton.Name = "CardButton" + position;
            cardButton.Content = cardsInHand[position].CardImage;
            cardButton.Style = (Style)FindResource("NoChromeButton");
            cardButton.Click += new RoutedEventHandler(cardButton_Click);

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
        public void cardButton_Click( object sender, RoutedEventArgs args )
        {
            // Deselect the currently selected card.
            DeselectCard(FindButton(SelectedCard));

            // Update the value of SelectedCard.
            for ( int i = 0; i < PlayerOneHand.Children.Count; ++i )
            {
                foreach ( FrameworkElement element in (PlayerOneHand.Children[i] as Grid).Children )
                {
                    if ( element.Name == (sender as Button).Name )
                    {
                        if ( i != SelectedCard )
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
        private Button FindButton( int buttonIndex )
        {
            if ( SelectedCard != -1 )
            {
                for ( int i = 0; i < PlayerOneHand.Children.Count; ++i )
                {
                    foreach ( FrameworkElement element in (PlayerOneHand.Children[i] as Grid).Children )
                    {
                        if ( element.Name == "CardButton" + buttonIndex )
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
            if ( cardButton != null )
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1.2;
                myScaleTransform.ScaleX = 1.2;

                cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                cardButton.RenderTransform = myScaleTransform;
            }
        }

        // Set the currently selected card to its normal size.
        private void DeselectCard( Button cardButton )
        {
            if ( cardButton != null )
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1;
                myScaleTransform.ScaleX = 1;

                cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                cardButton.RenderTransform = myScaleTransform;
            }
        }

        // Again, testing AttachedProperties. Will need to discuss.
        private void setInfoBox( TriggerBase target, int location )
        {
            target.SetValue(InfoBoxAttachedProperty, Player.CardsInHand[location]);
        }

        // Receive an update from the server, setting this client's SelectedCard property equal to the value
        // of the server's SelectedCard property.
        private void ReceiveUpdatesButton_Click( object sender, RoutedEventArgs e )
        {
            RequestUpdateFromServer(Datatype.RequestSelectedCard);

            //int previousValue = SelectedCard;

            //Thread messageThread = new Thread(RequestUpdateFromServer);
            //messageThread.Start();
            // Loop until worker thread activates. 
            //while ( !messageThread.IsAlive )
            {
                Console.WriteLine("Thread not yet alive");
            }

            // As of now, do not select the same card twice.
            //while ( SelectedCard == previousValue )
            {
                ReceiveUpdate(Datatype.UpdateSelectedCard);
            }

            //messageThread.Abort();
            //messageThread.Join();
        }

        private void RequestUpdateFromServer( Datatype datatype )
        {
            NetOutgoingMessage outmsg = Client.CreateMessage();

            outmsg.Write((byte)datatype);

            // Clear all messages that the client currently has.
            while ( Client.ReadMessage() != null ) ;

            Client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
        }

        // Return a boolean indicating whether or not an update was received.
        private bool ReceiveUpdate(Datatype messageType)
        {
            NetIncomingMessage inc;

            // Wait until the client receives a message from the server.
            while ( (inc = Client.ReadMessage()) == null ) ; 

            // Iterate through all of the available messages.
            while ( inc != null )
            {
                if ( inc.MessageType == NetIncomingMessageType.Data )
                {
                    if ((Datatype)inc.ReadByte() == messageType)
                    {
                        switch (messageType)
                        {
                            case Datatype.UpdateDeck:
                            {
                                Deck = (Deck)ServerUtilities.ReceiveUpdate(inc, messageType);
                                Client.Recycle(inc);
                                break;
                            }

                            case Datatype.UpdateSelectedCard:
                            {
                                DeselectCard(FindButton(SelectedCard));
                                SelectedCard = (int)ServerUtilities.ReceiveUpdate(inc, messageType);
                                SelectCard(FindButton(SelectedCard));
                                Client.Recycle(inc);
                                break;
                            }
                        }

                        return true;
                    }
                }
                else if ( inc.MessageType == NetIncomingMessageType.StatusChanged && messageType == Datatype.FirstMessage )
                {
                    return true;
                }

                inc = Client.ReadMessage();
            }

            return false;
        }

        // Set the server's SelectedCard property equal to the value of this client's SelectedCard property.
        private void UpdateServerButton_Click( object sender, RoutedEventArgs e )
        {
            ServerUtilities.SendUpdate(Client, Datatype.UpdateSelectedCard, SelectedCard);
        }

        // It seems that clients disconnect randomly from the server. This allows the connection to be reinitialized.
        private void ReinitializeConnectionButton_Click( object sender, RoutedEventArgs e )
        {
            InitializeClient();
        }

        // Use this event to perform periodic actions.
        static void update_Elapsed( object sender, System.Timers.ElapsedEventArgs e )
        {
            
        }

        // Use this event to respond to key presses.
        private void Window_KeyDown( object sender, KeyEventArgs e )
        {

        }
    }
}
