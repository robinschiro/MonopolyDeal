using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using GameObjects;
using GameServer;
using Lidgren.Network;
using AdditionalWindows;

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
        bool UpdateReceived = false;
        private bool Disconnected = false;
        private List<Player> PlayerList;
        private volatile Turn Turn;
        private string PlayerName;
        private SendOrPostCallback Callback;
        private MessageDialog WaitMessage;

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
            this.PlayerName = playerName;

            InitializeClient(ipAddress);

            // Do not continue until the client has successfully established communication with the server.
            WaitMessage = new MessageDialog("Please Wait...", "Waiting to establish communication with server...", isModal: false);
            if (!this.BeginCommunication)
            {
                WaitMessage.ShowDialog();  
            }

            // Create a Wait dialog to stop the creation of the room window until the player list is retrieved from the server.
            WaitMessage = new MessageDialog("Please Wait...", "Waiting for player list...", isModal: false);

            // Receive a list of the players already on the server.
            ServerUtilities.SendMessage(Client, Datatype.RequestPlayerList);

            // Do not continue until the client receives the Player List from the server.
            WaitMessage.ShowDialog();

            // Verify that the value of 'playerName' does not exist in the list of Player names.
            // If it does, modify the name so that it no longer matches one on the list.
            this.PlayerName = VerifyPlayerName(this.PlayerName);

            // Instantiate the player.
            this.Player = new Player(this.PlayerName);

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
            Client.Connect(ipAddress, ServerUtilities.PORT_NUMBER, outmsg);

            // Create the synchronization context used by the client to receive updates as soon as they are available.
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            // Register the 'GotMessage' method as a callback function for received updates.
            this.Callback = new SendOrPostCallback(GotMessage);
            Client.RegisterReceivedCallback(this.Callback);
        }

        // Update the client's window based on messages received from the server. This method is called as soon as the client
        // receives a message.
        public void GotMessage( object peer )
        {
            NetIncomingMessage inc;

            if ( false == this.BeginCommunication )
            {
                // Continue reading messages until the requested update is received.
                while ( !UpdateReceived && !this.Disconnected )
                {
                    Console.WriteLine(this.PlayerName + " stuck in begin");

                    // Iterate through all of the available messages.
                    while ( (inc = (peer as NetPeer).ReadMessage()) != null )
                    {
                        if ( inc.MessageType == NetIncomingMessageType.StatusChanged )
                        {
                            this.BeginCommunication = true;
                            UpdateReceived = true;

                            // Close the wait message if it is open.
                            if ( null != WaitMessage && !WaitMessage.CloseWindow )
                            {
                                CreateNewThread(new Action<Object>(( sender ) => { WaitMessage.CloseWindow = true; }));
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

                if ( null != inc && inc.MessageType == NetIncomingMessageType.Data )
                {
                    Datatype messageType = (Datatype)inc.ReadByte();
                    //MessageBox.Show(this.PlayerName + " received " + messageType.ToString());

                    switch ( messageType )
                    {
                        case Datatype.UpdatePlayerList:
                        {
                            PlayerList = (List<Player>)ServerUtilities.ReceiveMessage(inc, messageType);

                            if ( null != this.PlayerList && this.PlayerList.Count > 0 )
                            {
                                CreateNewThread(new Action<Object>(( sender ) => { this.LaunchGameButton.IsEnabled = (this.PlayerList[0].Name == this.PlayerName); }));
                            }

                            //// The parentheses around currentPlayerList are required. See http://bit.ly/19t9NEx.
                            //ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<List<Player>>(UpdatePlayerListBox), (playerList)); };
                            CreateNewThread(new Action<Object>(UpdatePlayerListBox));

                            // Close the wait message if it is open.
                            if  ( null != WaitMessage && !WaitMessage.CloseWindow )
                            {
                                CreateNewThread(new Action<Object>(( sender ) => { WaitMessage.CloseWindow = true; }));
                            }

                            break;
                        }

                        case Datatype.LaunchGame:
                        {
                            Console.WriteLine(this.Player.Name + " received Launch");

                            // Receive the data related to the current turn from the server.
                            this.Turn = (Turn)ServerUtilities.ReceiveMessage(inc, Datatype.LaunchGame);

                            if ( this.PlayerList[0].Name == this.PlayerName )
                            {
                                CreateNewThread(new Action<Object>(LaunchGame));
                            }

                            break;
                        }

                        case Datatype.TimeToConnect:
                        {
                            string playerToConnect = (String)ServerUtilities.ReceiveMessage(inc, messageType);

                            if ( this.PlayerName == playerToConnect )
                            {
                                CreateNewThread(new Action<Object>(LaunchGame));
                            }

                            break;
                        }
                    }
                }
            }
        }

        private void LaunchGame( Object filler = null )
        {
            // ROBIN: I previously attempted to pass the Client object to the GameWindow's constructor. As a result (for an unknown reason), 
            // the Client's callback messages were not registering. Therefore, each client is disconnected from the server before launching the game.
            // A new Client object is created and connected to the server when the GameWindow is constructed.
            Client.Disconnect("Bye");
            Client.UnregisterReceivedCallback(this.Callback);
            this.Disconnected = true;

            GameWindow gameWindow = new GameWindow(this.Player.Name, this.ServerIP, this.Turn);
            gameWindow.Show();
            this.Close();
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

        // Create a new thread to run a function that cannot be run on the same thread invoking CreateNewThread().
        public void CreateNewThread( Action<Object> action, object data = null )
        {
            ThreadStart start = delegate() { Dispatcher.Invoke(DispatcherPriority.Normal, action, data); };
            Thread newThread = new Thread(start);
            newThread.Start();
        }

        #endregion

    }
}
