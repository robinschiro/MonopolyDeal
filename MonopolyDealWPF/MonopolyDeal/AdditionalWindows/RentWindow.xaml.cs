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


namespace AdditionalWindows
{
    /// <summary>
    /// Interaction logic for RentWindow.xaml
    /// </summary>
    public partial class RentWindow : Window, INotifyPropertyChanged
    {
        private int amountOwed;
        private bool closeWindow = false;

        public ObservableCollection<Card> Payment { get; set; }
        public ObservableCollection<Card> Assets { get; set; }

        private int amountGiven;
        public string AmountGivenString
        {
            get
            {
                amountGiven = Card.SumOfCardValues(PaymentListView.Items);

                // Update the Pay Rent button.
                PayButton.IsEnabled = (amountGiven >= amountOwed) || ( 0 == AssetsListView.Items.Count );

                return "Total Value: " + amountGiven;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RentWindow( Player rentee, string renterName, int rentAmount, bool rentDoubled )
        {
            Payment = new ObservableCollection<Card>();
            Assets = new ObservableCollection<Card>();

            InitializeComponent();

            amountOwed = rentAmount * ((rentDoubled) ? (2) : (1));

            // Update the window to display the amount owed.
            AmountOwedLabel.Content = AmountOwedLabel.Content.ToString() + rentAmount + ((rentDoubled) ? (" x 2") : (""));

            // Add all the rentee's cards in play to the Assets listview.
            foreach ( List<Card> cardList in rentee.CardsInPlay )
            {
                foreach ( Card card in cardList )
                {
                    Assets.Add(card);
                }
            }

            // Update the window title to reflect the player receiving rent.
            this.Title = "Rental Payment to " + renterName;

            // Set the data context of the window.
            this.DataContext = this;
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

        private void GiveButton_Click( object sender, RoutedEventArgs e )
        {
            TransferSelectedItems(AssetsListView, PaymentListView);
        }

        private void RemoveButton_Click( object sender, RoutedEventArgs e )
        {
            TransferSelectedItems(PaymentListView, AssetsListView);
        }

        // Only allow the player to close the rent window when the 'Pay Rent' button is pressed.
        protected override void OnClosing(CancelEventArgs e)
        {
            if ( !closeWindow )
            {
                e.Cancel = true;
            }
        }

        // Transfer all selected items from one listview to another.
        private void TransferSelectedItems(ListView listview1, ListView listview2)
        {
            List<Card> selectedCards = new List<Card>();

            // Retrieve the observable collections that are bound to the listviews.
            ObservableCollection<Card> listview1Source = (ObservableCollection<Card>)listview1.ItemsSource;
            ObservableCollection<Card> listview2Source = (ObservableCollection<Card>)listview2.ItemsSource;

            // Add the selected items to a new list. This is necessary for the following foreach loop,
            // which modifies the value of the SelectedItems collection.
            foreach ( Card card in listview1.SelectedItems )
            {
                selectedCards.Add(card);
            }

            // Transfer the selected items.
            foreach ( Card card in selectedCards )
            {
                listview2Source.Add(card);
                listview1Source.Remove(card);
            }

            // Resize the ListViews' columns.
            AutoSizeColumns(listview1);
            AutoSizeColumns(listview2);

            // Raise the property change for the amount given.
            OnPropertyChanged("AmountGivenString");
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

        private void PayButton_Click( object sender, RoutedEventArgs e )
        {
            closeWindow = true;
            this.DialogResult = true;
            this.Close();
        }
    }
}
