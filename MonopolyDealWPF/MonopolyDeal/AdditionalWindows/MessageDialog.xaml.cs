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
using System.Threading;
using System.ComponentModel;

namespace AdditionalWindows
{
    
    // <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : Window
    {
        private bool closeWindow = false;
        public bool CloseWindow
        {
            get
            {
                return closeWindow;
            }
            set
            {
                closeWindow = value;
                if (closeWindow)
                {
                    this.Close();
                }
            }
        }
        public MessageBoxResult Result;

        public MessageDialog( string title, string message, MessageBoxButton option = (MessageBoxButton)(-1) )
        {
            InitializeComponent();

            this.Title = title;
            this.MessageLabel.Content = message;

            // Set the window's width to be large enough to contain the message.
            this.Loaded += new RoutedEventHandler(( sender, args ) => { this.Width = this.MessageLabel.ActualWidth + 50; });            

            if ( MessageBoxButton.YesNo == option )
            {
                this.YesNoGrid.Visibility = Visibility.Visible;
            }
            else if ( MessageBoxButton.OK == option )
            {
                this.OkButton.Visibility = Visibility.Visible;
            }
        }

        // Only allow the player to close the dialog when CloseWindow has been enabled.
        protected override void OnClosing( CancelEventArgs e )
        {
            if ( !CloseWindow )
            {
                e.Cancel = true;
            }
        }

        private void YesButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.Yes;
            CloseWindow = true;
            this.Close();
        }

        private void NoButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.No;
            CloseWindow = true;
            this.Close();
        }

        private void OkButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.OK;
            CloseWindow = true;
            this.Close();
        }
    }


}
