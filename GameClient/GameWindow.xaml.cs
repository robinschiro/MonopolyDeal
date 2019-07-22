using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using AdditionalWindows;
using GameObjects;
using GameServer;
using Lidgren.Network;
using Utilities;
using ResourceList = GameClient.Properties.Resources;

namespace GameClient
{

    /// <summary>
    /// Interaction logic for GameWindow.xaml
    /// </summary>
    public partial class GameWindow : Window, IGameClient, INotifyPropertyChanged
    {

        #region Variables

        private Deck Deck;
        private Player Player;
        private string PlayerName;
        private List<Player> PlayerList;
        private String ServerIP;
        private bool BeginCommunication;
        private bool UpdateReceived;
        private bool ReceivedPlayerList;
        private bool ReceivedDeck;
        private volatile NetClient Client;
        private Dictionary<String, Grid> PlayerFieldDictionary;
        private Dictionary<String, Grid> PlayerHandDictionary;
        private bool HavePlayersBeenAssigned;
        private Turn Turn;
        private List<Card> DiscardPile;
        private ColorAnimation EndTurnButtonAnimation;

        // Variables for managing the state of Action Cards.
        private int NumberOfRentees = 0;
        private List<Card> AssetsReceived = new List<Card>();
        private MessageDialog WaitMessage;
        private ActionData.RentRequest LastRentRequest;
        private ActionData.TheftRequest LastTheftRequest;

        public event PropertyChangedEventHandler PropertyChanged;

        private SoundPlayer SoundPlayer;

        private bool isCurrentTurnOwner;
        public bool IsCurrentTurnOwner
        {
            get
            {
                return isCurrentTurnOwner;
            }
            set
            {
                isCurrentTurnOwner = value;
                OnPropertyChanged("IsCurrentTurnOwner");
            }
        }

        #endregion Variables

        public GameWindow( string playerName, string ipAddress, int portNumber, Turn turn )
        {
            InitializeComponent();

            this.BeginCommunication = false;
            this.UpdateReceived = false;
            this.ReceivedPlayerList = false;
            this.ReceivedDeck = false;
            this.HavePlayersBeenAssigned = false;
            this.ServerIP = ipAddress;
            this.PlayerName = playerName;
            this.SoundPlayer = new SoundPlayer();
            this.EndTurnButtonAnimation = this.CreateEndTurnButtonAnimation();

            // Instantiate the Player's Turn object.
            this.Turn = turn;

            // Instantiate the DiscardPile.
            this.DiscardPile = new List<Card>();

            // Connect the client to the server.
            InitializeClient(ipAddress, portNumber);

            // Do not continue until the client has successfully established communication with the server.
            WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting to establish communication with server...");
            if ( !this.BeginCommunication )
            {
                WaitMessage.ShowDialog();
            }

            // Request the player list and wait for it before continuing.
            {
                // Create a Wait dialog to stop the creation of the room window until the player list is retrieved from the server.
                WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting for player list...");

                // Receive a list of the players already on the server.
                ServerUtilities.SendMessage(Client, Datatype.RequestPlayerList);

                // Do not continue until the client receives the Player List from the server.
                WaitMessage.ShowDialog();
            }

            // Request the deck and wait for it before continuing.
            {
                // Create a Wait dialog to stop the creation of the room window until the player list is retrieved from the server.
                WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting for deck...");

                // Receive a list of the players already on the server.
                ServerUtilities.SendMessage(Client, Datatype.RequestDeck);

                // Do not continue until the client receives the Player List from the server.
                WaitMessage.ShowDialog();
            }

            // Update the turn display.
            UpdateTurnDisplay(null);

            // Inform the next player in the list that he can connect.
            int pos = FindPlayerPositionInPlayerList(this.Player.Name);
            if ( pos < (this.PlayerList.Count - 1) )
            {
                ServerUtilities.SendMessage(Client, Datatype.TimeToConnect, this.PlayerList[pos + 1].Name);
            }

            // Assign the hands and playing fields of opponents to appropriate areas of the client's screen.
            AssignPlayersToGrids();

            WindowGrid.DataContext = this;

            // Re-title the window.
            this.Title = playerName + "'s Window";
            
            // Add an empty grid as the first element of every field. This grid is used to display each player's money pile.
            foreach ( Grid field in PlayerFieldDictionary.Values )
            {
                field.Children.Add(CreateCardGrid());
            }
            
            // Add player names to each playing field.
            for ( int i = 0; i < this.PlayerList.Count; i++ )
            {
                if ( this.PlayerList[i].Name != this.PlayerName )
                {
                    TextBlock playerNameTextBlock = new TextBlock();
                    playerNameTextBlock.Text = "Name: " + this.PlayerList[i].Name;

                    Viewbox playerNameViewbox = new Viewbox();
                    playerNameViewbox.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                    playerNameViewbox.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    playerNameViewbox.Child = playerNameTextBlock;

                    // Create a separator to separate the playing fields.
                    Separator fieldSeparator = new Separator();
                    fieldSeparator.Style = (Style)FindResource("FieldSeparator");

                    // Add the UI elements to the playing field.
                    PlayingField.Children.Add(playerNameViewbox);
                    PlayingField.Children.Add(fieldSeparator);

                    // Set the row positions of the UI elements.
                    int row = -2 * GetRelativePosition(this.PlayerName, PlayerList[i].Name) + 9;
                    Grid.SetRow(playerNameViewbox, row);
                    Grid.SetRow(fieldSeparator, row);
                }
            }

            // Display the cards in this player's hand.
            foreach ( Card card in Player.CardsInHand )
            {
                AddCardToGrid(card, PlayerOneHand, Player, true);
            }

            // Because this call automatically draws cards for the player, it must occur after the player's cardsInHand have been placed on the grid.
            CheckIfCurrentTurnOwner(shouldNotifyUserObject: false);

            // Enable Reactive Extensions (taken from http://goo.gl/0Jr5WU) in order to perform Size_Changed responses at the end of a chain of resizing events
            // (instead performing a Size_Changed response for every resize event). This is used to improve efficiency.
            IObservable<SizeChangedEventArgs> ObservableSizeChanges = Observable
                .FromEventPattern<SizeChangedEventArgs>(this, "SizeChanged")
                .Select(x => x.EventArgs)
                .Throttle(TimeSpan.FromMilliseconds(200));

            IDisposable SizeChangedSubscription = ObservableSizeChanges
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(x =>
                {
                    Size_Changed(x);
                });
        }

        #region Client Communication Code

        private void InitializeClient( string serverIP, int portNumber )
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
            Client.Connect(serverIP, portNumber, outmsg);

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

