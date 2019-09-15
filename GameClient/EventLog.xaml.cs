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
using GameServer;
using Lidgren.Network;
using Utilities;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for EventLog.xaml
    /// </summary>
    public partial class EventLog : UserControl, IEventLog
    {
        public ObservableCollection<EventLogItem> EventList { get; set; }

        private NetClient netClient;

        public EventLog()
        {
            InitializeComponent();
            this.EventList = new ObservableCollection<EventLogItem>();
            this.DataContext = this;
        }

        public EventLog(NetClient netClient) : this()
        {
            this.netClient = netClient;
        }

        public void PublishPlayCardEvent( Player player, Card card )
        {
            string eventLine = $"{player.Name} played {card.Name}";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishJustSayNoEvent( Player playerSayingNo )
        {
            string eventLine = $"{playerSayingNo.Name} rejected with a Just Say No!";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid )
        {
            string eventLine = $"{rentee.Name} paid {renter.Name } ${assetsPaid.Sum(card => card.Value)}M with the following assets: {string.Join(", ", assetsPaid.Select(card => card.Name))}";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishSlyDealEvent( Player thief, Player victim, Card property )
        {
            string eventLine = $"{thief.Name} would like to steal {property.Name} from {victim.Name}";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty )
        {
            string eventLine = $"{thief.Name} would like to trade {thiefProperty.Name} for {victimProperty.Name} from {victim.Name}";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishDealbreakerEvent( Player thief, Player victim, List<Card> monopoly )
        {
            PropertyType monopolyColor = ClientUtilities.GetCardListColor(monopoly);
            string eventLine = $"{thief.Name} would like to steal the {monopolyColor.ToString()} monopoly from {victim.Name}";

            this.PublishCustomEvent(eventLine);
        }

        public void PublishPlayerWonEvent( Player winner )
        {
            throw new NotImplementedException();
        }

        public void PublishCustomEvent( string eventLogline )
        {
            ServerUtilities.SendMessage(this.netClient, Datatype.GameEvent, eventLogline);
        }

        public void DisplayEvent( string serializedEvent )
        {
            this.EventList.Add(new EventLogItem { Content = serializedEvent });
            this.EventLogScrollViewer.ScrollToBottom();
        }
    }
}
