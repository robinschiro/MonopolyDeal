using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using GameObjects;

namespace AdditionalWindows
{
    /// <summary>
    /// Interaction logic for PlayerSelectionDialog.xaml
    /// </summary>
    public partial class PlayerSelectionDialog : Window
    {
        public Player SelectedPlayer;
        public bool enoughPlayers = true;

        public PlayerSelectionDialog( string selector, List<Player> playerList )
        {
            InitializeComponent();

            List<Player> modifiedPlayerList = new List<Player>(playerList.Where(player => player.Name != selector));

            // Load the listbox with the players and select the first player on the list.
            if ( modifiedPlayerList.Count > 0 )
            {
                PlayerListBox.ItemsSource = modifiedPlayerList;
                PlayerListBox.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show("No other players!");
                this.enoughPlayers = false;
            }
        }

        #region Events

        private void SelectPlayer_Click( object sender, RoutedEventArgs e )
        {
            SelectedPlayer = (Player)PlayerListBox.SelectedItem;
            this.DialogResult = true;
            this.Close();
        }

        #endregion
    }
}
