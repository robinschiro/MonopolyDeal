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

namespace MonopolyDeal
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window, IGameClient, INotifyPropertyChanged
    {
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

        public event PropertyChangedEventHandler PropertyChanged;

        // For each type of property, store the number of cards of that property type that make up a complete monopoly
        // ROBIN TODO: Think of a way to do this without hardcoding the data.
        private Dictionary<PropertyType, int> MonopolyData = new Dictionary<PropertyType, int>()
        {
            {PropertyType.Blue, 2},
            {PropertyType.Brown, 2},
            {PropertyType.Green, 3},
            {PropertyType.LightBlue, 3},
            {PropertyType.Orange, 3},
            {PropertyType.Pink, 3},
            {PropertyType.Railroad, 4},
            {PropertyType.Red, 3},
            {PropertyType.Utility, 2},
            {PropertyType.Wild, 0},
            {PropertyType.Yellow, 3},
            {PropertyType.None, -1}
        };

        private Dictionary<PropertyType, Dictionary<int, int>> RentData = new Dictionary<PropertyType, Dictionary<int, int>>()
        {
            {PropertyType.Blue, new Dictionary<int, int>() 
                                    {
                                        {1, 3},
                                        {2, 8}
                                    }},
            {PropertyType.Brown, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2}
                                    }},
            {PropertyType.Green, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 4},
                                        {3, 7}
                                    }},
            {PropertyType.LightBlue, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 3}
                                    }},
            {PropertyType.Orange, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 3},
                                        {3, 5}
                                    }},
            {PropertyType.Pink, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 4}
                                    }},
            {PropertyType.Railroad, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2},
                                        {3, 3},
                                        {4, 4}
                                    }},
            {PropertyType.Red, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 3},
                                        {3, 6},
                                    }},
            {PropertyType.Utility, new Dictionary<int, int>() 
                                    {
                                        {1, 1},
                                        {2, 2}
                                    }},
            {PropertyType.Yellow, new Dictionary<int, int>() 
                                    {
                                        {1, 2},
                                        {2, 4},
                                        {3, 6},
                                    }},
        };

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
            this.Player = FindPlayerInList(playerName);

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

                            CreateNewThread(new Action<Object>(DisplayOpponentCards));

                            break;
                        }

                        case Datatype.RequestRent:
                        {
                            // If the player receives a rent request, check if he is one of the players who must pay rent.
                            ActionData.RentRequest request = (ActionData.RentRequest)ServerUtilities.ReceiveMessage(inc, messageType);

                            // If he is, open the rent window.
                            if ( request.Rentees.Any(player => player.Name == this.Player.Name) )
                            {
                                CreateNewThread(new Action<Object>(DisplayRentWindow), request);
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
            if ( FindPlayerPositionInPlayerList(this.Player.Name) == this.Turn.CurrentTurnOwner && this.Turn.NumberOfActions < 3 )
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

                        RemoveCardFromHand(cardButton);

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
            Button targetCardButton = sender as Button;
            List<Card> targetCardList = FindListContainingCard(targetCardButton.Tag as Card);
            PropertyType targetCardListColor = GetCardListColor(targetCardList);

            Button sourceCardButton = e.Data.GetData(typeof(Button)) as Button;
            Card sourceCard = sourceCardButton.Tag as Card;

            // Do not allow a drag-drop operation to occur if the source and target are the same objects.
            if ( sourceCardButton != targetCardButton )
            {
                bool isMonopoly = IsCardListMonopoly(targetCardList);

                if ( !isMonopoly && (targetCardListColor == sourceCard.Color || PropertyType.Wild == sourceCard.Color || PropertyType.Wild == targetCardListColor) )
                {
                    RemoveCardFromCardsInPlay(sourceCard, this.Player);

                    targetCardList.Add(sourceCard);
                    DisplayCardsInPlay(this.Player, this.PlayerOneField);

                    // Update the server's information regarding this player.
                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);

                    // Mark the event as handled.
                    e.Handled = true;
                }
                else if (isMonopoly)
                {
                    switch ( sourceCard.Name )
                    {
                        case ( "House" ):
                        {
                            if ( !IsCardInCardList("House", targetCardList) )
                            {
                                RemoveCardFromCardsInPlay(sourceCard, this.Player);
                                targetCardList.Add(sourceCard);
                                DisplayCardsInPlay(this.Player, this.PlayerOneField);

                                // Update the server's information regarding this player.
                                ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
                            }
                            break;   
                        }

                        case ( "Hotel" ):
                        {
                            if ( IsCardInCardList("House", targetCardList) && !IsCardInCardList("Hotel", targetCardList) )
                            {
                                RemoveCardFromCardsInPlay(sourceCard, this.Player);
                                targetCardList.Add(sourceCard);
                                DisplayCardsInPlay(this.Player, this.PlayerOneField);

                                // Update the server's information regarding this player.
                                ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
                            }


                            break;   
                        }
                    }
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

        // Display the cards of the player's opponents.
        public void DisplayOpponentCards( Object filler )
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


        // Clear all of the card buttons from a given grid.
        public void ClearCardsInGrid( Grid playerField )
        {
            playerField.Children.Clear();
        }

        // Determines if a property card is of the same color as another property card.
        // TODO: Account for houses and hotels being added to monopolies.
        public bool CheckPropertyCompatibility( Card propOne, Card propTwo )
        {
            if ( propOne.Type != CardType.Property || propTwo.Type != CardType.Property )
            {
                return false;
            }

            // There is probably a better way to compare the cards' PropertyTypes, but this works for now.
            if ( ((propOne.Color == propTwo.Color) && (propOne.Color != PropertyType.None)) ||
                    ((propOne.AltColor == propTwo.AltColor) && (propOne.AltColor != PropertyType.None)) ||
                    ((propOne.Color == propTwo.AltColor) && (propOne.Color != PropertyType.None)) ||
                    ((propOne.AltColor == propTwo.Color) && (propOne.Color != PropertyType.None)) ||
                    ((propOne.Color == PropertyType.Wild) || (propTwo.Color == PropertyType.Wild)) )
            {
                return true;
            }

            return false;

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
                    List<List<Card>> monopolies = FindMonopolies(player);

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

                    foreach ( List<Card> cardList in FindMonopolies(player) )
                    {
                        colorsOfCurrentMonopolies.Add(GetCardListColor(cardList));
                    }

                    if ( PropertyType.Wild != cardBeingAdded.Color )
                    {
                        if ( colorsOfCurrentMonopolies.Contains(cardBeingAdded.Color) )
                        {
                            return false;
                        }

                        // If the card has passed the previous check, add it to the player's CardsInPlay.
                        for ( int i = 1; i < player.CardsInPlay.Count; ++i )
                        {
                            List<Card> cardList = player.CardsInPlay[i];

                            PropertyType cardListColor = GetCardListColor(cardList);

                            // If the cardlist is not a monopoly and is compatible with the card being added, add the card to the list.
                            if ( !IsCardListMonopoly(cardList) && (cardListColor == cardBeingAdded.Color || PropertyType.Wild == cardListColor) )
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
            for ( int i = 0; i < player.CardsInPlay.Count; ++i )
            {
                if ( player.CardsInPlay[i].Remove(cardBeingRemoved) )
                {
                    if ( (0 == player.CardsInPlay[i].Count) && (0 != i) )
                    {
                        player.CardsInPlay.Remove(player.CardsInPlay[i]);
                    }
                    return;
                }
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
            if ( isHand && player == this.Player )
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
                        if ( player == this.Player )
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
                                    foreach ( List<Card> cardList in FindMonopolies(Player) )
                                    {
                                        colorsOfCurrentMonopolies.Add(GetCardListColor(cardList));
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
                                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
                                };

                                menu.Items.Add(separateMenuItem);
                            }

                            menu.Items.Add(forwardMenuItem);
                            menu.Items.Add(backwardMenuItem);
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
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
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
                        ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
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


            return false;
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
                    PropertyType cardListColor = GetCardListColor(cardList);
                    if ( (PropertyType.Wild == rentCard.Color) || (cardListColor == rentCard.Color) || (cardListColor == rentCard.AltColor) )
                    {
                        matchingPropertyGroups.Add(cardList);
                    }
                }

                // If the player will not get any money from playing the rent, confirm the play.
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
                    int totalValue = RentData[GetCardListColor(cardList)][numProperties] + ((null != house) ? (house.Value) : (0)) + ((null != hotel) ? (hotel.Value) : (0));

                    if ( totalValue > amountToCollect )
                    {
                        amountToCollect = totalValue;
                    }
                }

                // If the player has a "Double the Rent" card and at least two actions remaining, ask if he would like to use it.
                Card doubleRentCard = FindActionCardInList(this.Player.CardsInHand, 1);

                if ( (null != doubleRentCard) && (this.Turn.NumberOfActions >= 2) )
                {
                    MessageBoxResult result = MessageBox.Show("You have a " + doubleRentCard.Name + " card. Would you like to apply it to this rent?",
                                    "Are you sure?",
                                    MessageBoxButton.YesNo);

                    rentDoubled = (MessageBoxResult.Yes == result);

                    if ( rentDoubled )
                    {
                        // Remove the card from the player's hand and add it to the discard pile.
                        RemoveCardFromHand((PlayerOneHand.Children[this.Player.CardsInHand.IndexOf(doubleRentCard)] as Grid).Tag as Button);
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
                // Send a rent request to all players except for the renter.
                rentees = new List<Player>(this.PlayerList.Where(player => player.Name != this.Player.Name));
            }

            // Send the message to the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestRent, new ActionData.RentRequest(this.Player.Name, rentees, amountToCollect, rentDoubled));

            return true;
        }

        // Display the rent window.
        private void DisplayRentWindow( Object request )
        {
            ActionData.RentRequest rentRequest = (ActionData.RentRequest)request;

            RentWindow rentWindow = new RentWindow(this.Player, rentRequest.RenterName, rentRequest.RentAmount, rentRequest.IsDoubled);

            if ( true == rentWindow.ShowDialog() )
            {
                // Transfer the selected assets to the renter and update the server with the new player list.

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
                    if ( card == cardBeingFound )
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

        // Given a player's name, find the matching Player object in the PlayerList (there is only one PlayerList).
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

        // Return a list of the indices (where each index is a position in the Player's CardsInPlay) of all monopolies.
        public List<List<Card>> FindMonopolies( Player player )
        {
            List<List<Card>> monopolies = new List<List<Card>>();

            // Iterate through the card lists. Always skip the first one, since it is reserved for money.
            for ( int i = 1; i < player.CardsInPlay.Count; ++i )
            {
                if ( IsCardListMonopoly(player.CardsInPlay[i]) )
                {
                    monopolies.Add(player.CardsInPlay[i]);
                }
            }

            return monopolies;
        }

        // Given a list of cards, determine the color of the monopoly being formed by the cards.
        public PropertyType GetCardListColor( List<Card> cardList )
        {
            if ( cardList.Count > 0 )
            {
                foreach ( Card card in cardList )
                {
                    if ( PropertyType.None != card.Color && PropertyType.Wild != card.Color )
                    {
                        return card.Color;
                    }
                }

                return PropertyType.Wild;
            }
            else
            {
                return PropertyType.None;
            }
        }

        // Determine if a card with a given name is in a card list.
        public bool IsCardInCardList( string name, List<Card> cardList )
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

        // Determine if a provided card list is a monopoly.
        public bool IsCardListMonopoly( List<Card> cardList )
        {
            PropertyType monopolyColor = GetCardListColor(cardList);
            int countOfProperties = 0;

            // First count the number of properties in the list. This algorithm excludes houses and hotels from the count.
            foreach ( Card card in cardList )
            {
                if ( card.Color != PropertyType.None )
                {
                    countOfProperties++;
                }
            }

            // If the number of properties in the list matches the number required for a monopoly of that color, it is a monopoly.
            // If the monopolyColor is 'Wild', then the list must contain only Multicolor Property Wild Card(s).
            if ( PropertyType.Wild != monopolyColor && countOfProperties == MonopolyData[monopolyColor] )
            {
                return true;
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
