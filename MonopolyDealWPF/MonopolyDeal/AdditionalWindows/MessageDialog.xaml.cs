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
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class WaitDialog : Window
    {
        public bool CloseWindow = false;

        public WaitDialog( string message )
        {
            InitializeComponent();

            this.MessageLabel.Content = message;
        }

        // Only allow the player to close the rent window when the 'Pay Rent' button is pressed.
        protected override void OnClosing( CancelEventArgs e )
        {
            if ( !CloseWindow )
            {
                e.Cancel = true;
            }
        }
    }


}
