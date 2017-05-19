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
    public partial class MessageDialog : ModalWindow
    {
        public MessageBoxResult Result { get; set; }

        public MessageDialog( string title, string message, MessageBoxButton option = (MessageBoxButton)(-1), bool isModal = true) : base( isModal )
        {
            InitializeComponent();
            this.Title = title;
            this.MessageLabel.Content = message;

            // Set the window's width to be large enough to contain the message.
            this.Loaded += new RoutedEventHandler(( sender, args ) => { this.Width = this.MessageLabel.ActualWidth + 100; });            

            if ( MessageBoxButton.YesNo == option )
            {
                this.YesNoGrid.Visibility = Visibility.Visible;
            }
            else if ( MessageBoxButton.OK == option )
            {
                this.OkButton.Visibility = Visibility.Visible;
            }
        }

        private void YesButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.Yes;
            CloseWindow = true;
        }

        private void NoButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.No;
            CloseWindow = true;
        }

        private void OkButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.OK;
            CloseWindow = true;
        }
    }


}
