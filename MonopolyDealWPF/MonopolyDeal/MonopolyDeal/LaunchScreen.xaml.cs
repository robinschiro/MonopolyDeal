using System;
using System.Windows;
using System.Windows.Controls;
using System.Net;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for LaunchScreen.xaml
    /// </summary>
    public partial class LaunchScreen : Window
    {
        private bool isIPAddressValid = true;
        private bool isPlayerNameValid = true;
        private bool isPortValid = true;


        public LaunchScreen()
        {
            InitializeComponent();
            this.Title = "Monopoly Deal Setup";

            // Set default port value.
            this.PortTextBox.Text = "14242";

            this.OKButton.Focus();
        }

        private void OKButton_Click( object sender, RoutedEventArgs e )
        {
            // Retrieve the port number.
            int portNumber = Convert.ToInt32(PortTextBox.Text);

            RoomWindow roomWindow = new RoomWindow(IPAddressTextBox.Text, portNumber, PlayerNameTextBox.Text);
            if ( roomWindow.ShowWindow )
            {
                roomWindow.Show();
            }
            this.Close();
        }

        // The OKButton is enabled only when text exists in the IPAddress textbox.
        private void IPAddress_TextChanged( object sender, TextChangedEventArgs e )
        {
            IPAddress filler;
            isIPAddressValid = (IPAddressTextBox.Text.ToLower() == "localhost") || (IPAddress.TryParse(IPAddressTextBox.Text, out filler));
            ValidateTextFields();            
        }

        private void PlayerName_TextChanged( object sender, TextChangedEventArgs e )
        {
            isPlayerNameValid = !String.IsNullOrWhiteSpace(PlayerNameTextBox.Text);
            ValidateTextFields();
        }

        private void ValidateTextFields()
        {
            // The outer try block is necessary because this event is initially fired before the OKButton exists.
            try
            {
                if ( isIPAddressValid && isPlayerNameValid && isPortValid )
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

        private void Port_TextChanged( object sender, TextChangedEventArgs e )
        {
            isPortValid = true;

            try
            {
                Convert.ToInt32(PortTextBox.Text);
            }
            catch (Exception ex)
            {
                isPortValid = false;
            }

            ValidateTextFields();
        }
    }
}
