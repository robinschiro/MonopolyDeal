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
        public const int MAX_NUMBER_OF_PLAYERS = 4;

        public LaunchScreen()
        {
            InitializeComponent();
            this.Title = "Monopoly Deal Setup";
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            GameWindow gameWindow = new GameWindow(Convert.ToInt16(NumberOfPlayers.Text));
            gameWindow.Show();
            Close();
        }

        // The OKButton is enabled only when a value between 2 - 4 is placed in the NumberOfPlayers textbox.
        private void NumberOfPlayers_TextChanged(object sender, TextChangedEventArgs e)
        {
            // The outer try block is necessary because this event is initially fired before the OKButton exists.
            try
            {
                try
                {
                    int numberOfPlayers = Convert.ToInt16(NumberOfPlayers.Text);
                    if (numberOfPlayers <= MAX_NUMBER_OF_PLAYERS && numberOfPlayers > 1)
                    {
                        OKButton.IsEnabled = true;
                    }
                }
                catch
                {
                    OKButton.IsEnabled = false;
                }
            }
            catch { }
        }
    }
}
