using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lidgren.Network;
using System.Reactive.Linq;
using System.Reactive;
using System.Windows.Media.Animation;
using System.Windows.Data;
using GameObjects;
using GameServer;
using AdditionalWindows;
using Utilities;

namespace MonopolyDeal
{

    /// <summary>
    /// Interaction logic for GameWindow.xaml
    /// </summary>
    public partial class GameWindow : Window, IGameClient, INotifyPropertyChanged
    {

        #region Variables

        private Deck Deck;
        private Player Player;
        private List<Player> PlayerList;
        private String ServerIP;
        private int SelectedCard;
        private bool BeginCommunication;
        private volatile NetClient Client;
        private List<Grid> PlayerFields;                        // This list is needed in order to resize the grids when the window size changes.
        private Dictionary<String, Grid> PlayerFieldDictionary;
        private Dictionary<String, Grid> PlayerHandDictionary;
        private bool HavePlayersBeenAssigned;
        private Turn Turn;
        private List<Card> DiscardPile;
        private int NumberOfRentees = 0;
        private List<Card> AssetsReceived = new List<Card>();
        private bool VictimAcceptedDeal;
        private MessageDialog WaitMessage;

        public event PropertyChangedEventHandler PropertyChanged;

        private MediaPlayer MediaPlayer;

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

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        #endregion Variables

        public GameWindow( string playerName, string ipAddress, Turn turn )
        {
            InitializeComponent();

            this.SelectedCard = -1;
            this.BeginCommunication = false;
            this.HavePlayersBeenAssigned = false;
            this.ServerIP = ipAddress;

            //// Play theme song.
            //mediaPlayer = new MediaPlayer();
            //CreateNewThread(new Action<Object>(PlaySong));
            
            // Instantiate the Player's Turn object.
            this.Turn = turn;

            // Instantiate the DiscardPile.
            this.DiscardPile = new List<Card>();

            // Connect the client to the server.
            InitializeClient(ipAddress);

            // Do not continue until the client has successfully established communication with the server.
            while ( !this.BeginCommunication ) ;

            // Receive a list of the players already on the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestPlayerList);

            // Do not continue until the client receives the Player List from the server.
            while ( this.PlayerList == null ) ;

            // Find the Player in the PlayerList.
            this.Player = (Player)this.PlayerList.Find(player => player.Name == playerName);

            // Assign the hands and playing fields of opponents to appropriate areas of the client's screen.
            AssignPlayersToGrids();

            WindowGrid.DataContext = this;

            // Re-title the window.
            this.Title = playerName + "'s Window";

            // Add an empty grid as the first element of every field. This grid is used to display each player's money pile.
            PlayerOneField.Children.Add(CreateCardGrid());
            PlayerTwoField.Children.Add(CreateCardGrid());
            PlayerThreeField.Children.Add(CreateCardGrid());
            PlayerFourField.Children.Add(CreateCardGrid());

            // Add the four player fields to the list.
            this.PlayerFields = new List<Grid>();
            this.PlayerFields.Add(PlayerOneField);
            this.PlayerFields.Add(PlayerTwoField);
            this.PlayerFields.Add(PlayerThreeField);
            this.PlayerFields.Add(PlayerFourField);

            // Display the cards in this player's hand.
            foreach ( Card card in Player.CardsInHand )
            {
                AddCardToGrid(card, PlayerOneHand, Player, true);
            }

            // Because this call automatically draws cards for the player, it must occur after the player's cardsInHand have been placed on the grid.
            CheckIfCurrentTurnOwner();

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

        private void InitializeClient( string serverIP )
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

            if ( false == this.BeginCommunication )
            {
                // Continue reading messages until the requested update is received.
                while ( !updateReceived )
                {
                    // Iterate through all of the available messages.
                    while ( (inc = (peer as NetPeer).ReadMessage()) != null )
                    {
                        if ( inc.MessageType == NetIncomingMessageType.StatusChanged )
                        {
                            this.BeginCommunication = true;
                            updateReceived = true;
                        }
                    }
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

                            // I'm not sure if this is necessary.
                            //Client.Recycle(inc);

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

                            // This piece of code causes this.Player to be overwritten when updates to other players are made.
                            // Commenting it out fixed that issue; I'm leaving this comment here in case this code is pivotal for some unknown reason.
                            //if ( null != this.Player )
                            //{
                            //    this.Player = (Player)this.PlayerList.Find(player => player.Name == this.Player.Name);

                            //    // Be sure that the player's MoneyList is properly set. This is the quick solution, not necessarily the correct solution.
                            //    foreach ( Card card in this.Player.CardsInPlay[0] )
                            //    {
                            //        this.Player.MoneyList.Add(card);
                            //    }
                            //}

                            // Display everyone's cards in play.
                            CreateNewThread(new Action<Object>(DisplayAllCards));

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
                            CreateNewThread(new Action<Object>(CheckIfCurrentTurnOwner));

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
            if ( isCurrentTurnOwner && this.Turn.NumberOfActions < 3 )
            {
                Button cardButton = sender as Button;

                if ( IsCardSelected(cardButton) )
                {
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
                        if ( HandleAction(cardBeingPlayed) )
                        {
                            // If the action card was handled, add the card to the DiscardPile and display it.
                            this.DiscardPile.Add(cardBeingPlayed);
                            AddCardToGrid(cardBeingPlayed, DiscardPileGrid, this.Player, false);
                            cardWasPlayed = true;
                        }
                    }

                    if ( cardWasPlayed )
                    {
                        // Update the value of 'SelectedCard' (no card is selected after a card is played).
                        SelectedCard = -1;

                        RemoveCardButtonFromHand(cardButton);

                        // Update the player's number of actions.
                        this.Turn.NumberOfActions++;

                        // Update the server's information regarding this player.
                        ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);

                        // Update the server's discard pile.
                        ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, DiscardPile);
                    }
                }
            }
        }