            if ( false == this.BeginCommunication )
            {
                // Continue reading messages until the requested update is received.
                while ( !UpdateReceived )
                {
                    Console.WriteLine(this.PlayerName + " not received update: " + UpdateReceived);

                    // Iterate through all of the available messages.
                    while ( (inc = (peer as NetPeer).ReadMessage()) != null )
                    {
                        if ( inc.MessageType == NetIncomingMessageType.StatusChanged )
                        {
                            this.BeginCommunication = true;
                            UpdateReceived = true;

                            Console.WriteLine(this.PlayerName + " received update: " + UpdateReceived);

                            // Close the wait message if it is open.
                            if ( null != this.WaitMessage && !this.WaitMessage.CloseWindow )
                            {
                                CreateNewThread(new Action<Object>(( sender ) => { this.WaitMessage.CloseWindow = true; }));
                            }
                        }

                        Thread.Sleep(100);
                    }

                    Thread.Sleep(100);
                }
            }
            else
            {
                inc = (peer as NetPeer).ReadMessage();

                if ( null != inc &&  inc.MessageType == NetIncomingMessageType.Data )
                {
                    Datatype messageType = (Datatype)inc.ReadByte();

                    switch ( messageType )
                    {
                        case Datatype.UpdateDeck:
                        {
                            this.Deck = (Deck)ServerUtilities.ReceiveMessage(inc, messageType);

                            // Close the wait message if it is open and we have not received the player list yet.
                            if ( !this.ReceivedDeck && null != this.WaitMessage && !this.WaitMessage.CloseWindow )
                            {
                                this.ReceivedDeck = true;
                                CreateNewThread(new Action<Object>(( sender ) => { this.WaitMessage.CloseWindow = true; }));
                            }

                            break;
                        }

                        case Datatype.UpdateDiscardPile:
                        {
                            this.DiscardPile = (List<Card>)ServerUtilities.ReceiveMessage(inc, messageType);

                            CreateNewThread(new Action<Object>(DisplayUpdatedDiscardPile));

                            break;
                        }

                        case Datatype.UpdatePlayerList:
                        {
                            this.PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);

                            // If the Player associated with this client does not exist yet, retrieve it from the list.
                            if ( null == this.Player )
                            {
                                this.Player = (Player)this.PlayerList.Find(player => player.Name == this.PlayerName);
                            }

                            // Display everyone's cards in play.
                            CreateNewThread(new Action<Object>(DisplayAllCards));

                            // Close the wait message if it is open and we have not received the player list yet.
                            if ( !this.ReceivedPlayerList && null != this.WaitMessage && !this.WaitMessage.CloseWindow )
                            {
                                this.ReceivedPlayerList = true;
                                CreateNewThread(new Action<Object>(( sender ) => { this.WaitMessage.CloseWindow = true; }));
                            }

                            break;
                        }

                        case Datatype.UpdateTurn:
                        {
                            this.Turn = (Turn)ServerUtilities.ReceiveMessage(inc, messageType);

                            // Update the turn indicator and action count.
                            CreateNewThread(new Action<Object>(UpdateTurnDisplay));

                            break;
                        }

                        case Datatype.RequestRent:
                        {
                            // Retrieve the request.
                            ActionData.RentRequest request = (ActionData.RentRequest)ServerUtilities.ReceiveMessage(inc, messageType);

                            // Check if the player is one of the players who must pay rent. If he is, open the rent window.
                            if ( request.Rentees.Any(player => player.Name == this.Player.Name) )
                            {
                                CreateNewThread(new Action<Object>(DisplayRentWindow), request);
                            }

                            break;
                        }

                        case Datatype.GiveRent:
                        {
                            // Retrieve the request.
                            ActionData.RentResponse response = (ActionData.RentResponse)ServerUtilities.ReceiveMessage(inc, messageType);

                            // If this player is the renter who originally requested the rent, add the AssetsGiven to the player's AssetsReceived list.
                            if ( response.RenterName == this.Player.Name )
                            {
                                CreateNewThread(new Action<Object>(ProcessReceivedRent), response);
                            }

                            break;
                        }

                        case Datatype.RequestTheft:
                        {
                            // Retrieve the request.
                            ActionData.TheftRequest request = (ActionData.TheftRequest)ServerUtilities.ReceiveMessage(inc, messageType);

                            if ( request.VictimName == this.Player.Name )
                            {
                                CreateNewThread(new Action<Object>(ProcessTheftRequest), request);
                            }

                            break;
                        }

                        case Datatype.ReplyToTheft:
                        {
                            // Retrieve the request.
                            ActionData.TheftResponse response = (ActionData.TheftResponse)ServerUtilities.ReceiveMessage(inc, messageType);

                            if ( response.ThiefName == this.Player.Name )
                            {
                                CreateNewThread(new Action<Object>(ProcessTheftResponse), response);
                            }

                            break;
                        }

                        case Datatype.EndTurn:
                        {
                            this.Turn = (Turn)ServerUtilities.ReceiveMessage(inc, messageType);

                            // Check to see if the player is the current turn owner. 
                            CreateNewThread(new Action<Object>(CheckIfCurrentTurnOwner), true);

                            // Update the turn display.
                            CreateNewThread(new Action<Object>(UpdateTurnDisplay));

                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Events

        private void OnPropertyChanged( string info )
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if ( handler != null )
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }

        public void PlayCardEvent( object sender, MouseButtonEventArgs args )
        {
            if ( isCurrentTurnOwner && this.Turn.ActionsRemaining > 0 )
            {
                Button cardButton = sender as Button;

                Card cardBeingPlayed = cardButton.Tag as Card;
                bool cardWasPlayed = false;

                // Add the card to the Player's playing field (if it is not an action card).
                if ( cardBeingPlayed.Type != CardType.Action )
                {
                    // Add the card to the Player's CardsInPlay list.
                    cardWasPlayed = AddCardToCardsInPlay(cardBeingPlayed, this.Player);
                }
                else
                {
                    // Handle the action.
                    cardWasPlayed = HandleAction(cardBeingPlayed);
                }

                if ( cardWasPlayed )
                {
                    // Update the player's number of actions.
                    this.Turn.ActionsRemaining--;

                    // Send the update to all players.
                    ServerUtilities.SendMessage(this.Client, Datatype.UpdateTurn, this.Turn);

                    if ( cardBeingPlayed.Type != CardType.Action )
                    {
                        RemoveCardFromHand(cardBeingPlayed);
                    }

                    // Update animation on end turn button.
                    if ( 0 == this.Turn.ActionsRemaining )
                    {
                        this.AnimateEndTurnButton();
                    }
                }                
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
            Client.Connect(ServerIP, ServerUtilities.PORT_NUMBER, outmsg);
        }

        private void Size_Changed( SizeChangedEventArgs e )
        {
            CreateNewThread(new Action<Object>(ResizeUIElements));
        }

        // End the player's turn.
        private void EndTurnButton_Click( object sender, RoutedEventArgs e )
        {
            // Do not let player end turn if have more than 7 cards in hand.
            if ( this.Player.CardsInHand.Count > 7 )
            {
                MessageBox.Show("You cannot have more than 7 cards at the end of your turn. Please discard some cards.", "Too Many Cards", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update the current turn owner by cycling through the player list.
            if ( Turn.CurrentTurnOwner == PlayerList.Count - 1 )
            {
                this.Turn.CurrentTurnOwner = 0;
            }
            else
            {
                this.Turn.CurrentTurnOwner++;
            }

            // Reset the number of actions.
            this.Turn.ActionsRemaining = Turn.INITIAL_ACTION_COUNT;

            // Send the updated Turn object to the server to be distributed to the other clients.
            ServerUtilities.SendMessage(Client, Datatype.EndTurn, this.Turn);

            this.EndTurnButton.ClearValue(Button.BackgroundProperty);
        }

        // When a user clicks and holds onto a card, trigger a drag event.
        private void cardButton_PreviewMouseMove( object sender, MouseEventArgs e )
        {
            Button cardButton = sender as Button;
            if ( cardButton != null && e.LeftButton == MouseButtonState.Pressed )
            {
                DragDrop.DoDragDrop(cardButton, cardButton, DragDropEffects.Move);
            }
        }

        // Allow cards to be drag and dropped from one place to another.
        void cardButton_Drop( object sender, DragEventArgs e )
        {
            if ( this.IsCurrentTurnOwner )
            {
                Button targetCardButton = sender as Button;
                List<Card> targetCardList = FindListContainingCard(targetCardButton.Tag as Card);
                PropertyType targetCardListColor = ClientUtilities.GetCardListColor(targetCardList);

                Button sourceCardButton = e.Data.GetData(typeof(Button)) as Button;
                Card sourceCard = sourceCardButton.Tag as Card;

                // Do not allow a drag-drop operation to occur if the source and target are the same objects.
                if ( sourceCardButton != targetCardButton )
                {
                    bool isMonopoly = ClientUtilities.IsCardListMonopoly(targetCardList);
                    bool performTransfer = false;

                    if ( !isMonopoly )
                    {
                        performTransfer = (targetCardListColor == sourceCard.Color || PropertyType.Wild == sourceCard.Color || 
                                           PropertyType.Wild == targetCardListColor || PropertyType.None == targetCardListColor);

                        //// Mark the event as handled.
                        //e.Handled = true;
                    }
                    else
                    {
                        switch ( sourceCard.Name )
                        {
                            case ("House"):
                            {
                                performTransfer = !ClientUtilities.IsCardInCardList("House", targetCardList);

                                break;
                            }

                            case ("Hotel"):
                            {
                                performTransfer = ClientUtilities.IsCardInCardList("House", targetCardList) && !ClientUtilities.IsCardInCardList("Hotel", targetCardList);

                                break;
                            }
                        }

                    }

                    if ( performTransfer )
                    {
                        RemoveCardFromCardsInPlay(sourceCard, this.Player);
                        targetCardList.Add(sourceCard);
                        DisplayCardsInPlay(this.Player, this.PlayerOneField);
                    }

                    // Update the server's information regarding this player.
                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
                }
            }
        }
        
        // When the user drops a Property Wild Card on an empty space in their field, put the card in its own space.
        private void PlayerOneField_Drop( object sender, DragEventArgs e )
        {
            if ( !e.Handled )
            {
                Button sourceCardButton = e.Data.GetData(typeof(Button)) as Button;
                Card sourceCard = sourceCardButton.Tag as Card;

                if ( PropertyType.Wild == sourceCard.Color )
                {
                    // Remove the card from its previous position on the player's playing field.
                    RemoveCardFromCardsInPlay(sourceCard, this.Player);

                    // Add it to a new space on the field.
                    AddCardToCardsInPlay(sourceCard, this.Player);

                    // Display the updated cards in play.
                    DisplayCardsInPlay(this.Player, this.PlayerOneField);

                    // Update the server's information regarding this player.
                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
                }
            }
        }


        // Clear the InfoBox when the mouse leaves a card button.
        void cardButton_MouseLeave( object sender, MouseEventArgs e )
        {
            Button cardButton = sender as Button;

            if ( null != cardButton.ContextMenu )
            {
                if ( !cardButton.ContextMenu.IsOpen )
                {
                    InfoBox.Children.Clear();
                }
            }
            else
            {
                InfoBox.Children.Clear();
            }
        }

        // Display the card that the mouse is currently hovering over.
        void cardButton_MouseEnter( object sender, MouseEventArgs e )
        {
            Button cardButton = sender as Button;
            
            DisplayCardInInfobox(cardButton.Tag as Card);
        }


        // When the player hovers over his bank, display a break down of his money.
        void cardButtonMoney_MouseEnter( object sender, MouseEventArgs e )
        {
            InfoBox.Children.Clear();
            InfoBox.Children.Add(new MoneyListView(this.Player));
        }

        #endregion

        #region Hand and Field Manipulation


        // Add the money card to the player's CardsInPlay and his MoneyList.
        // This is necessary in order to properly update the UI.
        public void AddMoneyToBank( Card moneyCard, Player player )
        {
            // Add the card to the CardsInPlay and display it on the top of the player's money pile.
            player.CardsInPlay[0].Add(moneyCard);
            AddCardToGrid(moneyCard, PlayerFieldDictionary[player.Name], player, false);

            // Update the player's MoneyList.
            player.MoneyList.Add(moneyCard);
        }

        // Update the value of the IsCurrentTurnOwner boolean.
        public void CheckIfCurrentTurnOwner( Object shouldNotifyUserObject )
        {
            bool shouldNotifyUser = (bool) shouldNotifyUserObject; 
            if ( this.Turn.CurrentTurnOwner == FindPlayerPositionInPlayerList(this.Player.Name) )
            {
                IsCurrentTurnOwner = true;

                // Inform the player that it is his/her turn.
                if ( shouldNotifyUser )
                {
                    this.PlaySound(ResourceList.UriPathTurnDing);

                    var turnNotificationDialog = new MessageDialog(this, string.Empty, "It's your turn!", MessageBoxButton.OK);
                    turnNotificationDialog.ShowDialog();
                }

                // Draw the cards for the current turn owner automatically.
                // In this game, only two cards can generally be drawn at a time. However, if the player has no cards in his hand, he must draw five cards.
                if ( this.Player.CardsInHand.Count > 0 )
                {
                    DrawCards(2);
                }
                else
                {
                    DrawCards(5);
                }
            }
            else
            {
                IsCurrentTurnOwner = false;
            }
        }

        // Update the discard pile card so that it displays the last card in the discard pile.
        public void DisplayUpdatedDiscardPile( Object filler = null )
        {
            // If the discard pile is empty, display nothing in its associated grid.
            if ( 0 == this.DiscardPile.Count )
            {
                DiscardPileGrid.Children.Clear();
            }
            // Otherwise, display only the last card in the discard pile.
            else
            {
                Card discardedCard = this.DiscardPile[this.DiscardPile.Count - 1];
                Button cardButton = ConvertCardToButton(discardedCard);
                DiscardPileGrid.Children.Add(cardButton);

                this.PlaySoundForAction((ActionId)discardedCard.ActionID);
            }
        }

        // Discard a card, sending it to the discard pile and updating the other players' clients.
        // This method will only be called by the player associated with this client.
        private void DiscardCard( Card cardToDiscard )
        {
            if ( null != cardToDiscard )
            {
                // Remove the card from the player's hand.
                RemoveCardFromHand(cardToDiscard);

                // Add the card to the discard pile and update its display.
                this.DiscardPile.Add(cardToDiscard);

                // Update the server's discard pile.
                ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, this.DiscardPile);
            }
        }

        // Wrap a Button around a Card.
        public Button ConvertCardToButton( Card card )
        {
            Button cardButton = new Button();

            cardButton.Content = new Image();
            (cardButton.Content as Image).Source = this.TryFindResource(card.CardImageUriPath) as DrawingImage;
            
            cardButton.Tag = card;
            cardButton.Style = (Style)FindResource("NoChromeButton");
            cardButton.RenderTransform = new TransformGroup();
            cardButton.RenderTransformOrigin = new Point(0.5, 0.5);

            return cardButton;
        }

        // This is used to get the position of a Player in the PlayerList relative
        // to the position of the client's Player object.
        // Source: This method was created by Rolan.
        public int GetRelativePosition( String player1Name, String player2Name )
        {
            int position;
            int player1 = 0;
            int player2 = 0;
            int dif = 0;

            player1 = FindPlayerPositionInPlayerList(player1Name);
            player2 = FindPlayerPositionInPlayerList(player2Name);

            dif = player2 - player1;

            if ( dif < 0 )
            {
                position = dif + PlayerList.Count;
            }
            else
            {
                position = dif;
            }

            return position;

        }

        // Display the cards in the Player's CardsInPlay list.
        public void DisplayCardsInPlay( Player player, Grid field )
        {
            ClearCardsInGrid(field);

            for ( int i = 0; i < player.CardsInPlay.Count; ++i )
            {
                field.Children.Add(CreateCardGrid(i));
                foreach ( Card card in player.CardsInPlay[i] )
                {
                    AddCardToGrid(card, field, player, false, i);
                }
            }
        }

        // Display the cards in the Player's CardsInHand.
        public void DisplayCardsInHand( Player player, Grid field )
        {
            ClearCardsInGrid(field);

            for ( int i = 0; i < player.CardsInHand.Count; ++i )
            {
                field.Children.Add(CreateCardGrid(i));
                AddCardToGrid(player.CardsInHand[i], field, player, true, i);
            }
        }

        // Display the cards of the player's opponents.
        public void DisplayOpponentCards( Object filler = null )
        {
            // If the other players have not been assigned to their respective areas of the client's window,
            // assign them now.
            if ( !HavePlayersBeenAssigned )
            {
                AssignPlayersToGrids();
            }

            foreach ( Player player in PlayerList )
            {
                // Skip the player that represents this client.
                if ( this.Player.Name != player.Name )
                {
                    // Update the display of the opponent's cards in play.
                    DisplayCardsInPlay(player, PlayerFieldDictionary[player.Name]);

                    // Update the field displaying the count of cards in the player's hand.
                    PlayerHandDictionary[player.Name].Tag = "x" + player.CardsInHand.Count;
                }
            }
        }

        // Display cards of the player and the player's opponents.
        public void DisplayAllCards( Object filler = null )
        {
            DisplayOpponentCards();
            DisplayCardsInPlay(this.Player, PlayerOneField);
        }

        // Update the card displayed in the InfoBox.
        public void DisplayCardInInfobox( Card card )
        {
            // Add this card to the InfoBox.
            InfoBox.Children.Clear();
            Button cardButton = ConvertCardToButton(card);

            // This transform is applied in order to display flipped properties properly.
            if ( (cardButton.Tag as Card).Type != CardType.Money )
            {
                TransformCardButton(cardButton, 0, 0);
            }

            InfoBox.Children.Add(cardButton);
        }

        // Display a framework element inside the Infobox.
        public void DisplayElementInInfobox( FrameworkElement element )
        {
            InfoBox.Children.Add(element);
        }

        public void UpdateTurnDisplay( Object filler )
        {
            this.ActionCount.Content = "Actions Remaining: " + this.Turn.ActionsRemaining;

            this.TurnIndicator.Content = this.PlayerList[this.Turn.CurrentTurnOwner].Name + "'s Turn";
        }

        // Resize UI elements so that they are propotional to the size of the window.
        public void ResizeUIElements( Object filler )
        {
            if ( PlayerFieldDictionary.Values != null )
            {
                foreach ( Grid field in PlayerFieldDictionary.Values )
                {
                    foreach ( FrameworkElement element in field.Children )
                    {
                        Grid cardGrid = (Grid)element;
                        for ( int i = 0; i < cardGrid.Children.Count; ++i )
                        {
                            Button cardButton = (Button)cardGrid.Children[i];

                            TransformCardButton(cardButton, i, field.ActualHeight);
                        }
                    }
                }
            }
        }

        // Associate each player in the PlayerList to each grid in the client's window.
        public void AssignPlayersToGrids()
        {
            HavePlayersBeenAssigned = true;

            PlayerFieldDictionary = new Dictionary<String, Grid>();
            PlayerHandDictionary = new Dictionary<String, Grid>();

            foreach ( Player player in PlayerList )
            {
                // Choose a logical position for each player on the client's screen.
                switch ( GetRelativePosition(this.Player.Name, player.Name) )
                {
                    case 0:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerOneField);
                        PlayerHandDictionary.Add(player.Name, PlayerOneHand);
                        break;
                    }
                    case 1:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerTwoField);
                        PlayerHandDictionary.Add(player.Name, PlayerTwoHand);
                        PlayerTwoHand.Visibility = System.Windows.Visibility.Visible;
                        break;
                    }

                    case 2:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerThreeField);
                        PlayerHandDictionary.Add(player.Name, PlayerThreeHand);
                        PlayerThreeHand.Visibility = System.Windows.Visibility.Visible;
                        break;
                    }

                    case 3:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerFourField);
                        PlayerHandDictionary.Add(player.Name, PlayerFourHand);
                        PlayerFourHand.Visibility = System.Windows.Visibility.Visible;
                        break;
                    }

