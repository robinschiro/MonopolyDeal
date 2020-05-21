using System.Windows;
using System.Windows.Input;

namespace AdditionalWindows
{
    // <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : ModalWindow
    {
        public MessageBoxResult Result { get; set; }

        public MessageDialog( Window owner, string title, string message, MessageBoxButton option = (MessageBoxButton)(-1), bool isModal = true ) : base(owner, isModal)
        {
            InitializeComponent();
            this.Title = title;
            this.MessageLabel.Text = message;

            if ( owner.IsVisible )
            {
                this.Owner = owner;
            }    

            switch (option)
            {
                case MessageBoxButton.YesNo:
                {
                    this.YesNoGrid.Visibility = Visibility.Visible;
                    break;
                }

                case MessageBoxButton.OKCancel:
                {
                    this.OkCancelGrid.Visibility = Visibility.Visible;
                    break;
                }

                case MessageBoxButton.OK:
                {
                    this.OkButton.Visibility = Visibility.Visible;
                    break;
                }
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

        private void CancelButton_Click( object sender, RoutedEventArgs e )
        {
            this.Result = MessageBoxResult.Cancel;
            CloseWindow = true;
        }

        private void ModalWindow_KeyDown( object sender, KeyEventArgs e )
        {
            if ( Key.Enter == e.Key && Visibility.Visible == this.OkButton.Visibility )
            {
                this.Result = MessageBoxResult.OK;
                CloseWindow = true;
            }
        }
    }


}
