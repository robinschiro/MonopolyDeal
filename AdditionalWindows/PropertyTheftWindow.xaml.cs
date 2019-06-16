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
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using GameObjects;
using Utilities;


namespace AdditionalWindows
{
    /// <summary>
    /// Interaction logic for RentWindow.xaml
    /// </summary>
    public partial class PropertyTheftWindow : ModalWindow, INotifyPropertyChanged
    {
        private bool showMonopoliesOnly = false;
        private TheftType type;
        private PropertyHierarchyView propertyViewVictim;
        private PropertyHierarchyView propertyViewThief;

        private Player victim;
        public Player Victim
        {
            get
            {
                return victim;
            }
        }

        private Player thief;
        public Player Thief
        {
            get
            {
                return thief;
            }
        }

        private Card propertyToGive;
        public Card PropertyToGive
        {
            get
            {
                return propertyToGive;
            }
        }

        private List<Card> propertiesToTake;
        public List<Card> PropertiesToTake
        {
            get
            {
                return propertiesToTake;
            }
        }

        public ObservableCollection<Card> AssetsOfVictim { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public PropertyTheftWindow( Player victim, Player thief, int actionId )
        {
            base.isModal = false;
            AssetsOfVictim = new ObservableCollection<Card>();
            this.victim = victim;
            this.thief = thief;
            this.type = (TheftType)actionId;
            this.showMonopoliesOnly = (TheftType.Dealbreaker == this.type);

            InitializeComponent();

            // Update the label for the victim's assets.
            this.VictimAssetsLabel.Content = victim.Name + "'s Assets";

            // Create a property hierarchy for the victim's properties. This view will be used regardless of the type of theft.
            propertyViewVictim = new PropertyHierarchyView(victim, showMonopoliesOnly);
            Grid.SetRow(propertyViewVictim, 1);
            Grid.SetColumn(propertyViewVictim, 0);
            WindowGrid.Children.Add(propertyViewVictim);
            propertyViewVictim.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(SelectedItemChanged);

            // If this is a ForcedDeal, create a tree view of the thief's properties.
            if (TheftType.ForcedDeal == type)
            {
                propertyViewThief = new PropertyHierarchyView(thief, false, true);
                Grid.SetRow(propertyViewThief, 1);
                Grid.SetColumn(propertyViewThief, 1);
                WindowGrid.Children.Add(propertyViewThief);
                propertyViewThief.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(SelectedItemChanged);
            }
            // Otherwise, display only the victim's properties.
            else
            {
                // Center the victim's properties in the window.
                Grid.SetColumnSpan(propertyViewVictim, 2);
                Grid.SetColumnSpan(VictimAssetsLabel, 2);

                // Hide the thief assets label.
                ThiefAssetsLabel.Visibility = System.Windows.Visibility.Hidden;
            }


            // Update the window title to reflect the player receiving rent.
            this.Title = "Stealing Property from " + victim.Name;

            // Set the data context of the window.
            this.DataContext = this;
        }

        void SelectedItemChanged( object sender, RoutedPropertyChangedEventArgs<object> e )
        {
            bool enableSteal = true;

            if ( null != propertyViewThief )
            {
                enableSteal &= (null != propertyViewThief.SelectedItem) && ((propertyViewThief.SelectedItem as TreeViewItem).Tag is Card);
            }

            if ( enableSteal )
            {
                enableSteal &= (null != propertyViewVictim.SelectedItem);
            }

            if ( enableSteal )
            {
                if ( TheftType.Dealbreaker == this.type )
                {
                    enableSteal &= (propertyViewVictim.SelectedItem as TreeViewItem).Tag is List<Card>;
                }
                else
                {
                    enableSteal &= (propertyViewVictim.SelectedItem as TreeViewItem).Tag is Card;
                }
            }

            StealButton.IsEnabled = enableSteal;
        }

        // Create the OnPropertyChanged method to raise the event.
        protected void OnPropertyChanged( string name )
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if ( handler != null )
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        // Resize the columns of a ListView to fit the contents.
        // Taken from: http://stackoverflow.com/questions/845269/force-resize-of-gridview-columns-inside-listview
        public void AutoSizeColumns( ListView listview )
        {
            GridView gv = listview.View as GridView;
            if ( gv != null )
            {
                foreach ( var c in gv.Columns )
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the same code that is executed when the gripper is double clicked
                    if ( double.IsNaN(c.Width) )
                    {
                        c.Width = c.ActualWidth;
                    }
                    c.Width = double.NaN;
                }
            }
        }

        private void StealButton_Click( object sender, RoutedEventArgs e )
        {
            // Set the PropertyToGive and PropertiesToTake based on the selection of the thief.
            if ( null != propertyViewThief )
            {
                this.propertyToGive = (propertyViewThief.SelectedItem as TreeViewItem).Tag as Card;
            }
            else
            {
                this.propertyToGive = new Card();
            }

            Object selectedItemTag = (propertyViewVictim.SelectedItem as TreeViewItem).Tag;
            this.propertiesToTake = (TheftType.Dealbreaker == this.type) ? (selectedItemTag as List<Card>) : new List<Card>() { (selectedItemTag as Card) };

            this.dialogResult = true;
            this.CloseWindow = true;
        }

        // A Treeview class used to display the properties of a player.
        private class PropertyHierarchyView : TreeView
        {
            private Player player;
            private bool showMonopoliesOnly;

            public PropertyHierarchyView( Player player, bool onlyMonopoliesSelectable, bool isThiefAssets = false )
            {
                // Instantiate member variables.
                this.player = player;
                this.showMonopoliesOnly = onlyMonopoliesSelectable;

                List<List<Card>> cardGroups;

                if ( isThiefAssets )
                {
                    cardGroups = new List<List<Card>>(player.CardsInPlay.Skip(1));
                }
                else
                {
                    cardGroups = onlyMonopoliesSelectable ? (ClientUtilities.FindMonopolies(player)) : new List<List<Card>>(player.CardsInPlay.Skip(1).Where(cardList => !ClientUtilities.IsCardListMonopoly(cardList)));
                }

                // Populate the tree with cards from the player's cards in play.
                foreach ( List<Card> cardList in cardGroups)
                {
                    // Each parent item will represent a potential monopoly,
                    TreeViewItem potentialMonopoly = new TreeViewItem();
                    potentialMonopoly.Tag = cardList;
                    potentialMonopoly.Header = ClientUtilities.GetCardListColor(cardList).ToString() + " Group";
                    this.Items.Add(potentialMonopoly);

                    // Display the properties from the group under the group label.
                    foreach ( Card property in cardList )
                    {
                        TreeViewItem propertyItem = new TreeViewItem();
                        propertyItem.Header = property.Name;
                        propertyItem.Tag = property;
                        propertyItem.IsEnabled = !onlyMonopoliesSelectable;
                        propertyItem.ToolTip = new Image() { Source = new BitmapImage(new Uri(property.CardImageUriPath, UriKind.Absolute)) };
                        potentialMonopoly.Items.Add(propertyItem);                        
                    }

                    this.ExpandSubtree(potentialMonopoly);
                }
            }
        }
    }
}
