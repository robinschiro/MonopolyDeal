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

        //Testing something called AttachedProperties. Allows function calls upon triggers. 
        //Figured it would be good for the infobox, as it is constantly being updated due to triggers.
        private DependencyProperty InfoBoxAttachedProperty = DependencyProperty.RegisterAttached("Contents", typeof(Card), typeof(GameWindow));

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
                DisplayCardInHand(Players[0].CardsInHand, i);
            }
        }

        // Display a card in a player's hand.
        // Right now, this method only works for player one's hand.
        public void DisplayCardInHand(List<Card> cardsInHand, int position)
        {
            Button cardButton = new Button();
            cardButton.Content = cardsInHand[position].CardImage;
            cardButton.Style = (Style)FindResource("NoChromeButton");

            ColumnDefinition col1 = new ColumnDefinition();
            col1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition col2 = new ColumnDefinition();
            col2.Width = new GridLength(16, GridUnitType.Star);
            ColumnDefinition col3 = new ColumnDefinition();
            col3.Width = new GridLength(1, GridUnitType.Star);

            Grid cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(col1);
            cardGrid.ColumnDefinitions.Add(col2);
            cardGrid.ColumnDefinitions.Add(col3);
            cardGrid.Children.Add(cardButton);
            Grid.SetColumn(cardButton, 1);

            PlayerOneHand.Children.Add(cardGrid);
            Grid.SetColumn(cardGrid, position);
        }

        // Again, testing AttachedProperties. Will need to discuss.
        public void setInfoBox(TriggerBase target, int location)
        {
            target.SetValue(InfoBoxAttachedProperty, Players[0].CardsInHand[location]);
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