                    case 4:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerFiveField);
                        PlayerHandDictionary.Add(player.Name, PlayerFiveHand);
                        PlayerFiveHand.Visibility = System.Windows.Visibility.Visible;
                        break;
                    }
                }
            }
        }

        // Clear all of the card buttons from a given grid.
        public void ClearCardsInGrid( Grid playerField )
        {
            playerField.Children.Clear();
        }

        // When a card is added to the Player's CardsInPlay, it must be placed in the same list as compatible properties
        // that have already been played. If no compatible properties have been played, a new list is created for the card.
        // This method returns 'true' if it successfully adds a card to the CardsInPlay.
        public bool AddCardToCardsInPlay( Card cardBeingAdded, Player player )
        {
            // Check if any cards currently in play match the color of the card being played (if the card is a property).
            // If it does, lay the card being played over the matching cards.
            switch (cardBeingAdded.Type)
            {
                case CardType.Property:
                {
                    // Before a card is added to the player's CardsInPlay, we must verify that it can be added to the player's CardsInPlay.
                    // If a monopoly of the card's color already exists, then the card cannot be played.
                    List<PropertyType> colorsOfCurrentMonopolies = new List<PropertyType>();

                    foreach ( List<Card> cardList in ClientUtilities.FindMonopolies(player) )
                    {
                        colorsOfCurrentMonopolies.Add(ClientUtilities.GetCardListColor(cardList));
                    }

                    if ( PropertyType.Wild != cardBeingAdded.Color )
                    {
                        // If the card has passed the previous check, add it to the player's CardsInPlay.
                        for ( int i = 1; i < player.CardsInPlay.Count; ++i )
                        {
                            List<Card> cardList = player.CardsInPlay[i];

                            PropertyType cardListColor = ClientUtilities.GetCardListColor(cardList);

                            // If the cardlist is not a monopoly and is compatible with the card being added, add the card to the list.
                            if ( !ClientUtilities.IsCardListMonopoly(cardList) && (cardListColor == cardBeingAdded.Color || PropertyType.Wild == cardListColor) )
                            {
                                cardList.Add(cardBeingAdded);
                                AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false, player.CardsInPlay.IndexOf(cardList));

                                return true;
                            }
                        }
                    }

                    // If this code is reached, the card must not have matched any existing properties.
                    // Create a new list for the card.
                    List<Card> newCardList = new List<Card>();
                    newCardList.Add(cardBeingAdded);
                    player.CardsInPlay.Add(newCardList);
                    AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false);
                    return true;
                }
                // Add the house to an existing monopoly or the hotel to a monopoly that has a house. The card should not be removed from the player's hand
                // unless it is placed in a monopoly.
                case CardType.Enhancement:
                {
                    List<Card> monopoly = (4 == cardBeingAdded.ActionID ) ? ClientUtilities.FindMonopolyWithoutHouse(player) : ClientUtilities.FindMonopolyWithoutHotel(player);
                    if ( null != monopoly && 
                        MessageBoxResult.Yes == MessageBox.Show("Adding a " + cardBeingAdded.Name + " to your monopoly will prevent you from being able to separate any " + 
                                                                "property wild cards from the set unless you have another monopoly that you can move the " + cardBeingAdded.Name + " to. \n\n" +
                                                                "Are you sure you want to do this?", 
                                                                "Are you sure you want to play your " + cardBeingAdded.Name + "?", MessageBoxButton.YesNo) )
                    {
                        monopoly.Add(cardBeingAdded);
                        AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false, player.CardsInPlay.IndexOf(monopoly));

                        return true;
                    }
                    
                    return false;
                }
                case CardType.Money:
                {
                    AddMoneyToBank(cardBeingAdded, player);
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }

        //// I do not remember why I created this method. It is flawed in that it does not save the position of wild cards.
        //// I will leave it commented out for now.
        //// Excluding the money list, removes all cards from the CardsInPlay and re-adds them.
        //public void RefreshCardsInPlay( Player player, Grid playerField )
        //{
        //    List<Card> cards = new List<Card>();
        //    List<Card> moneyList = player.CardsInPlay[0];

        //    // Collect all of the cards from the curent CardsInPlay.
        //    for ( int i = 1; i < player.CardsInPlay.Count; ++i )
        //    {
        //        foreach ( Card card in player.CardsInPlay[i])
        //        {
        //            cards.Add(card);
        //        }
        //    }

        //    // Reset the player's CardsInPlay.
        //    player.CardsInPlay = new List<List<Card>>();

        //    // Add the money list to the new CardsInPlay.
        //    player.CardsInPlay.Add(moneyList);

        //    // Add all of the collected cards to the new CardsInPlay.
        //    foreach ( Card card in cards )
        //    {
        //        AddCardToCardsInPlay(card);
        //    }

        //    // Display the player's refreshed CardsInPlay.
        //    DisplayCardsInPlay(player, playerField);

        //}

        // Remove a card from a player's CardsInPlay. If it is the last card in a list, remove the list as well (unless it is the money list).
        public void RemoveCardFromCardsInPlay( Card cardBeingRemoved, Player player )
        {
            int indexOfList = 0;

            for ( int i = 0; i < player.CardsInPlay.Count; ++i )
            {
                // Determine if the card list contains the card being removed.
                if ( player.CardsInPlay[i].Any(card => card.CardID == cardBeingRemoved.CardID) )
                {
                    // Remove the card.
                    player.CardsInPlay[i] = new List<Card>(player.CardsInPlay[i].Where(card => card.CardID != cardBeingRemoved.CardID));
                    indexOfList = i;
                    break; ;
                }
            }

            // If a card list becomes empty as a result of the previous operation, remove it.
            if ( (0 != indexOfList) && (0 == player.CardsInPlay[indexOfList].Count) )
            {
                player.CardsInPlay.Remove(player.CardsInPlay[indexOfList]);
            }

        }

        // Add a card to a specified grid.
        public void AddCardToGrid( Card cardBeingAdded, Grid grid, Player player, bool isHand, int position = -1 )
        {
            Button cardButton = ConvertCardToButton(cardBeingAdded);

            // For now, this applies to all card buttons (except cardbacks)
            cardButton.MouseEnter += new MouseEventHandler(cardButton_MouseEnter);
            cardButton.MouseLeave += new MouseEventHandler(cardButton_MouseLeave);

            // If a card is being added to the client's hand, attach these events to it and display it.
            if ( isHand && player.Name == this.Player.Name )
            {
                // Create context menu for all cards in hand.
                ContextMenu menu = new ContextMenu();
                cardButton.ContextMenu = menu;

                // If there are no actions left, disable all context menu items except for Discard.
                // All menu options are disabled if it is not the player's turn.
                menu.Opened += ( sender, args ) =>
                {
                    foreach (MenuItem item in menu.Items )
                    {
                        string header = (string)item.Header;
                        item.IsEnabled = isCurrentTurnOwner;
                        if ( ResourceList.AddEnhancementMenuItemHeader == header )
                        {
                            item.IsEnabled &= (4 == cardBeingAdded.ActionID) ? 
                                              (null != ClientUtilities.FindMonopolyWithoutHouse(player)) : 
                                              (null != ClientUtilities.FindMonopolyWithoutHotel(player));
                        }
                        else if ( ResourceList.DiscardMenuItemHeader == header )
                        {
                            item.IsEnabled &= (player.CardsInHand.Count > 7);
                        }
                        else if ( ResourceList.FlipCardMenuItemHeader == header )
                        {
                            item.IsEnabled = true;
                        }
                        else
                        {
                            item.IsEnabled &= (this.Turn.ActionsRemaining > 0);
                        }
                    }
                };

                // If a card is not an action card, there is only one way it can be played.
                if ( CardType.Property == cardBeingAdded.Type || CardType.Money == cardBeingAdded.Type )
                {
                    MenuItem playMenuItem = new MenuItem();
                    playMenuItem.Header = "Play";
                    playMenuItem.Click += ( sender, args ) =>
                    {
                        PlayCardEvent(cardButton, null);
                    };
                    menu.Items.Add(playMenuItem);
                    
                    if ( HasAltColor(cardBeingAdded) )
                    {
                        MenuItem flipMenuItem = new MenuItem();
                        flipMenuItem.Header = ResourceList.FlipCardMenuItemHeader;
                        flipMenuItem.Click += ( sender2, args2 ) =>
                        {
                            // Flip the card, swapping its primary and alternative colors.
                            FlipCard(cardBeingAdded);

                            TransformCardButton(cardButton, 0, 0);

                            DisplayCardInInfobox(cardBeingAdded);
                        };
                        menu.Items.Add(flipMenuItem);
                    }
                }
                // Houses and hotels have unique menu options. Players can add these to existing monopolies or use them as money.
                else if ( CardType.Enhancement == cardBeingAdded.Type )
                {
                    MenuItem playAsActionMenuItem = new MenuItem();
                    playAsActionMenuItem.Header = ResourceList.AddEnhancementMenuItemHeader;
                    playAsActionMenuItem.Click += (sender, args) =>
                    {
                        PlayCardEvent(cardButton, null);
                    };
                    MenuItem playAsMoneyMenuItem = new MenuItem();
                    playAsMoneyMenuItem.Header = "Play as Money";
                    playAsMoneyMenuItem.Click += (sender, args) =>
                    {
                        cardBeingAdded.Type = CardType.Money;
                        PlayCardEvent(cardButton, null);
                    };

                    menu.Items.Add(playAsActionMenuItem);
                    menu.Items.Add(playAsMoneyMenuItem);
                }
                // These apply to all other action cards. Players can play these cards as actions or money.
                else
                {
                    // If the action card is not a "Double the Rent" or "Just Say No", add this option.
                    // (Double the Rent cards are played only with Rent cards, and Just Say No cards
                    // are only used in response to actions).
                    if ( (2 != cardBeingAdded.ActionID) && (1 != cardBeingAdded.ActionID) )
                    {
                        MenuItem playAsActionMenuItem = new MenuItem();
                        playAsActionMenuItem.Header = "Play as Action";
                        playAsActionMenuItem.Click += ( sender, args ) =>
                        {
                            PlayCardEvent(cardButton, null);
                        };
                        menu.Items.Add(playAsActionMenuItem);
                    }

                    MenuItem playAsMoneyMenuItem = new MenuItem();
                    playAsMoneyMenuItem.Header = "Play as Money";
                    playAsMoneyMenuItem.Click += ( sender2, args2 ) =>
                    {
                        cardBeingAdded.Type = CardType.Money;
                        PlayCardEvent(cardButton, null);
                    };

                    menu.Items.Add(playAsMoneyMenuItem);
                }

                // To all cards (regardless of type), add the ability to discard the card.
                MenuItem discardMenuItem = new MenuItem();
                discardMenuItem.Header = ResourceList.DiscardMenuItemHeader;
                discardMenuItem.Click += ( sender, args ) =>
                {
                    if ( MessageBoxResult.Yes == MessageBox.Show("Are you sure you want to discard this card?", "Discard Confirmation", MessageBoxButton.YesNo) )
                    {
                        DiscardCard(cardBeingAdded);
                    }
                };
                menu.Items.Add(discardMenuItem);
            }
            // The card is being added to a player's playing field.
            else
            {
                // Play the card in a certain way depending on its type (i.e. property, money, or action).
                switch ( cardBeingAdded.Type )
                {
                    case CardType.Enhancement:
                    case CardType.Property:
                    {
                        // Create context menu that allows the user to reorder properties.
                        // This if statement is required to prevent players from seeing the context menu of other players' cards in play.
                        if ( player.Name == this.Player.Name )
                        {
                            // Create the appropriate context menu.
                            ContextMenu menu = new ContextMenu();
                            cardButton.ContextMenu = menu;

                            MenuItem forwardMenuItem = new MenuItem();
                            forwardMenuItem.Header = "Move Forward";
                            forwardMenuItem.Click += ( sender, args ) =>
                            {
                                MoveCardInList(cardBeingAdded, 1);
                            };
                            MenuItem backwardMenuItem = new MenuItem();
                            backwardMenuItem.Header = "Move Backward";
                            backwardMenuItem.Click += ( sender, args ) =>
                            {
                                MoveCardInList(cardBeingAdded, -1);
                            };

                            // If it is a two-color property card, allow the player to flip it.
                            if ( HasAltColor(cardBeingAdded) )
                            {
                                MenuItem flipMenuItem = new MenuItem();
                                flipMenuItem.Header = ResourceList.FlipCardMenuItemHeader;
                                flipMenuItem.Click += ( sender, args ) =>
                                {
                                    // Check to see if the flipped card can be added to the player's CardsInPlay.
                                    // If it cannot, do not do anything.
                                    List<PropertyType> colorsOfCurrentMonopolies = new List<PropertyType>();
                                    foreach ( List<Card> cardList in ClientUtilities.FindMonopolies(Player) )
                                    {
                                        colorsOfCurrentMonopolies.Add(ClientUtilities.GetCardListColor(cardList));
                                    }
                                    if ( colorsOfCurrentMonopolies.Contains(cardBeingAdded.AltColor) )
                                    {
                                        MessageBox.Show("You cannot flip this property because you already have a " + cardBeingAdded.AltColor + " monopoly on the field.");
                                        return;
                                    }

                                    // Flip the card, swapping its primary and alternative colors.
                                    FlipCard(cardBeingAdded);

                                    // Remove the card and re-add it to the Player's CardsInPlay.
                                    RemoveCardFromCardsInPlay(cardBeingAdded, this.Player);
                                    AddCardToCardsInPlay(cardBeingAdded, this.Player);

                                    // Display the updated CardsInPlay.
                                    DisplayCardsInPlay(player, grid);

                                    // Update the server.
                                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
                                };

                                menu.Items.Add(flipMenuItem);
                            }

                            // If the card is a Property Wild Card, add an option that allows the user to place it on its own space.
                            else if ( PropertyType.Wild == cardBeingAdded.Color )
                            {
                                MenuItem separateMenuItem = new MenuItem();
                                separateMenuItem.Header = ResourceList.SeparateWildCardMenuItemHeader;
                                separateMenuItem.Click += ( sender, args ) =>
                                {
                                    // Remove the card from its previous position on the player's playing field.
                                    RemoveCardFromCardsInPlay(cardBeingAdded, this.Player);

                                    // Add it to a new space on the field.
                                    AddCardToCardsInPlay(cardBeingAdded, this.Player);

                                    // Display the updated cards in play.
                                    DisplayCardsInPlay(this.Player, this.PlayerOneField);

                                    // Update the server's information regarding this player.
                                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
                                };

                                menu.Items.Add(separateMenuItem);
                            }

                            menu.Items.Add(forwardMenuItem);
                            menu.Items.Add(backwardMenuItem);

                            // When the menu is opened, disable all options if it is not the player's turn.
                            menu.Opened += ( sender, args ) =>
                            {
                                foreach ( MenuItem item in menu.Items )
                                {
                                    item.IsEnabled = this.IsCurrentTurnOwner;

                                    string header = (string)item.Header;
                                    if ( ResourceList.FlipCardMenuItemHeader == header || ResourceList.SeparateWildCardMenuItemHeader == header )
                                    {
                                        List<Card> cardListContainingCard = FindListContainingCard(cardBeingAdded);
                                        item.IsEnabled &= !(cardListContainingCard.Any(card => (int)ActionId.House == card.ActionID));
                                    }
                                }
                            };

                            // Add a drop event to receive property cards that are being placed in this card's group.
                            cardButton.Drop += new DragEventHandler(cardButton_Drop);
                            cardButton.AllowDrop = true;

                            // Allow cards to be dragged and dropped onto other groups.
                            cardButton.PreviewMouseMove += new MouseEventHandler(cardButton_PreviewMouseMove);
                        }

                        // Add the property card to the grid in the specified position.
                        if ( -1 != position )
                        {
                            Grid cardGrid = (grid.Children[position] as Grid);

                            // Lay properties of compatible colors on top of each other (offset vertically).
                            TransformCardButton(cardButton, cardGrid.Children.Count, grid.ActualHeight);

                            cardGrid.Children.Add(cardButton);
                            Grid.SetColumn(cardButton, 1);
                            return;
                        }

                        break;
                    }

                    case CardType.Money:
                    {
                        // Each player should only be to see the breakdown of his own money pile
                        if ( this.Player == player )
                        {
                            cardButton.MouseEnter += new MouseEventHandler(cardButtonMoney_MouseEnter);
                        }

                        // Play money cards horizontally.
                        TransformCardButton(cardButton, 0, 0);

                        (grid.Children[0] as Grid).Children.Add(cardButton);
                        Grid.SetColumn(cardButton, 1);
                        return;
                    }

                    // This case adds played Action cards to the Action Card/Discard pile.
                    case CardType.Action:
                    {
                        grid.Children.Add(cardButton);

                        return;
                    }
                }
            }

            // Wrap the card inside a grid in order to insert spaces between the displayed the cards.
            Grid cardGridWrapper = CreateCardGrid();
            cardGridWrapper.Children.Add(cardButton);
            cardGridWrapper.Tag = cardButton;
            Grid.SetColumn(cardButton, 1);

            // Add the card (within its grid wrapper) to the next available position in the specified grid.
            grid.Children.Add(cardGridWrapper);

            if ( isHand )
            {
                Grid.SetColumn(cardGridWrapper, grid.Children.Count - 1);
            }
            else
            {
                TransformCardButton(cardButton, 0, 0);
                if ( cardBeingAdded.Type == CardType.Property )
                {
                    Grid.SetColumn(cardGridWrapper, grid.Children.Count - 1);
                }
                else if ( cardBeingAdded.Type == CardType.Money )
                {
                    Grid.SetColumn(cardGridWrapper, 0);
                }
            }
        }

        // Create a grid used to create margins for a card being displayed.
        public Grid CreateCardGrid( int columnIndex = 0 )
        {
            // Wrap the card inside a grid in order to insert spaces between the displayed the cards.
            Grid cardGrid = new Grid();

            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition col2 = new ColumnDefinition();
            col2.Width = new GridLength(16, GridUnitType.Star);
            ColumnDefinition col3 = new ColumnDefinition();
            col3.Width = new GridLength(1, GridUnitType.Star);

            cardGrid.ColumnDefinitions.Add(col1);
            cardGrid.ColumnDefinitions.Add(col2);
            cardGrid.ColumnDefinitions.Add(col3);

            Grid.SetColumn(cardGrid, columnIndex);

            return cardGrid;
        }

        // Scale, rotate, or apply any other transform to a card before displaying it.
        public void TransformCardButton( Button cardButton, int numberOfMatchingCards, double gridHeight )
        {
            Card card = cardButton.Tag as Card;

            switch ( card.Type )
            {
                case CardType.Enhancement:
                case CardType.Property:
                {
                    TransformGroup transformGroup = (cardButton.RenderTransform as TransformGroup);

                    // Flip or unflip a two-color property.
                    if ( HasAltColor(cardButton.Tag as Card) )
                    {
                        RotateTransform horizontalTransform = new RotateTransform();

                        // First remove any rotate transform that may have been applied.
                        RemoveTransformTypeFromGroup(horizontalTransform.GetType(), transformGroup);

                        // Flip properties that are supposed to be flipped.
                        if ( card.IsFlipped )
                        {
                            horizontalTransform.Angle = 180;
                            //cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                            transformGroup.Children.Add(horizontalTransform);
                            //transformGroup.Children.Add(horizontalTransform);
                        }
                    }

                    // Lay properties of compatible colors on top of each other (offset vertically). Before doing this, remove the existed translate transform.
                    TranslateTransform translateTransform = new TranslateTransform();
                    RemoveTransformTypeFromGroup(translateTransform.GetType(), transformGroup);
                    translateTransform.Y = (numberOfMatchingCards * .10) * gridHeight;
                    transformGroup.Children.Add(translateTransform);
                    //transformGroup.Children.Add(myTranslateTransform);

                    //cardButton.RenderTransform = transformGroup;

                    break;
                }

                case CardType.Money:
                {
                    // Play money cards horizontally.
                    RotateTransform horizontalTransform = new RotateTransform();
                    horizontalTransform.Angle = -90;

                    cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                    cardButton.LayoutTransform = horizontalTransform;
                    break;
                }

                case CardType.Action:
                {
                    break;
                }
            }
        }

        // Remove a card from the player's hand.
        // This should only be called on the player associated with this client.
        public void RemoveCardFromHand( Card cardtoRemove )
        {
            // Remove the card from the grid displaying the player's hand.
            // This must be done before removing the card from the list.
            RemoveCardButtonFromHand((PlayerOneHand.Children[this.Player.CardsInHand.FindIndex(card => cardtoRemove.CardID == card.CardID)] as Grid).Tag as Button);

            // Remove the card from the player's CardsInHand list.
            this.Player.CardsInHand = new List<Card>(this.Player.CardsInHand.Where(card => card.CardID != cardtoRemove.CardID));

            // Update the server's information regarding this player.
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
        }
        
        // Remove a card (given its Button wrapper) from the player's hand.
        public void RemoveCardButtonFromHand( Button cardButton )
        {
            // Get the index of the card button.
            int buttonIndex = Grid.GetColumn(cardButton.Parent as Grid);

            // Remove the card's button from the PlayerOneHand grid.
            PlayerOneHand.Children.RemoveAt(buttonIndex);

            // Shift all subsequential card buttons to the left.
            for ( int i = buttonIndex; i < PlayerOneHand.Children.Count; ++i )
            {
                Grid.SetColumn(PlayerOneHand.Children[i], i);
            }
        }

        // Remove a type of transform from a given transform group.
        public bool RemoveTransformTypeFromGroup( Type transformType, TransformGroup transformGroup )
        {
            foreach ( Transform transform in transformGroup.Children )
            {
                if (transform.GetType() == transformType )
                {
                    return transformGroup.Children.Remove(transform);
                }
            }

            return false;
        }

        // Draw a given amount of cards from the Deck (the cards are placed in the Player's hand).
        private void DrawCards( int numberOfCards )
        {
            //// Reset the Deck.
            //Deck = null;
            //ServerUtilities.SendMessage(Client, Datatype.RequestDeck);

            //// Do not continue until the updated Deck is received from the server.
            //while ( Deck == null ) ;

            if ( null != this.Deck )
            {
                // Remove the given number of cards from the top of the Deck and add them to the Player's hand.
                for ( int i = 0; i < numberOfCards; ++i )
                {
                    // If the deck is empty, transfer all the cards from the discard pile to the deck and shuffle.
                    if ( this.Deck.CardList.Count == 0 )
                    {
                        if ( this.DiscardPile.Count != 0 )
                        {
                            this.Deck = new Deck(DiscardPile, true);
                            this.DiscardPile = new List<Card>();

                            ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, this.DiscardPile);
                        }
                        else
                        {
                            // ROBIN TODO: Should send a Game Over message to players because there are no cards to draw.
                            return;
                        }
                    }

                    Card drawnCard = this.Deck.CardList[0];

                    this.Player.CardsInHand.Add(drawnCard);
                    AddCardToGrid(drawnCard, PlayerOneHand, this.Player, true);
                    this.Deck.CardList.Remove(drawnCard);
                }


                // Send the updated Deck and Player to the server.
                ServerUtilities.SendMessage(Client, Datatype.UpdateDeck, Deck);
                ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
            }
        }

        // Shift the position of a card in play either forward or backward.
        public void MoveCardInList( Card cardBeingMoved, int numberOfSpaces )
        {
            foreach ( List<Card> cardList in Player.CardsInPlay )
            {
                for ( int i = 0; i < cardList.Count; ++i )
                {
                    if ( cardList[i] == cardBeingMoved )
                    {
                        MoveItemInList<Card>(cardList, i, i + numberOfSpaces);
                        DisplayCardsInPlay(Player, PlayerOneField);

                        // Update the server.
                        ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
                        return;
                    }
                }
            }
        }

        // Swap a card's Color and AltColor properties and toggle its IsFlipped property.
        public void FlipCard( Card card )
        {
            PropertyType oldColor = card.Color;
            card.Color = card.AltColor;
            card.AltColor = oldColor;
            card.IsFlipped = !card.IsFlipped;
        }

        #endregion

        #region Action Card Handling

        // Perform a specific action related to the given action card.
        private bool HandleAction( Card actionCard )
        {
            if ( CardType.Action == actionCard.Type )
            {
                if ( 0 == actionCard.ActionID )
                {
                    // "Pass Go" case.
                    DrawCards(2);
                    DiscardCard(actionCard);

                    return true;
                }
                else if ( 5 <= actionCard.ActionID && 7 >= actionCard.ActionID )
                {
                    // Steal property
                    return StealProperty(actionCard);
                }
                else
                {
                    // The card must be a rent card.
                    return CollectRent(actionCard);
                }
            }

            return false;
        }

        // Steal a property or set of properties from a player.
        private bool StealProperty( Card stealCard )
        {
            // Display the list of players.
            PlayerSelectionDialog dialog = new PlayerSelectionDialog(this.Player.Name, PlayerList);
            if ( dialog.enoughPlayers && true == dialog.ShowDialog() )
            {
                TheftType theftType = (TheftType)(stealCard.ActionID);

                // Verify that both players meet the required criteria for this steal card to be used. If they don't, cancel the action.
                // Perhaps prevent Dealbreaker if thief has color from selected monopoly
                switch ( theftType )
                {
                    case TheftType.Dealbreaker:
                    {
                        // If the other player does not have a monopoly, inform the thief and cancel the action.
                        if ( !dialog.SelectedPlayer.CardsInPlay.Any(cardGroup => ClientUtilities.IsCardListMonopoly(cardGroup)) )
                        {
                            MessageBox.Show(dialog.SelectedPlayer.Name + " does not have any monopolies to steal.", "Cannot Perform Deal");
                            return false;
                        }

                        break;
                    }

                    case TheftType.ForcedDeal:
                    {
                        // Both players need to have at least one property in order for the thief to perform a Forced Deal.
                        if ( !((this.Player.CardsInPlay.Skip(1).Count() > 0) && (dialog.SelectedPlayer.CardsInPlay.Skip(1).Count() > 0)) )
                        {
                            MessageBox.Show("Both players need to have at least one property in order to perform a Forced Deal.", "Cannot Perform Deal");
                            return false;
                        }

                        break;
                    }

                    case TheftType.SlyDeal:
                    {
                        // The selected player needs to have at least one property in order for the thief to perform a Sly Deal.
                        if ( !(dialog.SelectedPlayer.CardsInPlay.Skip(1).Count() > 0) )
                        {
                            MessageBox.Show(dialog.SelectedPlayer.Name + " does not have any properties to steal.", "Cannot Perform Deal");
                            return false;
                        }

                        break;
                    }                    
                }

                // Display the property theft window.
                PropertyTheftWindow propertyTheftWindow = new PropertyTheftWindow(dialog.SelectedPlayer, this.Player, stealCard.ActionID);
                bool isDealBreaker = (TheftType.Dealbreaker == theftType);

                if ( true == propertyTheftWindow.ShowDialog() )
                {
                    // Discard the action card and send the Theft Request to the victim. The victim has a chance to say "No" if he has a "Just Say No" card.
                    DiscardCard(stealCard);
                    
                    this.LastTheftRequest = new ActionData.TheftRequest(this.Player.Name, propertyTheftWindow.Victim.Name, stealCard.ActionID, propertyTheftWindow.PropertyToGive, propertyTheftWindow.PropertiesToTake);
                    ServerUtilities.SendMessage(Client, Datatype.RequestTheft, this.LastTheftRequest);

                    // Display a wait message until the victim has responded to the request.
                    WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting for victim to respond...");
                    WaitMessage.ShowDialog();                    
                }
                else
                {
                    // If window was closed without stealing, cancel the action.
                    return false;
                }
            }
            else
            {
                // If a player was not selected, cancel the action.
                return false;                
            }

            return true;
        }

        // Collect rent from the other players.
        private bool CollectRent( Card rentCard )
        {
            int amountToCollect = 0;
            bool rentDoubled = false;
            Card doubleRentCard = null;

            // Only one player is targeted for a "Debt Collector" or a Wild Rent.
            bool targetOnePlayer = (9 == rentCard.ActionID || 10 == rentCard.ActionID);
            
            // The amount of money collected through these actions is not affected by properties or "Double the Rent".
            if ( 8 == rentCard.ActionID || 9 == rentCard.ActionID )
            {
                switch ( rentCard.ActionID )
                {
                    // If it is an "It's My Birthday", collect a certain amount.
                    case 8:
                    {
                        amountToCollect = Card.BIRTHDAY_AMOUNT;
                        break;
                    }

                    // If it is a "Debt Collector", collect a certain amount.
                    case 9:
                    {
                        amountToCollect = Card.DEBT_AMOUNT;
                        break;
                    }
                }
            }
            // The rent card must be a property rent card.
            else
            {
                List<List<Card>> matchingPropertyGroups = new List<List<Card>>();

                // First, determine which lists on the player's field correpond to the rent card.
                // Skip the money list.
                foreach ( List<Card> cardList in this.Player.CardsInPlay.Skip(1) )
                {
                    PropertyType cardListColor = ClientUtilities.GetCardListColor(cardList);
                    if ( (PropertyType.Wild == rentCard.Color) || (cardListColor == rentCard.Color) || (cardListColor == rentCard.AltColor) )
                    {
                        matchingPropertyGroups.Add(cardList);
                    }
                }

                // If the player will not get any money from playing the rent, prevent him/her from performing the action.
                if ( 0 == matchingPropertyGroups.Count )
                {
                    MessageBoxResult result = MessageBox.Show("You do not have " + rentCard.Color.ToString() + " or " + rentCard.AltColor.ToString() + " properties. You cannot perform this action.",
                                    "Invalid Action",
                                    MessageBoxButton.OK);

                    return false;
                }

                // Determine the maximum amount of money the player can make from the rent.
                foreach ( List<Card> cardList in matchingPropertyGroups )
                {
                    // Determine the number of properties in the set.
                    Card house = cardList.FirstOrDefault(card => card.Name == "House");
                    Card hotel = cardList.FirstOrDefault(card => card.Name == "Hotel");
                    int numProperties = cardList.Count - ( (null != house) ? (1) : (0) ) - ( (null != hotel) ? (1) : (0) );

                    // Calculate the total value of the set.
                    int totalValue = ClientUtilities.RentData[ClientUtilities.GetCardListColor(cardList)][numProperties] + ((null != house) ? (house.Value) : (0)) + ((null != hotel) ? (hotel.Value) : (0));

                    if ( totalValue > amountToCollect )
                    {
                        amountToCollect = totalValue;
                    }
                }

                // If the player has a "Double the Rent" card and at least two actions remaining, ask if he would like to use it.
                doubleRentCard = FindActionCardInList(this.Player.CardsInHand, 1);

                if ( (null != doubleRentCard) && (this.Turn.ActionsRemaining >= 2) )
                {
                    MessageBoxResult result = MessageBox.Show("You have a " + doubleRentCard.Name + " card. Would you like to apply it to this rent?",
                                    "Are you sure?",
                                    MessageBoxButton.YesNo);

                    rentDoubled = (MessageBoxResult.Yes == result);
                }
            }

            // Create the list of players receiving the rent request.
            List<Player> rentees = null;

            if (targetOnePlayer)
            {
                // Prevent the renter from performing any action until all rentees have paid their rent.
                NumberOfRentees = 1;

                // Display the list of players.
                PlayerSelectionDialog dialog = new PlayerSelectionDialog(this.Player.Name, PlayerList);
                if ( dialog.enoughPlayers && true == dialog.ShowDialog() )
                {
                    // Send the rent request to the selected player.
                    rentees = new List<Player>() { dialog.SelectedPlayer };
                }
                else
                {
                    // If a player was not selected, cancel the action.
                    return false;
                }
            }
            else
            {
                // Prevent the renter from performing any action until all rentees have paid their rent.
                NumberOfRentees = PlayerList.Count - 1;

                // Send a rent request to all players except for the renter.
                rentees = new List<Player>(this.PlayerList.Where(player => player.Name != this.Player.Name));
            }

            // Discard the action card and send the message to the server.
            DiscardCard(rentCard);

            if ( rentDoubled )
            {
                // Remove the card from the player's hand and add it to the discard pile.
                DiscardCard(doubleRentCard);

                // Update the number of actions.
                this.Turn.ActionsRemaining--;
            }

            this.LastRentRequest = new ActionData.RentRequest(this.Player.Name, rentees, amountToCollect, rentDoubled);
            ServerUtilities.SendMessage(Client, Datatype.RequestRent, this.LastRentRequest);

            // Display a messagebox informing the renter that he cannot do anything until all rentees have paid their rent.
            // ROBIN TODO: Show some sort of progress bar.
            WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting for rentees to pay rent...");
            WaitMessage.ShowDialog();

            return true;
        }

        // The Just Say No is a special card; it is played in several places and thus deserves its own method.
        private void PlayJustSayNo( Card justSayNoCard )
        {
            // Remove the Just Say No from the victim's hand and add it to the discard pile.
            DiscardCard(justSayNoCard);

            // Update the server with the current version of this player.
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
            ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, this.DiscardPile);
        }

        // Display the rent window.
        private void DisplayRentWindow( Object request )
        {
            ActionData.RentRequest rentRequest = (ActionData.RentRequest)request;
            String renterName = rentRequest.RenterName;
            List<Card> payment = new List<Card>();
            bool acceptedDeal = false;

            RentWindow rentWindow = new RentWindow(this.Player, renterName, rentRequest.RentAmount, rentRequest.IsDoubled);

            // Proceed only if the rentee accepted the deal.
            if ( true == rentWindow.ShowDialog() )
            {
                acceptedDeal = true;

                // Retrieve the list of cards that the rentee selected as payment.
                payment = rentWindow.Payment.ToList<Card>();             
   
                // Retrieve the player object representing the renter.
                Player renter = this.PlayerList.Find(player => player.Name == rentRequest.RenterName);

                // Remove the cards of the payment from the rentee.
                foreach ( Card card in payment )
                {
                    RemoveCardFromCardsInPlay(card, this.Player);

                    // If a money card is used as payment, remove it from the MoneyList.
                    if ( CardType.Money == card.Type )
                    {
                        this.Player.MoneyList.Remove(card);
                    }
                }

                // If there are any card lists that contain an enhancement card but are not monopolies, then place the enhancement card(s) in the player's bank.
                bool enhancementsWereConverted = false;
                List<List<Card>> nonMonopolies = this.Player.CardsInPlay.Where(cardList => !ClientUtilities.IsCardListMonopoly(cardList)).ToList<List<Card>>();
                foreach ( List<Card> cardList in nonMonopolies )
                {
                    List<Card> enhancements = cardList.Where(card => card.Type == CardType.Enhancement).ToList<Card>();
                    foreach (Card enhancement in enhancements)
                    {
                        RemoveCardFromCardsInPlay(enhancement, this.Player);
                        enhancement.Type = CardType.Money;
                        AddCardToCardsInPlay(enhancement, this.Player);

                        enhancementsWereConverted = true;
                    }                 
                }
                
                // Display this player's updated CardsInPlay.
                DisplayCardsInPlay(this.Player, PlayerOneField);

                if ( enhancementsWereConverted )
                {
                    MessageBox.Show("The Houses/Hotels on any full sets that you needed to break up were converted to money and placed in your bank.");
                }
            }
            else
            {
                // Remove the Just Say No from the victim's hand and add it to the discard pile.
                Card justSayNo = this.Player.CardsInHand.FirstOrDefault(card => 2 == card.ActionID);
                if ( null != justSayNo )
                {
                    PlayJustSayNo(justSayNo);
                }
            }

            // Send the updated rentee to the server and a RentResponse to the renter.
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
            ServerUtilities.SendMessage(Client, Datatype.GiveRent, new ActionData.RentResponse(renterName, this.Player.Name, payment, acceptedDeal));
        }

        // Process a rent response for the renter.
        private void ProcessReceivedRent( Object response )
        {
            ActionData.RentResponse rentResponse = (ActionData.RentResponse)response;

            // Verify that there still exists renters who have not paid their rent.
            if ( this.NumberOfRentees > 0 )
            {
                if ( rentResponse.AcceptedDeal )
                {
                    this.AssetsReceived.AddRange(rentResponse.AssetsGiven);
                }
                else
                {
                    string message = rentResponse.RenteeName + " rejected your rent request with a \"Just Say No!\".";

                    Card justSayNo = this.Player.CardsInHand.FirstOrDefault(card => 2 == card.ActionID);
                    // If the renter has his own Just Say No, ask the renter if he wants to use it.
                    // If yes, send the rent request again.
                    bool playerWantsToUseJustSayNo = ClientUtilities.AskPlayerAboutJustSayNo("Rest Request Rejected", message, playerHasJustSayNo: null != justSayNo);
                    if ( playerWantsToUseJustSayNo )
                    {
                        // By the time the renter presses "Yes", he may have already used all of his Just Say No cards. Verify that he still have one before moving on.
                        justSayNo = this.Player.CardsInHand.FirstOrDefault(card => 2 == card.ActionID);

                        if ( null != justSayNo )
                        {
                            // Update the rent number.
                            this.NumberOfRentees++;

                            PlayJustSayNo(justSayNo);
                            this.LastRentRequest.Rentees = new List<Player>() { this.PlayerList.First(player => player.Name == rentResponse.RenteeName) };
                            ServerUtilities.SendMessage(Client, Datatype.RequestRent, this.LastRentRequest);
                        }
                        else
                        {
                            MessageBox.Show("You have already used all of your \"Just Say No!\" cards.");
                        }
                    }
                }

                // Update the number of remaining rentees.
                this.NumberOfRentees--;

                // If all rentees have paid their rent, then add the cards from the AssetsReceived to the player's CardsInPlay.
                if ( 0 == this.NumberOfRentees )
                {
                    StringBuilder assetsSummary = new StringBuilder();
                    foreach ( Card card in this.AssetsReceived )
                    {
                        AddCardToCardsInPlay(card, this.Player);
                        assetsSummary.Append(card.Name + " (" + (CardType.Property == card.Type ? (card.Color + " ") : string.Empty) + card.Type + ")\n");
                    }

                    // Clear the list of AssetsReceived.
                    AssetsReceived.Clear();

                    // Send the updated player to the server.
                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);

                    // Update the wait dialog.
                    if ( null != WaitMessage )
                    {
                        WaitMessage.CloseWindow = true;
                    }

                    // Show message dialog with summary of assets received.
                    MessageBox.Show("You received " + (string.IsNullOrWhiteSpace(assetsSummary.ToString()) ? "no assets." : "the following assets:\n" + assetsSummary.ToString()), "Assets Received");
                }
            }
            else
            {
                throw new Exception("Rent Responses out of sync");
            }
            
        }

        private void ProcessTheftRequest( Object request )
        {
            ActionData.TheftRequest theftRequest = (ActionData.TheftRequest)request;

            string message = theftRequest.ThiefName + " has played a " + ((TheftType)theftRequest.ActionID).ToString() + " against you.";

            // By default, use the name of the first property in the list.
            string nameOfPropertyToTake = theftRequest.PropertiesToTake[0].Name;            

            switch ( (TheftType)theftRequest.ActionID )
            {
                case TheftType.Dealbreaker:
                {
                    // If a Dealbreaker was used, use the name of the monopoly's color.
                    nameOfPropertyToTake = ClientUtilities.GetCardListColor(theftRequest.PropertiesToTake).ToString();

                    message += "\nThis player would like to take your " + nameOfPropertyToTake + " monopoly.";
                    break;
                }

                case TheftType.ForcedDeal:
                {
                    message += "\nThis player would like to trade " + theftRequest.PropertyToGive.Name + " for " + nameOfPropertyToTake + ".";
                    break;
                }

                case TheftType.SlyDeal:
                {
                    message += "\nThis player would like to steal " + nameOfPropertyToTake + ".";
                    break;
                }
            }

            // Display the message box to the victim.
            Card justSayNo = this.Player.CardsInHand.FirstOrDefault(card => 2 == card.ActionID);
            bool playerWantsToUseJustSayNo = ClientUtilities.AskPlayerAboutJustSayNo("Theft Request", message, playerHasJustSayNo: null != justSayNo);

            if ( playerWantsToUseJustSayNo )
            {
                // Remove the Just Say No from the victim's hand.
                PlayJustSayNo(justSayNo);               
            }
            else
            {
                // If there is a property to give to the victim, give the property to the victim.
                if ( String.Empty != theftRequest.PropertyToGive.Name )
                {
                    AddCardToCardsInPlay(theftRequest.PropertyToGive, this.Player);
                }

                // Remove the properties to take from the victim's cards in play.
                foreach ( Card card in theftRequest.PropertiesToTake )
                {
                    RemoveCardFromCardsInPlay(card, this.Player);
                }
            }

            // Update the server with this player's current state.
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);

            // Send the theft reply to the thief.
            ServerUtilities.SendMessage(Client, Datatype.ReplyToTheft, new ActionData.TheftResponse(theftRequest.ThiefName, this.Player.Name, !playerWantsToUseJustSayNo));
        }

        private void ProcessTheftResponse( Object response )
        {
            ActionData.TheftResponse theftResponse = (ActionData.TheftResponse)response;

            // Update the wait dialog.
            if ( null != WaitMessage )
            {
                WaitMessage.CloseWindow = true;
            }

            // This is called once the thief has received a reply from the victim.
            if ( theftResponse.AcceptedDeal )
            {
                // Transfer the PropertyToGive to the victim.
                if ( String.Empty != this.LastTheftRequest.PropertyToGive.Name )
                {
                    RemoveCardFromCardsInPlay(this.LastTheftRequest.PropertyToGive, this.Player);
                }

                // Add the card(s) to the thief's cards in play.
                if ( (TheftType)(this.LastTheftRequest.ActionID) == TheftType.Dealbreaker )
                {
                    this.Player.CardsInPlay.Add(this.LastTheftRequest.PropertiesToTake);
                }
                else
                {
                    AddCardToCardsInPlay(this.LastTheftRequest.PropertiesToTake[0], this.Player);
                }


                // Update the server with the current version of this player (the thief).
                ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
            }
            else
            {
                string message = this.LastTheftRequest.VictimName + " rejected your deal with a \"Just Say No!\".";

                // If the thief chooses to play a Just Say No, send the theft request again.
                Card justSayNo = this.Player.CardsInHand.FirstOrDefault(card => 2 == card.ActionID);
                bool playerWantsToUseJustSayNo = ClientUtilities.AskPlayerAboutJustSayNo("Deal Rejected", message, playerHasJustSayNo: null != justSayNo);
                if ( playerWantsToUseJustSayNo )
                {
                    PlayJustSayNo(justSayNo);
                    ServerUtilities.SendMessage(Client, Datatype.RequestTheft, this.LastTheftRequest);

                    // Display a wait message until the victim has responded to the request.
                    WaitMessage = new MessageDialog(this, ResourceList.PleaseWaitWindowTitle, "Waiting for victim to respond...");
                    WaitMessage.ShowDialog();
                }
            }

            // Reset the value of the last theft request.
            this.LastTheftRequest = null;
        }

        #endregion

        #region Sound and Animation

        private void PlaySoundForAction(Object actionId)
        {
            this.PlaySoundForAction((ActionId)actionId);
        }

        private void PlaySoundForAction(ActionId actionId)
        {
            switch ( actionId )
            {
                case ActionId.ItsMyBirthday:
                {
                    this.PlaySound(ResourceList.UriPathItsMyBirthday);
                    break;
                }

                case ActionId.JustSayNo:
                {
                    this.PlaySound(ResourceList.UriPathNoSound);
                    break;
                }
            }               
        }

        private void PlaySound( Object filler )
        {
            string uriPath = filler as string;
            Stream resourceStream = Application.GetResourceStream(new Uri(uriPath)).Stream;
            this.SoundPlayer.Stream = resourceStream;
                        
            this.SoundPlayer.Play();
        }

        private ColorAnimation CreateEndTurnButtonAnimation()
        {
            ColorAnimation colorAnimation = new ColorAnimation();
            colorAnimation.To = Colors.LawnGreen;
            colorAnimation.RepeatBehavior = RepeatBehavior.Forever;
            colorAnimation.Duration = new Duration(TimeSpan.FromSeconds(1));
            colorAnimation.AutoReverse = true;

            return colorAnimation;
        }

        private void AnimateEndTurnButton()
        {
            this.EndTurnButton.Background = new SolidColorBrush(Colors.Gray);
            this.EndTurnButton.Background.BeginAnimation(SolidColorBrush.ColorProperty, this.EndTurnButtonAnimation);
        }

        #endregion

        #region Miscellaneous

        // Create a new thread to run a function that cannot be run on the same thread invoking CreateNewThread().
        public Thread CreateNewThread( Action<Object> action, object data = null, string name = "" )
        {
            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, action, data); };
            Thread newThread = new Thread(start);

            if ( !String.IsNullOrEmpty(name) )
            {
                newThread.Name = name;
            }

            newThread.Start();

            return newThread;
        }

        // Shift the position of an item in a list.
        public void MoveItemInList<T>( IList<T> list, int oldIndex, int newIndex )
        {
            var item = list[oldIndex];

            list.RemoveAt(oldIndex);

            if ( newIndex < list.Count && newIndex >= 0 )
            {
                list.Insert(newIndex, item);
            }
            else
            {
                list.Add(item);
            }
        }

        // Given a card, find the card list in the Player's CardsInPlay that contains it.
        public List<Card> FindListContainingCard( Card cardBeingFound )
        {
            foreach ( List<Card> cardList in Player.CardsInPlay )
            {
                foreach ( Card card in cardList )
                {
                    if ( card.CardID == cardBeingFound.CardID )
                    {
                        return cardList;
                    }
                }
            }

            return null;
        }

        public Card FindActionCardInList( List<Card> cardList, int actionID )
        {
            foreach ( Card card in cardList )
            {
                if ( actionID == card.ActionID )
                {
                    return card;
                }
            }

            return null;
        }

        // Given a player's name, find the position of the player in the PlayerList (there is only one PlayerList).
        private int FindPlayerPositionInPlayerList( string playerName )
        {
            for ( int i = 0; i < PlayerList.Count; ++i )
            {
                if ( PlayerList[i].Name == playerName )
                {
                    return i;
                }
            }

            return -1;
        }

        // Determine if a given card is a two-color property (aka 'Property Wild Card').
        public bool HasAltColor( Card card )
        {
            return (card.AltColor != PropertyType.None);
        }

        // This returns the number of a type of card (i.e. money, action, or property) found within
        // a provided list of card lists.
        private int CountOfCardTypeInList( CardType cardType, List<List<Card>> cardGroupList )
        {
            int count = 0;

            foreach ( List<Card> cardGroup in cardGroupList )
            {
                foreach ( Card card in cardGroup )
                {
                    if ( card.Type == cardType )
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Determine the number of a certain card type (i.e. money, action, or property) in a given grid.
        private int CountOfCardTypeInGrid( CardType cardType, Grid grid )
        {
            int count = 0;

            foreach ( FrameworkElement element in grid.Children )
            {
                Grid cardGrid = element as Grid;

                foreach ( FrameworkElement innerElement in cardGrid.Children )
                {
                    Button existingCardButton = (Button)innerElement;
                    Card cardInGrid = existingCardButton.Tag as Card;

                    if ( cardInGrid.Type == cardType )
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Verify that the given player name does not match the name of player than has already joined the lobby. If it does, rename the player.
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
