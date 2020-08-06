using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Controls
{
    /// <summary>
    /// Interaction logic for CardCount.xaml
    /// </summary>
    public partial class CardCountDisplay : UserControl, INotifyPropertyChanged
    {
        public int CardCount
        {
            set
            {
                if ( 0 == value )
                {
                    this.DisplayText = string.Empty;
                }
                else
                {
                    this.DisplayText = $"x{value}";
                }
                OnPropertyChanged("DisplayText");
            }
        }

        public string DisplayText { get; private set; }

        private DrawingImage displayImage;
        public DrawingImage DisplayImage
        {
            get
            {
                return displayImage;
            }
            set
            {
                displayImage = value;
                OnPropertyChanged("DisplayImage");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CardCountDisplay()
        {
            InitializeComponent();
            this.CardCountGrid.DataContext = this;
        }

        private void OnPropertyChanged( string info )
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }
}
