using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using GameObjects;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for EventLog.xaml
    /// </summary>
    public partial class EventLog : UserControl, IEventLog
    {
        public ObservableCollection<string> EventList { get; set; }

        public EventLog()
        {
            InitializeComponent();
            this.EventList = new ObservableCollection<string>();
            this.DataContext = this;
        }

        public void PublishPlayCardEvent( Player player, Card card )
        {
            throw new NotImplementedException();
        }

        public void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid )
        {
            string eventLine = rentee.Name + " paid " + renter.Name + " the following assets: " + string.Join(", ", assetsPaid.Select(card => card.Name));

            this.PublishCustomEvent(eventLine);
        }

        public void PublishSlyDealEvent( Player thief, Player victim, Card property )
        {
            throw new NotImplementedException();
        }

        public void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty )
        {
            throw new NotImplementedException();
        }

        public void PublishDealbreakerEvent( Player thief, Player victim, List<Card> monopoly )
        {
            throw new NotImplementedException();
        }

        public void PublishPlayerWonEvent( Player winner )
        {
            throw new NotImplementedException();
        }

        public void PublishCustomEvent( string eventLogline )
        {
            //this.EventLogTextBlock.Text += "\r\n" + eventLogline + " ";
            this.EventList.Add(eventLogline);

            this.EventLogScrollViewer.ScrollToBottom();
        }
    }
}
