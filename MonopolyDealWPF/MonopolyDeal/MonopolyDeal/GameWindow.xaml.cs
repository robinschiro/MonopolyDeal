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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MonopolyDeal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window
    {
        public Deck Deck;
        private List<Player> Players;

        public GameWindow( int numberOfPlayers = 2 )
        {
            InitializeComponent();

            // Initialize the deck.
            this.Deck = new Deck();

            // Initialize the player collection.
            this.Players = new List<Player>();
            for (int i = 0; i < numberOfPlayers; ++i)
            {
                Players.Add(new Player(Deck, "Player " + i));
            }

            for (int i = 0; i < Players[0].CardsInHand.Count; ++i)
            {
                PlayerHand.Children.Add(Players[0].CardsInHand[i].CardImage);
                Grid.SetColumn(Players[0].CardsInHand[i].CardImage, i);
            }
        }

        // This event is not used in the application; it was created a test a component of XAML.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //int deltaX = 0, deltaY = 0;

            //if (e.Key == Key.Left)
            //{
            //    deltaX = -5;
            //}
            //if (e.Key == Key.Right)
            //{
            //    deltaX = 5;
            //}
            //if (e.Key == Key.Up)
            //{
            //    deltaY = -5;
            //}
            //if (e.Key == Key.Down)
            //{
            //    deltaY = 5;
            //}

            //foreach (FrameworkElement element in this.GameCanvas.Children)
            //{
            //    if ( element.IsFocused )
            //    {
            //        double left = (double)element.GetValue(Canvas.LeftProperty);
            //        element.SetValue(Canvas.LeftProperty, left + deltaX);

            //        double top = (double)element.GetValue(Canvas.TopProperty);
            //        element.SetValue(Canvas.TopProperty, top + deltaY);
            //    }
            //}
            //e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
