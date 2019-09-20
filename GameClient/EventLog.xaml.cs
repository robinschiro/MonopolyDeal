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
using GameObjectsResourceList = GameObjects.Properties.Resources;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for EventLog.xaml
    /// </summary>
    public partial class EventLog : UserControl, IEventLog
    {
        struct CardDisplayColors
        {
            public Brush Foreground { get; set; }
            public Brush Background { get; set; }
        }

        private IDictionary<PropertyType, CardDisplayColors> propertyTypeToDisplayColors = new Dictionary<PropertyType, CardDisplayColors>()
        {
            { PropertyType.Blue, new CardDisplayColors() { Foreground = Brushes.White, Background = Brushes.Blue } }
        };

        private NetClient netClient;
        //private Deck deck = new Deck();

        public ObservableCollection<EventLogItem> EventList { get; set; }

        public EventLog()
        {
            InitializeComponent();
            this.DataContext = this;
            this.EventList = new ObservableCollection<EventLogItem>();
            //this.deck = Card.ge
        }

        public EventLog(NetClient netClient) : this()
        {
            this.netClient = netClient;
        }

        public void PublishPlayCardEvent( Player player, Card card )
        {
            string eventLine = $"{player.Name} played {card.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishJustSayNoEvent( Player playerSayingNo )
        {
            string eventLine = $"{playerSayingNo.Name} rejected with a Just Say No!";

            this.PublishEvent(eventLine);
        }

        public void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid )
        {
            string eventLine = $"{rentee.Name} paid {renter.Name } ${assetsPaid.Sum(card => card.Value)}M with the following assets: {string.Join(", ", assetsPaid.Select(card => card.Name))}";

            this.PublishEvent(eventLine);
        }

        public void PublishSlyDealEvent( Player thief, Player victim, Card property )
        {
            string eventLine = $"{thief.Name} would like to steal {property.Name} from {victim.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty )
        {
            string eventLine = $"{thief.Name} would like to trade {thiefProperty.Name} for {victimProperty.Name} from {victim.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishDealbreakerEvent( Player thief, Player victim, List<Card> monopoly )
        {
            PropertyType monopolyColor = ClientUtilities.GetCardListColor(monopoly);
            string eventLine = $"{thief.Name} would like to steal the {monopolyColor.ToString()} monopoly from {victim.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishNewTurnEvent( Player player )
        {
            string eventLine = $"It is {player.Name}'s turn!";

            this.PublishEvent(eventLine);
        }

        public void PublishPlayerWonEvent( Player winner )
        {
            throw new NotImplementedException();
        }

        private void PublishEvent( string eventLogline )
        {
            ServerUtilities.SendMessage(this.netClient, Datatype.GameEvent, eventLogline);
        }

        private EventLogItem CreateEventLogItemFromSerializedEvent(string serializedEvent)
        {
            return new EventLogItem();
        }

        public void DisplayEvent( string serializedEvent )
        {
            DrawingImage cardImageSource = this.TryFindResource("cardbackDrawingImage") as DrawingImage;
            var testButton = new TextBlock()
            {
                Text = "(Card)",
                Background = Brushes.Blue,
                Foreground = Brushes.White,
                ToolTip = new Image()
                {
                    Source = cardImageSource,
                    MaxWidth = Convert.ToInt32(GameObjectsResourceList.TooltipMaxWidth)
                }
            };

            var eventTextBlock = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 18
            };
            eventTextBlock.Inlines.Add(new Run(serializedEvent + " "));
            eventTextBlock.Inlines.Add(testButton);

            EventLogItem logItem = new EventLogItem() { Content = eventTextBlock };

            this.EventList.Add(logItem);
            this.EventLogScrollViewer.ScrollToBottom();
        }
    }
}
