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
using GameObjects;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for EventLog.xaml
    /// </summary>
    public partial class EventLog : UserControl, IEventLog
    {
        private int eventNum = 0;

        public EventLog()
        {
            InitializeComponent();
        }

        public void PublishPlayCardEvent( Player player, Card card )
        {
            throw new NotImplementedException();
        }

        public void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid )
        {
            throw new NotImplementedException();
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
            for ( int i = 0; i < 10; i++ )
            {
                this.EventLogTextBlock.Text += "\r\n" + eventLogline + " " + this.eventNum++;
            }

            this.EventLogScrollViewer.ScrollToBottom();
        }
    }
}
