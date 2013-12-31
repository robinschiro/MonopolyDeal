using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameObjects;
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
        private Player Player;
        private List<Player> PlayerList;
        private String ServerIP;
        private int SelectedCard;
        private bool BeginCommunication;
        private volatile NetClient Client;

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow( string playerName, string ipAddress, NetClient client = null )
        {
            InitializeComponent();
            this.SelectedCard = -1;
            this.BeginCommunication = false;
            this.ServerIP = ipAddress;

            // Connect the client to the server.
            InitializeClient(ipAddress);

            // Do not continue until the client has successfully established communication with the server.
            while ( !this.BeginCommunication );

            // Receive a list of the players already on the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestPlayerList);

            // Do not continue until the client receives the Player List from the server.
            while ( this.PlayerList == null ) ;

            // Find the Player in the PlayerList.
            this.Player = FindPlayerInList(playerName);

            // Re-title the window.
            this.Title = playerName + "'s Window";

            // Display the cards in this player's hand.
            foreach ( Card card in Player.CardsInHand )
            {
                AddCardToGrid(card, PlayerOneHand, true);
            }
        }

        #region Client Communication Code

        private void InitializeClient(string serverIP)
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
            Client.Connect(serverIP, 14242, outmsg);

            // Create the synchronization context used by the client to receive updates as soon as they are available.
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            // Register the 'GotMessage' method as a callback function for received updates.
            Client.RegisterReceivedCallback(new SendOrPostCallback(GotMessage));
        }

        // Update the client's window based on messages received from the server. This method is called as soon as the client
        // receives a message.
        public void GotMessage( object peer )
        {
            NetIncomingMessage inc;
            bool updateReceived = false;

            // Continue reading messages until the requested update is received.
            while ( !updateReceived )
            {
                // Iterate through all of the available messages.
                while ( (inc = (peer as NetPeer).ReadMessage()) != null )
                {
                    if ( inc.MessageType == NetIncomingMessageType.Data )
                    {
                        Datatype messageType = (Datatype)inc.ReadByte();

                        switch ( messageType )
                        {
                            case Datatype.UpdateDeck:
                            {
                                this.Deck = (Deck)ServerUtilities.ReceiveMessage(inc, messageType);

                                // I'm not sure if this is necessary.
                                //Client.Recycle(inc);

                                break;
                            }

                            case Datatype.UpdatePlayerList:
                            {
                                this.PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);

                                ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Object>(DisplayOpponentCards), null); };
                                Thread newThread = new Thread(start);
                                newThread.Start();

                                break;
                            }
                        }

                        updateReceived = true;
                        
                    }
                    else if ( inc.MessageType == NetIncomingMessageType.StatusChanged )
                    {
                        this.BeginCommunication = true;
                        updateReceived = true;
                    }
                }
            }
        } 

        #endregion

        #region Events

        public void PlayCardEvent( object sender, MouseButtonEventArgs args )
        {
            Button cardButton = FindButton(SelectedCard);

            if ( -1 != SelectedCard && sender == cardButton )
            {
                RemoveCardFromHand(cardButton);

                // Update the value of 'SelectedCard' (no card is selected after a card is played).
                SelectedCard = -1;

                // Add the card to the Player's CardsInPlay list.
                Player.CardsInPlay.Add(cardButton.Tag as Card);

                AddCardToGrid(cardButton.Tag as Card, PlayerOneField, false);

                // Update the server.
                ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
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

        // It seems that clients disconnect randomly from the server. This allows the connection to be reinitialized.
        private void ReinitializeConnectionButton_Click( object sender, RoutedEventArgs e )
        {
            // Disconnect from the server
            Client.Disconnect("Disconnecting");

            NetOutgoingMessage outmsg = Client.CreateMessage();

            // Write byte ( first byte informs server about the message type ) ( This way we know, what kind of variables to read )
            outmsg.Write((byte)PacketTypes.LOGIN);

            // Write String "Name" . Not used, but just showing how to do it
            outmsg.Write("MyName");

            // Connect to the server.
            Client.Connect(ServerIP, 14242, outmsg);
        }

        // Use this event to respond to key presses.
        private void Window_KeyDown( object sender, KeyEventArgs e )
        {

        }

        #endregion

        #region Hand and Field Manipulation
 
        // Wrap a Button around a Card.
        public Button ConvertCardToButton( Card card )
        {
            Button cardButton = new Button();
            cardButton.Content = card.CardImageUriPath;
            cardButton.Tag = card;
            cardButton.Style = (Style)FindResource("NoChromeButton");

            return cardButton;
        }

        // This is a proof-of-concept method; it will later be deleted or significantly modified.
        // Display the cards played by the player's opponent (this method is flawed because it only works for 2-player games).
        public void DisplayOpponentCards(Object filler = null)
        {
            Player opponent = null;

            // Find the opponent in the PlayerList.
            foreach ( Player player in PlayerList )
            {
                if ( player.Name != this.Player.Name )
                {
                    opponent = player;
                    break;
                }
            }

            // If the opponent is found, display his cards.
            if ( opponent != null )
            {
                // Update the display of the opponent's cards in play.
                ClearCardsInGrid(PlayerTwoField);
                foreach ( Card card in opponent.CardsInPlay )
                {
                    AddCardToGrid(card, PlayerTwoField, false);
                }

                // Update the display of the opponent's hand.
                ClearCardsInGrid(PlayerTwoHand);
                foreach ( Card card in opponent.CardsInHand )
                {
                    Card cardBack = new Card(-1, "pack://application:,,,/GameObjects;component/Images/cardback.jpg");
                    AddCardToGrid(cardBack, PlayerTwoHand, false);
                }
            }

        }

        // Clear all of the card buttons from a given grid.
        public void ClearCardsInGrid( Grid playerField )
        {
            playerField.Children.Clear();
        }

        // Add a card to the player's side of the playing field.
        public void AddCardToGrid( Card card, Grid grid, bool isHand )
        {
            Button cardButton = new Button();

            // Create an image based on the card's uri path.
            cardButton.Content = new Image();
            (cardButton.Content as Image).Source = new BitmapImage(new Uri(card.CardImageUriPath, UriKind.Absolute));

            cardButton.Tag = card;
            cardButton.Style = (Style)FindResource("NoChromeButton");

            // If a card is being added to the client's hand, attach these events to it.
            if ( isHand )
            {
                cardButton.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(SelectCardEvent);
                cardButton.PreviewMouseRightButtonDown += new MouseButtonEventHandler(PlayCardEvent);
            }

            // Wrap the card inside a grid in order to insert spaces between the displayed the cards.
            Grid cardGridWrapper = new Grid();

            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition col2 = new ColumnDefinition();
            col2.Width = new GridLength(16, GridUnitType.Star);
            ColumnDefinition col3 = new ColumnDefinition();
            col3.Width = new GridLength(1, GridUnitType.Star);

            cardGridWrapper.ColumnDefinitions.Add(col1);
            cardGridWrapper.ColumnDefinitions.Add(col2);
            cardGridWrapper.ColumnDefinitions.Add(col3);
            cardGridWrapper.Children.Add(cardButton);
            Grid.SetColumn(cardButton, 1);

            // Add the card (within its grid wrapper) to the next available position in the specified grid.
            grid.Children.Add(cardGridWrapper);
            Grid.SetColumn(cardGridWrapper, grid.Children.Count - 1);
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

        // Find a card in the player's hand given its position in PlayerOneHand.
        private Button FindButton( int buttonIndex )
        {
            if ( buttonIndex != -1 )
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

        #endregion

        #region Miscellaneous

        private Player FindPlayerInList( string playerName )
        {
            foreach ( Player player in PlayerList )
            {
                if ( player.Name == playerName )
                {
                    return player;
                }
            }

            return null;
        }

        private string VerifyPlayerName( string playerName )
        {
            bool hasBeenModified = false;
            int a = 2;
            List<string> playerNames = new List<string>();

            // Generate the the list of player names.
            foreach ( Player player in PlayerList )
            {
                playerNames.Add(player.Name);
            }

            while ( playerNames.Contains(playerName) )
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

            return playerName;
        }

        #endregion
    }
}
