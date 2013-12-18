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
        private List<String> PlayerNames;
        private String ServerIP;
        private int SelectedCard;

        // Client Object
        private volatile NetClient Client;

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow( string ipAddress, string playerName )
        {
            InitializeComponent();

            this.ServerIP = ipAddress;
            this.SelectedCard = -1;

            // Connect the client to the server.
            InitializeClient();

            // Do not do anything until the server finalizes its connection with the client.
            ClientReceiveUpdate(Datatype.FirstMessage);

            // Receive the deck from the server.
            RequestUpdateFromServer(Datatype.RequestDeck);
            ClientReceiveUpdate(Datatype.UpdateDeck);

            // Receive a list of the names of the players already on the server.
            // Eventually, each client will receive a list of Player objects, not just Player names.
            RequestUpdateFromServer(Datatype.RequestPlayerNames);
            ClientReceiveUpdate(Datatype.UpdatePlayerNames);

            // Verify that the value of 'playerName' does not exist in the list of Player names.
            // If it does, modify the name until it no longer matches one on the list.
            bool hasBeenModified = false;
            int a = 2;
            while ( PlayerNames.Contains(playerName) )
            {
                if ( !hasBeenModified )
                {
                    playerName += a;
                    hasBeenModified = true;
                }
                else
                {
                    ++a;
                    playerName = playerName.Substring(0, playerName.Length - 1) + a;
                }
            }

            // Instantiate the player.
            this.Player = new Player(Deck, playerName);

            // Display the cards in this player's hand.
            for ( int i = 0; i < Player.CardsInHand.Count; ++i )
            {
                DisplayCardInHand(Player.CardsInHand, i);
            }

            // Send the updated deck back to the server.
            ServerUtilities.SendUpdate(Client, Datatype.UpdateDeck, Deck);

            // Send the player's information to the server.
            ServerUtilities.SendUpdate(Client, Datatype.UpdatePlayer, Player);
        }

        #region Client Communication Code

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

        private void RequestUpdateFromServer( Datatype datatype )
        {
            NetOutgoingMessage outmsg = Client.CreateMessage();

            outmsg.Write((byte)datatype);

            // Clear all messages that the client currently has.
            while ( Client.ReadMessage() != null ) ;

            Client.SendMessage(outmsg, NetDeliveryMethod.ReliableOrdered);
        }

        // Return a boolean indicating whether or not an update was received.
        private void ClientReceiveUpdate( Datatype messageType )
        {
            NetIncomingMessage inc;
            bool updateReceived = false;

            // Continue reading messages until the requested update is received.
            while ( !updateReceived )
            {
                // Wait until the client receives a message from the server.
                while ( (inc = Client.ReadMessage()) == null ) ;

                // Iterate through all of the available messages.
                while ( inc != null )
                {
                    if ( inc.MessageType == NetIncomingMessageType.Data )
                    {
                        if ( (Datatype)inc.ReadByte() == messageType )
                        {
                            switch ( messageType )
                            {
                                case Datatype.UpdateDeck:
                                {
                                    Deck = (Deck)ServerUtilities.ReceiveUpdate(inc, messageType);

                                    updateReceived = true;

                                    // I'm not sure if this is necessary.
                                    //Client.Recycle(inc);

                                    break;
                                }

                                case Datatype.UpdateSelectedCard:
                                {
                                    DeselectCard(FindButton(SelectedCard));
                                    SelectedCard = (int)ServerUtilities.ReceiveUpdate(inc, messageType);
                                    SelectCard(FindButton(SelectedCard));

                                    updateReceived = true;
                                    break;
                                }

                                case Datatype.UpdatePlayerNames:
                                {
                                    PlayerNames = (List<string>)ServerUtilities.ReceiveUpdate(inc, messageType);

                                    updateReceived = true;
                                    break;
                                }
                            }
                        }
                    }
                    else if ( inc.MessageType == NetIncomingMessageType.StatusChanged && messageType == Datatype.FirstMessage )
                    {
                        updateReceived = true;
                    }

                    inc = Client.ReadMessage();
                }
            }
        }

        #endregion

        #region Gui Related Code

        // Display a card in a player's hand.
        public void DisplayCardInHand( List<Card> cardsInHand, int position )
        {
            Button cardButton = new Button();
            cardButton.Content = cardsInHand[position].CardImage;
            cardButton.Tag = cardsInHand[position];
            cardButton.Style = (Style)FindResource("NoChromeButton");
            cardButton.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(SelectCardEvent);
            cardButton.PreviewMouseRightButtonDown += new MouseButtonEventHandler(PlayCardEvent);

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

        public void PlayCardEvent( object sender, MouseButtonEventArgs args )
        {
            Button cardButton = FindButton(SelectedCard);

            if ( -1 != SelectedCard && sender == cardButton )
            {
                RemoveCardFromHand(cardButton);

                // Update the value of 'SelectedCard' (no card is selected after a card is played).
                SelectedCard = -1;

                AddCardToPlay(cardButton);

            }
        }

        // Add a card to the player's side of the playing field.
        public void AddCardToPlay( Button cardButton )
        {
            // Add the card to the Player's CardsInPlay list.
            Player.CardsInPlay.Add(cardButton.Tag as Card);
        }

        // Remove a card (given its Button wrapper) from the player's hand.
        public void RemoveCardFromHand( Button cardButton )
        {
            // Remove the card from the Player's CardsInHand list.
            Player.CardsInHand.Remove(cardButton.Tag as Card);

            // Get the index of the card button.
            int buttonIndex = Grid.GetColumn(cardButton.Parent as Grid);

            // Remove the card's button from the PlayerOneHand grid.
            PlayerOneHand.Children.RemoveAt(buttonIndex);

            // Shift all subsequetial card buttons to the left.
            for ( int i = buttonIndex; i < Player.CardsInHand.Count; ++i )
            {
                Grid.SetColumn(PlayerOneHand.Children[i], i);
            }
        }

        // Increase the size of a card when it is selected and decrease its size when another card is selected.
        public void SelectCardEvent( object sender, MouseButtonEventArgs args )
        {
            // Get the index of the button that called this event.
            int buttonIndex = Grid.GetColumn((sender as Button).Parent as Grid);

            if ( buttonIndex != SelectedCard )
            {
                // Deselect the currently selected card.
                if ( SelectedCard != -1 )
                {
                    DeselectCard(FindButton(SelectedCard));
                }

                // Select this card.
                SelectedCard = buttonIndex;
                SelectCard(sender as Button);
            }
            else
            {
                // Deselect this card if it is already selected.
                SelectedCard = -1;
                DeselectCard(sender as Button);
            }
        }

        // Find a card in the player's hand given its position in PlayerOneHand.
        private Button FindButton( int buttonIndex )
        {
            if ( SelectedCard != -1 )
            {
                for ( int i = 0; i < PlayerOneHand.Children.Count; ++i )
                {
                    Button cardButton = (PlayerOneHand.Children[i] as Grid).Children[0] as Button;
                    if ( Grid.GetColumn((PlayerOneHand.Children[i] as Grid)) == buttonIndex )
                    {
                        return cardButton;
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
            ClientReceiveUpdate(Datatype.UpdateSelectedCard);
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

        #endregion
    }
}