        // Increase the size of a card when it is selected and decrease its size when another card is selected.
        public void SelectCardEvent( object sender, MouseButtonEventArgs args )
        {
            // Get the index of the button that called this event.
            int buttonIndex = Grid.GetColumn((sender as Button).Parent as Grid);

            // The card should be selected if a) it is not already selected and b) it is the player's turn.
            if ( (buttonIndex != SelectedCard) && isCurrentTurnOwner && (Turn.NumberOfActions < 3) )
            {
                // Deselect the currently selected card.
                if ( SelectedCard != -1 )
                {
                    //DeselectCard(FindButtonInGrid(SelectedCard, PlayerOneHand));
                    DeselectCard((PlayerOneHand.Children[SelectedCard] as Grid).Tag as Button);
                }

                // Select this card.
                SelectedCard = buttonIndex;
                SelectCard(sender as Button);

                // Add this card to the InfoBox.
                DisplayCardInInfobox((sender as Button).Tag as Card);
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

        // When the client resizes the windows, scale the display of overlaid cards on the players' fields so that they
        // are always spaced proportionally.
        //private void Window_SizeChanged( object sender, SizeChangedEventArgs e )
        //{
        //    if ( PlayerFields != null )
        //    {
        //        foreach ( Grid field in PlayerFields )
        //        {
        //            foreach ( FrameworkElement element in field.Children )
        //            {
        //                Grid cardGrid = (Grid)element;
        //                for ( int i = 0; i < cardGrid.Children.Count; ++i )
        //                {
        //                    Button cardButton = (Button)cardGrid.Children[i];

        //                    TransformCardButton(cardButton, i, field.ActualHeight);
        //                }
        //            }
        //        }
        //    }
        //}

        private void Size_Changed( SizeChangedEventArgs e )
        {
            CreateNewThread(new Action<Object>(ResizeUIElements));
        }

        // Draw two cards from the Deck.
        private void DrawCardButton_Click( object sender, RoutedEventArgs e )
        {
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

        // End the player's turn.
        private void EndTurnButton_Click( object sender, RoutedEventArgs e )
        {
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
            this.Turn.NumberOfActions = 0;

            // Send the updated Turn object to the server to be distributed to the other clients.
            ServerUtilities.SendMessage(Client, Datatype.EndTurn, this.Turn);
        }

        // Use this event to respond to key presses.
        private void Window_KeyDown( object sender, KeyEventArgs e )
        {

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
                        performTransfer = (targetCardListColor == sourceCard.Color || PropertyType.Wild == sourceCard.Color || PropertyType.Wild == targetCardListColor);

                        //// Mark the event as handled.
                        //e.Handled = true;
                    }
                    else
                    {
                        switch ( sourceCard.Name )
                        {
                            case ("House"):
                            {
                                performTransfer = !IsCardInCardList("House", targetCardList);

                                break;
                            }

                            case ("Hotel"):
                            {
                                performTransfer = IsCardInCardList("House", targetCardList) && !IsCardInCardList("Hotel", targetCardList);

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
        public void CheckIfCurrentTurnOwner(Object filler = null)
        {
            if ( this.Turn.CurrentTurnOwner == FindPlayerPositionInPlayerList(this.Player.Name) )
            {
                IsCurrentTurnOwner = true;

                // Draw the cards for the current turn owner automatically.
                DrawCards(2);

                // Inform the player that it is his/her turn.
                //MessageBox.Show("It is your turn!");
            }
            else
            {
                IsCurrentTurnOwner = false;
            }
        }

        // Given a card button, determine if it is currently selected.
        public bool IsCardSelected( Button cardButton )
        {
            return Grid.GetColumn(cardButton.Parent as Grid) == SelectedCard;
        }

        public void DisplayUpdatedDiscardPile( Object filler )
        {
            DiscardPileGrid.Children.Clear();

            foreach ( Card card in this.DiscardPile )
            {
                AddCardToGrid(card, DiscardPileGrid, this.Player, false);
            }
        }

        // Wrap a Button around a Card.
        public Button ConvertCardToButton( Card card )
        {
            Button cardButton = new Button();
            cardButton.Content = new Image();
            (cardButton.Content as Image).Source = new BitmapImage(new Uri(card.CardImageUriPath, UriKind.Absolute));
            cardButton.Tag = card;
            cardButton.Style = (Style)FindResource("NoChromeButton");
            cardButton.RenderTransform = new TransformGroup();
            cardButton.RenderTransformOrigin = new Point(0.5, 0.5);

            //Binding buttonWidthToImageWidth = new Binding("ActualWidth");
            //buttonWidthToImageWidth.Source = (cardButton.Content as Image);
            //cardButton.SetBinding(Button.WidthProperty, buttonWidthToImageWidth);

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

                    // Update the display of the opponent's hand.
                    ClearCardsInGrid(PlayerHandDictionary[player.Name]);
                    foreach ( Card card in player.CardsInHand )
                    {
                        Card cardBack = new Card(-1, "pack://application:,,,/GameObjects;component/Images/cardback.jpg");
                        AddCardToGrid(cardBack, PlayerHandDictionary[player.Name], player, true);
                    }
                }
            }
        }

        public void DisplayAllCards( Object filler = null )
        {
            // Display the opponents' cards.
            DisplayOpponentCards();

            // Display the Player's cards in play as well.
            DisplayCardsInPlay(this.Player, PlayerOneField);
        
            // Display the Player's cards in hand.
            //DisplayCardsInHand(this.Player, PlayerOneHand);
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


        // Resize UI elements so that they are propotional to the size of the window.
        public void ResizeUIElements( Object filler )
        {
            if ( PlayerFields != null )
            {
                foreach ( Grid field in PlayerFields )
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
                        break;
                    }

                    case 2:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerThreeField);
                        PlayerHandDictionary.Add(player.Name, PlayerThreeHand);
                        break;
                    }

                    case 3:
                    {
                        PlayerFieldDictionary.Add(player.Name, PlayerFourField);
                        PlayerHandDictionary.Add(player.Name, PlayerFourHand);
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
            if ( cardBeingAdded.Type == CardType.Property )
            {
                if ( "House" == cardBeingAdded.Name || "Hotel" == cardBeingAdded.Name )
                {
                    // Add the house to an existing monopoly or the hotel to a monopoly that has a house. The card should not be removed from the player's hand
                    // unless it is placed in a monopoly.
                    List<List<Card>> monopolies = ClientUtilities.FindMonopolies(player);

                    if ( "House" == cardBeingAdded.Name )
                    {
                        foreach ( List<Card> monopoly in monopolies )
                        {
                            if ( !IsCardInCardList("House", monopoly) )
                            {
                                monopoly.Add(cardBeingAdded);
                                AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false, player.CardsInPlay.IndexOf(monopoly));

                                return true;
                            }
                        }
                    }
                    else
                    {
                        foreach ( List<Card> monopoly in monopolies )
                        {
                            if ( IsCardInCardList("House", monopoly) && !IsCardInCardList("Hotel", monopoly) )
                            {
                                monopoly.Add(cardBeingAdded);
                                AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false, player.CardsInPlay.IndexOf(monopoly));

                                return true;
                            }
                        }
                    }

                    // Do not add the house or hotel if the code is reached.
                    return false;
                }
                else
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
                        // ROBIN TODO: Instead of doing nothing and returning false, the card should be added to a new list.
                        if ( colorsOfCurrentMonopolies.Contains(cardBeingAdded.Color) )
                        {
                            return false;
                        }

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
                }

                // If this code is reached, the card must not have matched any existing properties.
                List<Card> newCardList = new List<Card>();
                newCardList.Add(cardBeingAdded);
                player.CardsInPlay.Add(newCardList);
                AddCardToGrid(cardBeingAdded, PlayerFieldDictionary[player.Name], player, false);
                return true;
            }
            else if ( cardBeingAdded.Type == CardType.Money )
            {
                AddMoneyToBank(cardBeingAdded, player);
                return true;
            }

            return false;
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
                cardButton.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(SelectCardEvent);

                // Prevent the context menu of a card from opening when it shifts into the position of a card that has just been played.
                // Also, disable the context menus of all cards in the player's hand that are not selected.
                cardButton.ContextMenuOpening += ( sender, args ) =>
                {
                    // If the card is not selected, do not open the context menu.
                    if ( !IsCardSelected(cardButton) )
                    {
                        args.Handled = true;
                    }
                };

                // If a card is not an action card, there is only one way it can be played.
                if ( CardType.Action != cardBeingAdded.Type )
                {
                    if ( HasAltColor(cardBeingAdded) )
                    {
                        ContextMenu menu = new ContextMenu();

                        MenuItem playMenuItem = new MenuItem();
                        playMenuItem.Header = "Play as Property";
                        playMenuItem.Click += ( sender, args ) =>
                        {
                            PlayCardEvent(cardButton, null);
                        };

                        MenuItem flipMenuItem = new MenuItem();
                        flipMenuItem.Header = "Flip Card";
                        flipMenuItem.Click += ( sender2, args2 ) =>
                        {
                            // Flip the card, swapping its primary and alternative colors.
                            FlipCard(cardBeingAdded);

                            TransformCardButton(cardButton, 0, 0);

                            DisplayCardInInfobox(cardBeingAdded);
                        };
                        menu.Items.Add(playMenuItem);
                        menu.Items.Add(flipMenuItem);

                        cardButton.ContextMenu = menu;
                    }
                    else
                    {
                        cardButton.PreviewMouseRightButtonDown += new MouseButtonEventHandler(PlayCardEvent);
                    }
                }
                // Houses and hotels have unique menu options. Players can add these to existing monopolies or use them as money.
                else if ( "House" == cardBeingAdded.Name || "Hotel" == cardBeingAdded.Name )
                {
                    ContextMenu menu = new ContextMenu();
                    MenuItem playAsActionMenuItem = new MenuItem();
                    playAsActionMenuItem.Header = "Add to Monopoly";
                    playAsActionMenuItem.Click += ( sender, args ) =>
                    {
                        cardBeingAdded.Type = CardType.Property;
                        PlayCardEvent(cardButton, null);
                    };
                    MenuItem playAsMoneyMenuItem = new MenuItem();
                    playAsMoneyMenuItem.Header = "Play as Money";
                    playAsMoneyMenuItem.Click += ( sender, args ) =>
                    {
                        cardBeingAdded.Type = CardType.Money;
                        PlayCardEvent(cardButton, null);
                    };

                    menu.Items.Add(playAsActionMenuItem);
                    menu.Items.Add(playAsMoneyMenuItem);
                    cardButton.ContextMenu = menu;
                }
                // These apply to all other action cards. Players can play these cards as actions or money.
                else
                {
                    ContextMenu menu = new ContextMenu();

                    // If the action card is not a "Double the Rent", add this option.
                    // (Double the Rent cards are played only with Rent cards.)

                    if ( 2 != cardBeingAdded.ActionID )
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
                    cardButton.ContextMenu = menu;
                }
            }
            // The card is being added to a player's playing field.
            else
            {
                // Play the card in a certain way depending on its type (i.e. property, money, or action).
                switch ( cardBeingAdded.Type )
                {
                    case CardType.Property:
                    {
                        // Create context menu that allows the user to reorder properties.
                        // This if statement is required to prevent players from seeing the context menu of other players' cards in play.
                        if ( player.Name == this.Player.Name )
                        {
                            // Create the appropriate context menu.
                            ContextMenu menu = new ContextMenu();
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
                                flipMenuItem.Header = "Flip Card";
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
                                        return;
                                    }

                                    // Flip the card, swapping its primary and alternative colors.
                                    FlipCard(cardBeingAdded);

                                    // TODO: If the card list to which the card belonged contains a house, place the house back in the player's hand.
                                    // Or, place the house/hotel in a separate list that can be accessed whenever the player wants to play a house/hotel or use it as money.
                                    if ( IsCardInCardList("House", FindListContainingCard(cardBeingAdded)) || IsCardInCardList("Hotel", FindListContainingCard(cardBeingAdded)) )
                                    {

                                    }

                                    // Remove the card and re-add it to the Player's CardsInPlay.
                                    //RefreshCardsInPlay(player, grid);
                                    RemoveCardFromCardsInPlay(cardBeingAdded, this.Player);
                                    AddCardToCardsInPlay(cardBeingAdded, this.Player);

                                    // Displayed the updated CardsInPlay.
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
                                separateMenuItem.Header = "Separate Wild Card";
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
                                }
                            };


                            cardButton.ContextMenu = menu;


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
                    cardButton.RenderTransform = horizontalTransform;
                    break;
                }

                case CardType.Action:
                {
                    break;
                }
            }
        }

        // Remove a card from the player's hand.
        public void RemoveCardFromHand( Card cardtoRemove, Player player )
        {
            // Remove the card from the player's CardsInHand list.
            player.CardsInHand = new List<Card>(player.CardsInHand.Where(card => card.CardID != cardtoRemove.CardID));
        }
        
        // Remove a card (given its Button wrapper) from the player's hand.
        public void RemoveCardButtonFromHand( Button cardButton )
        {
            RemoveCardFromHand(cardButton.Tag as Card, this.Player);

            // Get the index of the card button.
            int buttonIndex = Grid.GetColumn(cardButton.Parent as Grid);

            // Remove the card's button from the PlayerOneHand grid.
            PlayerOneHand.Children.RemoveAt(buttonIndex);

            // Shift all subsequential card buttons to the left.
            for ( int i = buttonIndex; i < Player.CardsInHand.Count; ++i )
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
        
        // Increase the size of the currently selected card.
        public void SelectCard( Button cardButton )
        {
            if ( null != cardButton )
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1.2;
                myScaleTransform.ScaleX = 1.2;

                (cardButton.RenderTransform as TransformGroup).Children.Add(myScaleTransform);
            }
        }

        // Set the currently selected card to its normal size.
        public void DeselectCard( Button cardButton )
        {
            if ( null != cardButton )
            {
                // Instantiating an object of type "ScaleTransform" should not be necessary.
                // TODO: Find a way to pass the "ScaleTransform" without having to instantiate an object.
                ScaleTransform scaleTransform = new ScaleTransform();
                RemoveTransformTypeFromGroup(scaleTransform.GetType(), cardButton.RenderTransform as TransformGroup);
            }
        }

        // Draw a given amount of cards from the Deck (the cards are placed in the Player's hand).
        private void DrawCards( int numberOfCards )
        {
            // Reset the Deck.
            Deck = null;
            ServerUtilities.SendMessage(Client, Datatype.RequestDeck);

            // Do not continue until the updated Deck is received from the server.
            while ( Deck == null ) ;

            // Remove the given number of cards from the top of the Deck and add them to the Player's hand.
            for ( int i = 0; i < numberOfCards; ++i )
            {
                // If the deck is empty, transfer all the cards from the discard pile to the deck and shuffle.
                if ( Deck.CardList.Count == 0 )
                {
                    Deck.CardList = DiscardPile;
                    Deck.Shuffle<Card>(Deck.CardList);
                    DiscardPile = new List<Card>();

                    ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, DiscardPile);
                }

                Card drawnCard = Deck.CardList[0];

                Player.CardsInHand.Add(drawnCard);
                AddCardToGrid(drawnCard, PlayerOneHand, Player, true);
                Deck.CardList.Remove(drawnCard);
            }
            

            // Send the updated Deck and Player to the server.
            ServerUtilities.SendMessage(Client, Datatype.UpdateDeck, Deck);
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
        }

        // Again, testing AttachedProperties. Will need to discuss.
        private void setInfoBox( TriggerBase target, int location )
        {
            target.SetValue(InfoBoxAttachedProperty, Player.CardsInHand[location]);
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
                // ROBIN TODO: Verify that both players meet the required criteria for this steal card to be used. If the don't, cancel the action.
                // Perhaps prevent Dealbreaker if thief has color from selected monopoly
                {
                    
                }

                // Display the property theft window.
                PropertyTheftWindow propertyTheftWindow = new PropertyTheftWindow(dialog.SelectedPlayer, this.Player, stealCard.ActionID);
                bool isDealBreaker = TheftType.Dealbreaker == (TheftType)(stealCard.ActionID);

                if ( true == propertyTheftWindow.ShowDialog() )
                {
                    // Send the Theft Request to the victim. The victim has a chance to say "No" if he has a "Just Say No" card.
                    {
                        ServerUtilities.SendMessage(Client, Datatype.RequestTheft, new ActionData.TheftRequest(this.Player.Name, propertyTheftWindow.Victim.Name, stealCard.ActionID, propertyTheftWindow.PropertyToGive, propertyTheftWindow.PropertiesToTake));
                    }

                    // Display a wait message until the victim has responded to the request.
                    WaitMessage = new MessageDialog("Please Wait...", "Waiting for victim to respond...");
                    WaitMessage.ShowDialog();

                    // This is called once the thief has received a reply from the victim.
                    if ( this.VictimAcceptedDeal )
                    {
                        // Transfer the PropertyToGive to the victim.
                        if ( String.Empty != propertyTheftWindow.PropertyToGive.Name )
                        {
                            RemoveCardFromCardsInPlay(propertyTheftWindow.PropertyToGive, this.Player);
                        }
                        
                        // Add the card(s) to the thief's cards in play.
                        if ( isDealBreaker )
                        {
                            this.Player.CardsInPlay.Add(propertyTheftWindow.PropertiesToTake);
                        }
                        else
                        {
                            AddCardToCardsInPlay(propertyTheftWindow.PropertiesToTake[0], this.Player);
                        }
                        

                        // Update the server with the current version of this player (the thief).
                        ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);
                    }
                    else
                    {
                        MessageBox.Show(propertyTheftWindow.Victim.Name + " rejected your deal with a \"Just Say No!\".");
                    }


                }
            }   

            return true;
        }

        // Collect rent from the other players.
        private bool CollectRent( Card rentCard )
        {
            int amountToCollect = 0;
            bool rentDoubled = false;

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

                // If the player will not get any money from playing the rent, give him the option to cancel his action.
                if ( 0 == matchingPropertyGroups.Count )
                {
                    MessageBoxResult result = MessageBox.Show("You do not have " + rentCard.Color.ToString() + " or " + rentCard.AltColor.ToString() + " properties. Are you sure you want to play this card?",
                                    "Are you sure?",
                                    MessageBoxButton.YesNo);

                    if ( MessageBoxResult.No == result )
                    {
                        return false;
                    }
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
                Card doubleRentCard = FindActionCardInList(this.Player.CardsInHand, 1);

                if ( (null != doubleRentCard) && (this.Turn.NumberOfActions < 2) )
                {
                    MessageBoxResult result = MessageBox.Show("You have a " + doubleRentCard.Name + " card. Would you like to apply it to this rent?",
                                    "Are you sure?",
                                    MessageBoxButton.YesNo);

                    rentDoubled = (MessageBoxResult.Yes == result);

                    if ( rentDoubled )
                    {
                        // Remove the card from the player's hand and add it to the discard pile.
                        RemoveCardButtonFromHand((PlayerOneHand.Children[this.Player.CardsInHand.IndexOf(doubleRentCard)] as Grid).Tag as Button);
                        this.DiscardPile.Add(doubleRentCard);
                        AddCardToGrid(doubleRentCard, DiscardPileGrid, this.Player, false);

                        // Update the number of actions.
                        this.Turn.NumberOfActions++;
                    }
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
            }
            else
            {
                // Prevent the renter from performing any action until all rentees have paid their rent.
                NumberOfRentees = PlayerList.Count - 1;

                // Send a rent request to all players except for the renter.
                rentees = new List<Player>(this.PlayerList.Where(player => player.Name != this.Player.Name));
            }

            // Send the message to the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestRent, new ActionData.RentRequest(this.Player.Name, rentees, amountToCollect, rentDoubled));

            // Display a messagebox informing the renter that he cannot do anything until all rentees have paid their rent.
            // ROBIN TODO: Show some sort of progress bar.
            WaitMessage = new MessageDialog("Please Wait...", "Waiting for rentees to pay rent...");
            WaitMessage.ShowDialog();

            return true;
        }

        // Display the rent window.
        private void DisplayRentWindow( Object request )
        {
            ActionData.RentRequest rentRequest = (ActionData.RentRequest)request;
            //String renterName = this.PlayerList.Find(player => player.Name == rentRequest.RenterName).Name;
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
                
                // Display this player's updated CardsInPlay.
                DisplayCardsInPlay(this.Player, PlayerOneField);

            }
            else
            {
                // Remove the Just Say No from the victim's hand
                RemoveCardButtonFromHand((PlayerOneHand.Children[this.Player.CardsInHand.FindIndex(card => 2 == card.ActionID)] as Grid).Tag as Button);
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
                    MessageBox.Show(rentResponse.RenteeName + " rejected your rent request with a \"Just Say No\" card.");
                }

                // Update the number of remaining rentees.
                this.NumberOfRentees--;

                // If all rentees have paid their rent, then add the cards from the AssetsReceived to the player's CardsInPlay.
                if ( 0 == this.NumberOfRentees )
                {
                    foreach ( Card card in this.AssetsReceived )
                    {
                        AddCardToCardsInPlay(card, this.Player);
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

            MessageDialog requestDialog;
            bool hasNo = this.Player.CardsInHand.Any(card => 2 == card.ActionID);
            string message = theftRequest.ThiefName + " has played a " + ((TheftType)theftRequest.ActionID).ToString() + " against you.\n";

            // By default, use the name of the first property in the list.
            string nameOfPropertyToTake = theftRequest.PropertiesToTake[0].Name;            

            switch ( (TheftType)theftRequest.ActionID )
            {
                case TheftType.Dealbreaker:
                {
                    // If a Dealbreaker was used, use the name of the monopoly's color.
                    nameOfPropertyToTake = ClientUtilities.GetCardListColor(theftRequest.PropertiesToTake).ToString();

                    message += "This player would like to take your " + nameOfPropertyToTake + " monopoly.\n";
                    break;
                }

                case TheftType.ForcedDeal:
                {
                    message += "This player would like to trade " + theftRequest.PropertyToGive.Name + " for " + nameOfPropertyToTake + ".\n";
                    break;
                }

                case TheftType.SlyDeal:
                {
                    message += "This player would like to steal " + nameOfPropertyToTake + " from you.\n";
                    break;
                }
            }

            if ( hasNo )
            {
                message += "Would you like to use your \"Just Say No\" card to reject " + theftRequest.ThiefName + "'s deal?";
            }
            else
            {
                message += "Press OK to accept the deal.";
            }

            // Display the message box to the victim.
            requestDialog = new MessageDialog("Theft Request", message, hasNo ? MessageBoxButton.YesNo : MessageBoxButton.OK);
            requestDialog.ShowDialog();

            // Determine if the victim accepted the deal. If he didn't, remove his "Just Say No" and update the server.
            bool acceptedDeal = requestDialog.Result != MessageBoxResult.Yes;
            if ( !acceptedDeal )
            {
                // Remove the Just Say No from the victim's hand.
                RemoveCardButtonFromHand((PlayerOneHand.Children[this.Player.CardsInHand.FindIndex(card => 2 == card.ActionID)] as Grid).Tag as Button);                
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
            ServerUtilities.SendMessage(Client, Datatype.ReplyToTheft, new ActionData.TheftResponse(theftRequest.ThiefName, this.Player.Name, acceptedDeal));
        }

        private void ProcessTheftResponse( Object response )
        {
            ActionData.TheftResponse theftResponse = (ActionData.TheftResponse)response;

            // Update this global so that the proper action is taken when the wait dialog is closed.
            this.VictimAcceptedDeal = theftResponse.AcceptedDeal;

            // Update the wait dialog.
            if ( null != WaitMessage )
            {
                WaitMessage.CloseWindow = true;
            }
        }

        #endregion

        #region Miscellaneous

        // Create a new thread to run a function that cannot be run on the same thread invoking CreateNewThread().
        public void CreateNewThread( Action<Object> action, object data = null )
        {
            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, action, data); };
            Thread newThread = new Thread(start);
            newThread.Start();
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

        // Determine if a card with a given name is in a card list.
        public static bool IsCardInCardList( string name, List<Card> cardList )
        {
            foreach ( Card cardInMonopoly in cardList )
            {
                if ( name == cardInMonopoly.Name )
                {
                    return true;
                }
            }

            return false;
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

        //// Play the song.
        //public void PlaySong(Object filler)
        //{
        //    mediaPlayer.Open(new Uri("C:\\Users\\Robin\\Dropbox\\Songs\\A Fifth of Beethoven.mp3"));
        //    mediaPlayer.Play();
        //}

        #endregion
    }
}
