﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using GameObjects;

namespace AdditionalWindows
{
    /// <summary>
    /// Interaction logic for RentWindow.xaml
    /// </summary>
    public partial class RentWindow : ModalWindow, INotifyPropertyChanged
    {
        private int amountOwed;
        private const string AssetValueLabelPrefix = "Total: ";

        public ObservableCollection<Card> Payment { get; set; }
        public ObservableCollection<Card> Assets { get; set; }

        private int amountGiven;
        public string AmountGivenString
        {
            get
            {
                amountLeft = Card.SumOfCardValues(AssetsListView.Items);
                amountGiven = Card.SumOfCardValues(PaymentListView.Items);

                // Update the Pay Rent button.
                PayButton.IsEnabled = (amountGiven >= amountOwed) || (0 == amountLeft);

                return AssetValueLabelPrefix + amountGiven;
            }
        }

        private int amountLeft;
        public string AmountLeftString
        {
            get
            {
                amountLeft = Card.SumOfCardValues(AssetsListView.Items);

                return AssetValueLabelPrefix + amountLeft;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RentWindow( Window owner, Player rentee, string renterName, int rentAmount ) : base(owner)
        {
            Payment = new ObservableCollection<Card>();
            Assets = new ObservableCollection<Card>();

            InitializeComponent();

            // Update the window to display the amount owed.
            TextBlock amountOwedTextblock = new TextBlock();
            this.amountOwed = rentAmount;
            amountOwedTextblock.Inlines.Add(new Bold(new Run(AmountOwedLabel.Content.ToString() + this.amountOwed)));
            AmountOwedLabel.Content = amountOwedTextblock;

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

            this.JustSayNoButton.IsEnabled = rentee.CardsInHand.Any(card => ActionId.JustSayNo == (ActionId)card.ActionID);          
        }

        // Create the OnPropertyChanged method to raise the event.
        protected void OnPropertyChanged( string name )
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void GiveSelectedButton_Click( object sender, RoutedEventArgs e )
        {
            TransferSelectedItems(AssetsListView, PaymentListView);
        }

        private void RemoveSelectedButton_Click( object sender, RoutedEventArgs e )
        {
            TransferSelectedItems(PaymentListView, AssetsListView);
        }

        private void GiveAllButton_Click( object sender, RoutedEventArgs e )
        {
            AssetsListView.SelectAll();
            TransferSelectedItems(AssetsListView, PaymentListView);
        }

        private void RemoveAllButton_Click( object sender, RoutedEventArgs e )
        {
            PaymentListView.SelectAll();
            TransferSelectedItems(PaymentListView, AssetsListView);
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
                        
            OnPropertyChanged("AmountLeftString");
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

        // Close the rent window when the user presses the Pay button.
        private void PayButton_Click( object sender, RoutedEventArgs e )
        {
            this.dialogResult = true;
            this.CloseWindow = true;
        }

        private void AssetsListView_MouseDoubleClick( object sender, MouseButtonEventArgs e )
        {
            TransferSelectedItems(AssetsListView, PaymentListView);
        }

        private void PaymentListView_MouseDoubleClick( object sender, MouseButtonEventArgs e )
        {
            TransferSelectedItems(PaymentListView, AssetsListView);
        }

        private void JustSayNoButton_Click( object sender, RoutedEventArgs e )
        {
            var messageDialog = new MessageDialog(this, "Confirmation", "Are you sure you want to use your \"Just Say No!\" card?", MessageBoxButton.OKCancel);
            messageDialog.ShowDialog();            
            if ( MessageBoxResult.OK == messageDialog.Result )
            {
                this.dialogResult = false;
                this.CloseWindow = true;
            }
        }
    }
}
