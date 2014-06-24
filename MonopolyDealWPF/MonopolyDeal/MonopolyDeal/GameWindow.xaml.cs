﻿using System;
using System.ComponentModel;
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
using System.Reactive.Linq;
using System.Reactive;
using System.Windows.Media.Animation;

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
        private Turn Turn;
        private List<Card> DiscardPile;
        private Dictionary<PropertyType, int> MonopolyData;
        MediaPlayer MediaPlayer;


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

        private bool hasDrawn;
        public bool HasDrawn
        {
            get
            {
                return hasDrawn;
            }
            set
            {
                hasDrawn = value;
                OnPropertyChanged("HasDrawn");
            }
        }


        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow( string playerName, string ipAddress, Turn turn )
        {
            this.SelectedCard = -1;
            this.BeginCommunication = false;
            this.ServerIP = ipAddress;
            this.HasDrawn = false;

            //// Play theme song.
            //mediaPlayer = new MediaPlayer();
            //CreateNewThread(new Action<Object>(PlaySong));
            
            // Instantiate the Player's Turn object.
            this.Turn = turn;

            // Instantiate the MonopolyData object.
            InstantiateMonopolyData();

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

            CheckIfCurrentTurnOwner();

            InitializeComponent();

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

            //EnableButtonsIfTurnOwner(null);

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

                        case Datatype.EndTurn:
                        {
                            this.Turn = (Turn)ServerUtilities.ReceiveMessage(inc, messageType);

                            // Check to see if the player is the current turn owner. 
                            CheckIfCurrentTurnOwner();

                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

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
            if ( FindPlayerPositionInPlayerList(this.Player.Name) == this.Turn.CurrentTurnOwner && this.Turn.NumberOfActions < 3 && this.HasDrawn )
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
                        cardWasPlayed = AddCardToCardsInPlay(cardBeingPlayed);
                    }
                    else
                    {
                        // Add the card to the DiscardPile and display it.
                        this.DiscardPile.Add(cardBeingPlayed);
                        AddCardToGrid(cardBeingPlayed, DiscardPileGrid, this.Player, false);
                        cardWasPlayed = true;
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

            if ( buttonIndex != SelectedCard )
            {
                // Deselect the currently selected card.
                if ( SelectedCard != -1 )
                {
                    DeselectCard(FindButtonInGrid(SelectedCard, PlayerOneHand));
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
            // In this game, only two cards can be drawn at a time.
            DrawCards(2);
            this.HasDrawn = true;
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

            // Reset the status of the HasDrawn variable so that it is false at the beginning of this player's next turn.
            this.HasDrawn = false;

            // Send the updated Turn object to the server to be distributed to the other clients.
            ServerUtilities.SendMessage(Client, Datatype.EndTurn, this.Turn);
        }

        // Use this event to respond to key presses.
        private void Window_KeyDown( object sender, KeyEventArgs e )
        {

        }

        #endregion

        #region Hand and Field Manipulation

        // Update the value of the IsCurrentTurnOwner boolean.
        public void CheckIfCurrentTurnOwner()
        {
            if ( this.Turn.CurrentTurnOwner == FindPlayerPositionInPlayerList(this.Player.Name) )
            {
                IsCurrentTurnOwner = true;
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

            //foreach ( List<Card> cardList in Player.CardsInPlay )
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
            if ( (cardButton.Tag as Card).Type != CardType.Money )
            {
                TransformCardButton(cardButton, 0, 0);
            }

            InfoBox.Children.Add(cardButton);
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

        // Display the cards of the player's opponents.
        public void DisplayOpponentCards( Object filler )
        {
            foreach ( Player player in PlayerList )
            {
                Grid playerHand = null;
                Grid playerField = null;

                // Choose a logical position for each player on the client's screen.
                switch ( GetRelativePosition(Player.Name, player.Name) )
                {
                    case 1:
                    {
                        playerHand = PlayerTwoHand;
                        playerField = PlayerTwoField;
                        break;
                    }

                    case 2:
                    {
                        playerHand = PlayerThreeHand;
                        playerField = PlayerThreeField;
                        break;
                    }

                    case 3:
                    {
                        playerHand = PlayerFourHand;
                        playerField = PlayerFourField;
                        break;
                    }
                }

                //// If the opponent is found, display his cards.
                if ( playerHand != null )
                {
                    // Update the display of the opponent's cards in play.
                    DisplayCardsInPlay(player, playerField);

                    // Update the display of the opponent's hand.
                    ClearCardsInGrid(playerHand);
                    foreach ( Card card in player.CardsInHand )
                    {
                        Card cardBack = new Card(-1, "pack://application:,,,/GameObjects;component/Images/cardback.jpg");
                        AddCardToGrid(cardBack, playerHand, player, true);
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
        public bool AddCardToCardsInPlay( Card cardBeingAdded )
        {
            // Check if any cards currently in play match the color of the card being played (if the card is a property).
            // If it does, lay the card being played over the matching cards.
            if ( cardBeingAdded.Type == CardType.Property )
            {
                if ( "House" == cardBeingAdded.Name || "Hotel" == cardBeingAdded.Name )
                {
                    // Add the house to an existing monopoly or the hotel to a monopoly that has a house. The card should not be removed from the player's hand
                    // unless it is placed in a monopoly.
                    List<List<Card>> monopolies = FindMonopolies(Player);

                    if ( "House" == cardBeingAdded.Name )
                    {
                        foreach ( List<Card> monopoly in monopolies )
                        {
                            if ( !IsCardInCardList("House", monopoly  ) )
                            {
                                monopoly.Add(cardBeingAdded);
                                AddCardToGrid(cardBeingAdded, PlayerOneField, this.Player, false, Player.CardsInPlay.IndexOf(monopoly));

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
                                AddCardToGrid(cardBeingAdded, PlayerOneField, this.Player, false, Player.CardsInPlay.IndexOf(monopoly));

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

                    foreach ( List<Card> cardList in FindMonopolies(Player) )
                    {
                        colorsOfCurrentMonopolies.Add(FindCardListColor(cardList));
                    }

                    if ( PropertyType.Wild != cardBeingAdded.Color )
                    {
                        if ( colorsOfCurrentMonopolies.Contains(cardBeingAdded.Color) )
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // A Property Wild Card (that is, the card that is compatible with all properties) cannot be added to 
                        // the player's CardsInPlay unless there is a list of property cards (that are not a monopoly) already there.
                        // That is, a Property Wild Card can never be by itself on the field.
                        if ( colorsOfCurrentMonopolies.Count == Player.CardsInPlay.Count - 1 )
                        {
                            return false;
                        }
                    }

                    // If the card has passed the previous check, add it to the player's CardsInPlay.
                    for ( int i = 1; i < Player.CardsInPlay.Count; ++i )
                    {
                        List<Card> cardList = Player.CardsInPlay[i];

                        PropertyType cardListColor = FindCardListColor(cardList);

                        // If the cardlist is not a monopoly and is compatible with the card being added, add the card to the list.
                        if ( !IsCardListMonopoly(cardList) && ( cardListColor == cardBeingAdded.Color || PropertyType.Wild == cardBeingAdded.Color ) )
                        {
                            cardList.Add(cardBeingAdded);
                            AddCardToGrid(cardBeingAdded, PlayerOneField, this.Player, false, Player.CardsInPlay.IndexOf(cardList));

                            return true;
                        }
                    }
                }

                // If this code is reached, the card must not have matched any existing properties.
                List<Card> newCardList = new List<Card>();
                newCardList.Add(cardBeingAdded);
                Player.CardsInPlay.Add(newCardList);
                AddCardToGrid(cardBeingAdded, PlayerOneField, this.Player, false);
                return true;
            }
            else if ( cardBeingAdded.Type == CardType.Money )
            {
                Player.CardsInPlay[0].Add(cardBeingAdded);
                AddCardToGrid(cardBeingAdded, PlayerOneField, this.Player, false);
                return true;
            }

            return false;
        }

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
                // If it is an action card, it can be played in one of two ways.
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
                    MenuItem nextMonopolyMenuItem = new MenuItem();
                    nextMonopolyMenuItem.Header = "Place on Next Monopoly";
                    nextMonopolyMenuItem.Click += ( sender, args ) =>
                    {
                    };

                    menu.Items.Add(playAsActionMenuItem);
                    menu.Items.Add(playAsMoneyMenuItem);
                    menu.Items.Add(nextMonopolyMenuItem);
                    cardButton.ContextMenu = menu;
                }
                else
                {
                    ContextMenu menu = new ContextMenu();
                    MenuItem playAsActionMenuItem = new MenuItem();
                    playAsActionMenuItem.Header = "Play as Action";
                    playAsActionMenuItem.Click += ( sender, args ) =>
                    {
                        PlayCardEvent(cardButton, null);
                    };
                    MenuItem playAsMoneyMenuItem = new MenuItem();
                    playAsMoneyMenuItem.Header = "Play as Money";
                    playAsMoneyMenuItem.Click += ( sender2, args2 ) =>
                    {
                        cardBeingAdded.Type = CardType.Money;
                        PlayCardEvent(cardButton, null);
                    };

                    menu.Items.Add(playAsActionMenuItem);
                    menu.Items.Add(playAsMoneyMenuItem);
                    cardButton.ContextMenu = menu;
                }
            }
            else
            {
                // Create context menu that allows the user to reorder properties and money cards (not action cards).
                // This if statement is required to prevent players from seeing the context menu of other players' cards in play.
                if ( grid == PlayerOneField && cardBeingAdded.Type != CardType.Action )
                {
                    ContextMenu menu = new ContextMenu();
                    MenuItem forwardMenuItem = new MenuItem();
                    forwardMenuItem.Header = "Move Forward";
                    forwardMenuItem.Click += ( sender, args ) =>
                    {
                        MoveCardInList(cardBeingAdded, 1);
                    };
                    MenuItem backwardMenuItem = new MenuItem();
                    backwardMenuItem.Header = "Move Backward";
                    backwardMenuItem.Click += ( sender2, args2 ) =>
                    {
                        MoveCardInList(cardBeingAdded, -1);
                    };

                    // If it is a two-color property card, allow the player to flip it.
                    if ( HasAltColor(cardBeingAdded) )
                    {
                        MenuItem flipMenuItem = new MenuItem();
                        flipMenuItem.Header = "Flip Card";
                        flipMenuItem.Click += ( sender2, args2 ) =>
                        {
                            // Check to see if the flipped card can be added to the player's CardsInPlay.
                            // If it cannot, do not do anything.
                            List<PropertyType> colorsOfCurrentMonopolies = new List<PropertyType>();
                            foreach ( List<Card> cardList in FindMonopolies(Player) )
                            {
                                colorsOfCurrentMonopolies.Add(FindCardListColor(cardList));
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
                            RemoveCardFromCardsInPlay(cardBeingAdded, this.Player);
                            AddCardToCardsInPlay(cardBeingAdded);

                            // Displayed the updated CardsInPlay.
                            DisplayCardsInPlay(this.Player, PlayerOneField);

                            // Update the server.
                            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, this.Player);

                        };
                        menu.Items.Add(flipMenuItem);
                    }
                    
                    menu.Items.Add(forwardMenuItem);
                    menu.Items.Add(backwardMenuItem);
                    cardButton.ContextMenu = menu;
                }

                // Play the card in a certain way depending on its type (i.e. property, money, or action).
                switch ( cardBeingAdded.Type )
                {
                    case CardType.Property:
                    {
                        // Testing new approach to adding cards to grid
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
                        //Play money cards horizontally.
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

        private void cardButton_PreviewMouseMove( object sender, MouseEventArgs e )
        {
            Button cardButton = sender as Button;
            if ( cardButton != null && e.LeftButton == MouseButtonState.Pressed )
            {
                DragDrop.DoDragDrop(cardButton, cardButton, DragDropEffects.Move);
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

        // Find a card in the player's hand given its position in the grid.
        private Button FindButtonInGrid( int buttonIndex, Grid grid )
        {
            if ( buttonIndex != -1 )
            {
                for ( int i = 0; i < grid.Children.Count; ++i )
                {
                    Button cardButton = (grid.Children[i] as Grid).Tag as Button;
                    if ( Grid.GetColumn((grid.Children[i] as Grid)) == buttonIndex )
                    {
                        return cardButton;
                    }
                }
            }
            return null;
        }

        // Increase the size of the currently selected card.
        public void SelectCard( Button cardButton )
        {
            if ( cardButton != null )
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = 1.2;
                myScaleTransform.ScaleX = 1.2;

                //cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                //cardButton.RenderTransform = myScaleTransform;

                (cardButton.RenderTransform as TransformGroup).Children.Add(myScaleTransform);
            }
        }

        // Set the currently selected card to its normal size.
        public void DeselectCard( Button cardButton )
        {
            if ( cardButton != null )
            {
                // Instantiating an object of type "ScaleTransform" should not be necessary.
                // TODO: Find a way to pass the "ScaleTransform" without having to instantiate an object.
                ScaleTransform scaleTransform = new ScaleTransform();
                RemoveTransformTypeFromGroup(scaleTransform.GetType(), cardButton.RenderTransform as TransformGroup);


                //ScaleTransform myScaleTransform = new ScaleTransform();
                //myScaleTransform.ScaleY = 1;
                //myScaleTransform.ScaleX = 1;

                //cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                //cardButton.RenderTransform = myScaleTransform;
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

        #region Miscellaneous

        // Create a new thread to run a function that cannot be run on the same thread invoking CreateNewThread().
        public void CreateNewThread( Action<Object> action )
        {
            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, action, null); };
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
        public PropertyType FindCardListColor( List<Card> cardList )
        {
            foreach ( Card card in cardList )
            {
                if ( PropertyType.None != card.Color && PropertyType.Wild != card.Color )
                {
                    return card.Color;
                }
            }
            return PropertyType.None;

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
            PropertyType monopolyColor = FindCardListColor(cardList);
            int countOfProperties = 0;

            // First count the number of properties in the list. This algorithm excludes houses and hotels from the count.
            foreach ( Card card in cardList )
            {
                if ( card.Color != PropertyType.None )
                {
                    countOfProperties++;
                }
            }

            if ( countOfProperties == MonopolyData[monopolyColor] )
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

        // For each type of property, store the number of cards of that property type that make up a complete monopoly
        // ROBIN TODO: Think of a way to do this without hardcoding the data.
        public void InstantiateMonopolyData()
        {
            MonopolyData = new Dictionary<PropertyType, int>();
            MonopolyData.Add(PropertyType.Blue, 2);
            MonopolyData.Add(PropertyType.Brown, 2);
            MonopolyData.Add(PropertyType.Green, 3);
            MonopolyData.Add(PropertyType.LightBlue, 3);
            MonopolyData.Add(PropertyType.Orange, 3);
            MonopolyData.Add(PropertyType.Pink, 3);
            MonopolyData.Add(PropertyType.Railroad, 4);
            MonopolyData.Add(PropertyType.Red, 3);
            MonopolyData.Add(PropertyType.Utility, 2);
            MonopolyData.Add(PropertyType.Wild, 0);
            MonopolyData.Add(PropertyType.Yellow, 3);
            MonopolyData.Add(PropertyType.None, -1);
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
