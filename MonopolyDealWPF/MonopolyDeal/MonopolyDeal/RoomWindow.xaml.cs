using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using GameObjects;
using GameServer;
using Lidgren.Network;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for RoomWindow.xaml
    /// </summary>
    public partial class RoomWindow : Window
    {
        private volatile NetClient Client;
        private Player Player;
        private string ServerIP;
        private bool BeginCommunication;
        private List<Player> PlayerList;
        private volatile Turn Turn;

        //// This is part of a failed attempt to use Binding. Binding currently does not work because non-UI threads cannot change the contents of
        //// observable collections. We should keep this code here in case we want to revisit binding in the future.
        //public event PropertyChangedEventHandler PropertyChanged;

        //public ObservableCollection<Player> PlayerList
        //{
        //    get
        //    {
        //        if ( playerList != null )
        //        {
        //            return new ObservableCollection<GameObjects.Player>(playerList);
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //    }
        //    set
        //    {
        //        playerList.Clear();
        //        foreach ( Player player in value )
        //        {
        //            playerList.Add(player);
        //        }
        //        OnPropertyChanged("PlayerList");
        //    }
        //}

        //// Create the OnPropertyChanged method to raise the event 
        //protected void OnPropertyChanged( string name )
        //{
        //    PropertyChangedEventHandler handler = PropertyChanged;
        //    if ( handler != null )
        //    {
        //        handler(this, new PropertyChangedEventArgs(name));
        //    }
        //}

        public RoomWindow( string ipAddress, string playerName )
        {
            InitializeComponent();
            this.DataContext = this;
            this.ServerIP = ipAddress;
            this.BeginCommunication = false;
            //this.PlayerList = new ObservableCollection<GameObjects.Player>();

            InitializeClient(ipAddress);

            // Do not continue until the client has successfully established communication with the server.
            while ( !this.BeginCommunication ) ;

            // Receive a list of the players already on the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestPlayerList);

            // Do not continue until the client receives the Player List from the server.
            while ( this.PlayerList == null ) ;

            // Verify that the value of 'playerName' does not exist in the list of Player names.
            // If it does, modify the name so that it no longer matches one on the list.
            playerName = VerifyPlayerName(playerName);

            // Instantiate the player.
            this.Player = new Player(playerName);

            // Send the player's information to the server.
            ServerUtilities.SendMessage(Client, Datatype.UpdatePlayer, Player);
        }

        private void InitializeClient( string ipAddress )
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

            // Connect client, to ip previously requested from user.
            Client.Connect(ipAddress, 14242, outmsg);

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

                        Thread.Sleep(100);
                    }

                    Thread.Sleep(100);
                }
            }
            else
            {
                inc = (peer as NetPeer).ReadMessage();

                if ( null != inc && inc.MessageType == NetIncomingMessageType.Data )
                {
                    Datatype messageType = (Datatype)inc.ReadByte();

                    switch ( messageType )
                    {
                        case Datatype.UpdatePlayerList:
                        {
                            PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);

                            //// The parentheses around currentPlayerList are required. See http://bit.ly/19t9NEx.
                            //ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<List<Player>>(UpdatePlayerListBox), (playerList)); };
                            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Object>(UpdatePlayerListBox), null); };
                            Thread newThread = new Thread(start);
                            newThread.Start();

                            break;
                        }

                        case Datatype.LaunchGame:
                        {
                            // Receive the data related to the current turn from the server.
                            Turn Turn = (Turn)ServerUtilities.ReceiveMessage(inc, Datatype.LaunchGame);

                            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Turn>(LaunchGame), Turn); };
                            Thread newThread = new Thread(start);
                            newThread.SetApartmentState(ApartmentState.STA);
                            newThread.Start();

                            break;
                        }
                    }
                }
            }
        }

        private void LaunchGame( Turn turn )
        {
            // ROBIN: I previously attempted to pass the Client object to the GameWindow's constructor. As a result (for an unknown reason), 
            // the Client's callback messages were not registering. Therefore, each client is disconnected from the server before launching the game.
            // A new Client object is created and connected to the server when the GameWindow is constructed.
            Client.Disconnect("Bye");

            GameWindow gameWindow = new GameWindow(this.Player.Name, this.ServerIP, turn);
            gameWindow.Show();
            Close();
        }

        private void UpdatePlayerListBox( Object filler )
        {
            PlayerListBox.Items.Clear();

            foreach ( Player player in PlayerList )
            {
                PlayerListBox.Items.Add(player);
            }
        }

        #region Events

        private void LaunchGameButton_Click( object sender, RoutedEventArgs e )
        {
            ServerUtilities.SendMessage(Client, Datatype.LaunchGame);
        }

        #endregion

        #region Miscellaneous

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
