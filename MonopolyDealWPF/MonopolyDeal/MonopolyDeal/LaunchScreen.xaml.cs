using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
        }

        private void OKButton_Click( object sender, RoutedEventArgs e )
        {
            GameWindow gameWindow = new GameWindow(IPAddress.Text, PlayerName.Text);
            gameWindow.Show();
            Close();
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
