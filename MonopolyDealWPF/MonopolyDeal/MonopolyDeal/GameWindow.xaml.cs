using System;
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

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window, IGameClient
    {
        private Deck Deck;
        private Player Player;
        private List<Player> PlayerList;
        private String ServerIP;
        private int SelectedCard;
        private bool BeginCommunication;
        private volatile NetClient Client;
        private List<Grid> PlayerFields;
        private Turn Turn;
        private List<Card> DiscardPile;

        // TODO: The disabling/enabling of buttons should be implemented through binding.
        private bool IsCurrentTurnOwner
        {
            get
            {
                return (this.Turn.CurrentTurnOwner == FindPlayerPositionInPlayerList(this.Player.Name));
            }
        }

        private bool HasDrawn;
            
        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

        public GameWindow( string playerName, string ipAddress, Turn turn )
        {
            InitializeComponent();
            this.SelectedCard = -1;
            this.BeginCommunication = false;
            this.ServerIP = ipAddress;
            this.HasDrawn = false;

            // Add the four player fields to the list.
            this.PlayerFields = new List<Grid>();
            this.PlayerFields.Add(PlayerOneField);
            this.PlayerFields.Add(PlayerTwoField);
            this.PlayerFields.Add(PlayerThreeField);
            this.PlayerFields.Add(PlayerFourField);

            // Instantiate the Player's Turn object.
            this.Turn = turn;

            // Instantiate the DiscardPile.
            this.DiscardPile = new List<Card>();

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
                AddCardToGrid(card, PlayerOneHand, Player, true);
            }

            EnableButtonsIfTurnOwner(null);

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

                                CreateNewThread(new Action<Object>(EnableButtonsIfTurnOwner));

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
            if ( FindPlayerPositionInPlayerList(this.Player.Name) == this.Turn.CurrentTurnOwner && this.Turn.NumberOfActions < 3 && this.HasDrawn )
            {
                Button cardButton = FindButtonInGrid(SelectedCard, PlayerOneHand);

                if ( -1 != SelectedCard && sender == cardButton )
                {
                    Card cardBeingPlayed = cardButton.Tag as Card;
                    RemoveCardFromHand(cardButton);

                    // Update the value of 'SelectedCard' (no card is selected after a card is played).
                    SelectedCard = -1;

                    // Add the card to the Player's playing field (if it is not an action card).
                    if ( cardBeingPlayed.Type != CardType.Action )
                    {
                        // Add the card to the Player's CardsInPlay list.
                        AddCardToCardsInPlay(cardBeingPlayed);
                        AddCardToGrid(cardBeingPlayed, PlayerOneField, this.Player, false);
                    }
                    else
                    {
                        // Add the card to the DiscardPile and display it.
                        this.DiscardPile.Add(cardBeingPlayed);
                        AddCardToGrid(cardBeingPlayed, DiscardPileGrid, this.Player, false);
                    }

                    // Update the player's number of actions.
                    this.Turn.NumberOfActions++;

                    // Update the server's information regarding this player.
                    ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);

                    // Update the server's discard pile.
                    ServerUtilities.SendMessage(Client, Datatype.UpdateDiscardPile, DiscardPile);
                }
            }
        }

        //1 is absolute maxima/minima (saddle points, relative max/min)
        //6 are from Ch. 15

        // When a card is added to the Player's CardsInPlay, it must be placed in the same list as compatible properties
        // that have already been played. If no compatible properties have been played, a new list is created for the card.
        public void AddCardToCardsInPlay( Card cardBeingAdded )
        {
            // Check if any cards currently in play match the color of the card being played (if the card is a property).
            // If it does, lay the card being played over the matching cards.
            if ( cardBeingAdded.Type == CardType.Property )
            {
                foreach ( List<Card> cardList in Player.CardsInPlay )
                {
                    //Grid cardGrid = element as Grid;
                    bool propertyMatchesSet = true;

                    foreach ( Card cardInList in cardList )
                    {
                        // Check the compatibility of the two properties.
                        if ( !CheckPropertyCompatibility(cardInList, cardBeingAdded) )
                        {
                            propertyMatchesSet = false;
                            break;
                        }
                    }

                    if ( propertyMatchesSet )
                    {
                        cardList.Add(cardBeingAdded);
                        return;
                    }
                }
            }

            // If this code is reached, the card must not have matched any existing properties.
            List<Card> newCardList = new List<Card>();
            newCardList.Add(cardBeingAdded);
            Player.CardsInPlay.Add(newCardList);
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
                InfoBox.Children.Clear();
                InfoBox.Children.Add(ConvertCardToButton((sender as Button).Tag as Card));
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
            
            // Disable the Draw Cards button once it has been clicked.
            DrawCardButton.IsEnabled = false;
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

        // Enable or disable certain buttons depending on whether or not the player is the current turn owner.
        public void EnableButtonsIfTurnOwner( Object filler )
        {
            if ( IsCurrentTurnOwner )
            {
                EndTurnButton.IsEnabled = true;
                DrawCardButton.IsEnabled = true;
            }
            else
            {
                EndTurnButton.IsEnabled = false;
                DrawCardButton.IsEnabled = false;
            }
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
        public void DisplayPlayerCardsInPlay()
        {
            ClearCardsInGrid(PlayerOneField);
            foreach ( List<Card> cardList in Player.CardsInPlay )
            {
                foreach ( Card card in cardList )
                {
                    AddCardToGrid(card, PlayerOneField, Player, false);
                }
            }
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
        public void DisplayOpponentCards(Object filler)
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
                    ClearCardsInGrid(playerField);
                    foreach ( List<Card> cardList in player.CardsInPlay )
                    {
                        foreach ( Card card in cardList )
                        {
                            //AddCardToCardsInPlay(card);
                            AddCardToGrid(card, playerField, player, false);
                        }
                    }

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
        public bool CheckPropertyCompatibility(Card propOne, Card propTwo)
        {
            if ( propOne.Type != CardType.Property || propTwo.Type != CardType.Property )
            {
                return false;
            }

            // There is probably a better way to compare the cards' PropertyTypes, but this works for now.
            if ( ( (propOne.Color == propTwo.Color) && (propOne.Color != PropertyType.None) )           ||
                    ( (propOne.AltColor == propTwo.AltColor) && (propOne.AltColor != PropertyType.None) )  || 
                    ( (propOne.Color == propTwo.AltColor) && (propOne.Color != PropertyType.None) )        ||
                    ( (propOne.AltColor== propTwo.Color) && (propOne.Color != PropertyType.None) )         ||
                    ( (propOne.Color == PropertyType.Wild) || (propTwo.Color == PropertyType.Wild) ) )
            {
                return true;
            }

            return false;
            
        }
        

        // Add a card to a specified grid.
        public void AddCardToGrid( Card cardBeingAdded, Grid grid, Player player, bool isHand )
        {
            Button cardButton = ConvertCardToButton(cardBeingAdded);

            // If a card is being added to the client's hand, attach these events to it and display it.
            if ( isHand )
            {
                cardButton.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(SelectCardEvent);
                cardButton.PreviewMouseRightButtonDown += new MouseButtonEventHandler(PlayCardEvent);
            }
            else
            {
                // Create context menu that allows the user to reorder properties and money cards (not action cards).
                // This if statement is required to prevent players from seeing the context menu of other players' cards in play.
                if ( grid == PlayerOneField && cardBeingAdded.Type != CardType.Action)
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
                    menu.Items.Add(forwardMenuItem);
                    menu.Items.Add(backwardMenuItem);
                    cardButton.ContextMenu = menu;
                }
                
                // Play the card in a certain way depending on its type (i.e. property, money, or action).
                switch ( cardBeingAdded.Type )
                {
                    case CardType.Property:
                    {
                        // Check if any cards currently in play match the color of the card being played (if the card is a property).
                        // If it does, lay the card being played over the matching cards.
                        foreach ( FrameworkElement element in grid.Children )
                        {
                            // A 'cardGrid' is either a single card or group of cards (stacked on top of each other) currently
                            // on the playing field.
                            Grid cardGrid = element as Grid;
                            bool propertyMatchesSet = true;

                            foreach ( FrameworkElement innerElement in cardGrid.Children )
                            {
                                Button existingCardButton = (Button)innerElement;
                                Card cardInGrid = existingCardButton.Tag as Card;

                                // Check the compatibility of the two properties.
                                if ( !CheckPropertyCompatibility(cardInGrid, cardBeingAdded) )
                                {
                                    propertyMatchesSet = false;
                                    break;
                                }
                            }

                            if ( propertyMatchesSet )
                            {
                                // Lay properties of compatible colors on top of each other (offset vertically).
                                TransformCardButton(cardButton, cardGrid.Children.Count, grid.ActualHeight);


                                // ROBIN TODO: Widen the gap between the player's hand and his playing field.
                                // Add the new card to the cardGrid that contains the matching card on the field.
                                cardGrid.Children.Add(cardButton);
                                Grid.SetColumn(cardButton, 1);

                                return;
                            }
                        }

                        break;
                    }

                    case CardType.Money:
                    {

                        // Check if any cards currently in play match the color of the card being played (if the card is a property).
                        // If it does, lay the card being played over the matching cards.
                        foreach (FrameworkElement element in grid.Children)
                        {
                            // A 'cardGrid' is either a single card or group of cards (stacked on top of each other) currently on the playing field.
                            Grid cardGrid = element as Grid;

                            foreach (FrameworkElement innerElement in cardGrid.Children)
                            {
                                Button existingCardButton = (Button)innerElement;
                                Card cardInGrid = existingCardButton.Tag as Card;

                                // Check to see if the found card is a money card. There should only be one group of money cards (the first group on the playing field)
                                if (cardInGrid.Type == CardType.Money)
                                {
                                    // Play money cards horizontally.
                                    TransformCardButton(cardButton, 0, 0);

                                    cardGrid.Children.Add(cardButton);
                                    Grid.SetColumn(cardButton, 1);

                                    return;
                                }
                            }

                            //If this reached, that means that no money cards have yet been played.

                        }

                        break;
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
                if ( cardBeingAdded.Type == CardType.Property)
                {
                    // If at least one money card has been played, set the column to grid.Children.Count - 1. Otherwise, do not subtract 1.
                    if (CountOfCardTypeInGrid(CardType.Money, grid) > 0)
                    {
                        Grid.SetColumn(cardGridWrapper, grid.Children.Count - 1);
                    }
                    else
                    {
                        Grid.SetColumn(cardGridWrapper, grid.Children.Count);
                    }
                }
                else if ( cardBeingAdded.Type == CardType.Money )
                {
                    TransformCardButton(cardButton, 0, 0);
                    Grid.SetColumn(cardGridWrapper, 0);
                }
            }
        }

        public void TransformCardButton( Button cardButton, int numberOfMatchingCards, double gridHeight )
        {
            Card card = cardButton.Tag as Card;

            switch ( card.Type )
            {
                case CardType.Property:
                {
                    // Lay properties of compatible colors on top of each other (offset vertically).
                    TranslateTransform myTranslateTransform = new TranslateTransform();
                    myTranslateTransform.Y = (numberOfMatchingCards * .10) * gridHeight;
                    cardButton.RenderTransform = myTranslateTransform;

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

                cardButton.RenderTransformOrigin = new Point(0.5, 0.5);
                cardButton.RenderTransform = myScaleTransform;
            }
        }

        // Set the currently selected card to its normal size.
        public void DeselectCard( Button cardButton )
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

        #endregion

        #region Miscellaneous

        // Create a new thread to run a function that cannot be run on the same thread invoking CreateNewThread().
        public void CreateNewThread( Action<Object> action )
        {
            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, action, null); };
            Thread newThread = new Thread(start);
            newThread.Start();
        }

        public void MoveItemInList<T>(IList<T> list, int oldIndex, int newIndex)
        {
            var item = list[oldIndex];

            list.RemoveAt(oldIndex);

            //if ( newIndex > oldIndex ) newIndex--;
            // the actual index could have shifted due to the removal

            if ( newIndex < list.Count && newIndex >= 0 )
            {
                list.Insert(newIndex, item);
            }
            else
            {
                list.Add(item);
            }
        }

        public void MoveCardInList( Card cardBeingMoved, int numberOfSpaces )
        {
            foreach ( List<Card> cardList in Player.CardsInPlay )
            {
                for ( int i = 0; i < cardList.Count; ++i )
                {
                    if ( cardList[i] == cardBeingMoved )
                    {
                        MoveItemInList<Card>(cardList, i, i + numberOfSpaces);
                        DisplayPlayerCardsInPlay();

                        // Update the server.
                        ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
                        return;
                    }
                }
            }
        }

        public List<Card> FindListContainingCard( Card cardBeingFound )
        {
            foreach ( List<Card> cardList in Player.CardsInPlay )
            {
                foreach (Card card in cardList)
                {
                    if ( card == cardBeingFound )
                    {
                        return cardList;
                    }
                }
            }

            return null;
        }

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

        // This returns the number of a type of card (i.e. money, action, or property) found within
        // a provided list of card lists.
        private int CountOfCardTypeInList(CardType cardType, List<List<Card>> cardGroupList)
        {
            int count = 0;

            foreach (List<Card> cardGroup in cardGroupList)
            {
                foreach (Card card in cardGroup)
                {
                    if (card.Type == cardType)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

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
