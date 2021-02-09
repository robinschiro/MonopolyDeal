using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using GameObjectsResourceList = GameObjects.Properties.Resources;

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
                this.DisplayText = (0 == value) ? string.Empty : $"x{value}";
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
            this.CardCountImageTooltip.MaxWidth = Convert.ToInt32(GameObjectsResourceList.TooltipMaxWidth);
        }

        private void OnPropertyChanged( string info )
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }
}
