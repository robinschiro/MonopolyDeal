using System;
using System.Windows;
using System.Windows.Controls;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for LaunchScreen.xaml
    /// </summary>
    public partial class LaunchScreen : Window
    {
        public LaunchScreen()
        {
            InitializeComponent();
            this.Title = "Monopoly Deal Setup";
            this.OKButton.Focus();
        }

        private void OKButton_Click( object sender, RoutedEventArgs e )
        {
            RoomWindow roomWindow = new RoomWindow(IPAddress.Text, PlayerName.Text);
            if ( roomWindow.ShowWindow )
            {
                roomWindow.Show();
            }
            this.Close();
        }

        // The OKButton is enabled only when text exists in the IPAddress textbox.
        private void IPAddress_TextChanged( object sender, TextChangedEventArgs e )
        {
            ValidateTextFields();            
        }

        private void PlayerName_TextChanged( object sender, TextChangedEventArgs e )
        {
            ValidateTextFields();
        }

        private void ValidateTextFields()
        {
            // The outer try block is necessary because this event is initially fired before the OKButton exists.
            try
            {
                if ( !String.IsNullOrWhiteSpace(IPAddress.Text) && !String.IsNullOrWhiteSpace(PlayerName.Text) )
                {
                    OKButton.IsEnabled = true;
                }
                else
                {
                    OKButton.IsEnabled = false;
                }
            }
            catch { }
        }
    }
}
