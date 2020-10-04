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
            { PropertyType.Blue, new CardDisplayColors() { Foreground = Brushes.White, Background = Brushes.Blue } },
            { PropertyType.Brown, new CardDisplayColors() { Foreground = Brushes.White, Background = Brushes.SaddleBrown } },
            { PropertyType.Green, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.LimeGreen } },
            { PropertyType.LightBlue, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.LightBlue } },
            { PropertyType.None, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.White} },
            { PropertyType.Orange, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.Orange } },
            { PropertyType.Pink, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.Magenta} },
            { PropertyType.Railroad, new CardDisplayColors() { Foreground = Brushes.White, Background = Brushes.Black } },
            { PropertyType.Red, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.Red } },
            { PropertyType.Utility, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.PaleGoldenrod} },
            { PropertyType.Wild, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.White} },
            { PropertyType.Yellow, new CardDisplayColors() { Foreground = Brushes.Black, Background = Brushes.Yellow} }
        };

        private NetClient netClient;
        private Dictionary<int, Card> allCards;

        public ObservableCollection<EventLogItem> EventList { get; set; }

        public EventLog(Dictionary<int, Card> allCards )
        {
            InitializeComponent();
            this.DataContext = this;
            this.EventList = new ObservableCollection<EventLogItem>();
            this.allCards = allCards;
        }

        public EventLog(NetClient netClient, Dictionary<int, Card> allCards) : this(allCards)
        {
            this.netClient = netClient;
        }

        public void PublishPlayCardEvent( Player player, Card card )
        {
            string eventLine = $"{player.Name} played [{card.CardID}]";

            this.PublishEvent(eventLine);
        }

        public void PublishPlayTargetedCardEvent( Player player, Player target, Card card )
        {
            string eventLine = $"{player.Name} played [{card.CardID}] against {target.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishDiscardEvent( Player player, Card card )
        {
            string eventLine = $"{player.Name} discarded [{card.CardID}]";

            this.PublishEvent(eventLine);
        }

        public void PublishJustSayNoEvent( Player playerSayingNo )
        {
            string eventLine = $"{playerSayingNo.Name} rejected with a Just Say No!";

            this.PublishEvent(eventLine);
        }

        public void PublishPayRentEvent( Player renter, Player rentee, List<Card> assetsPaid )
        {
            int totalRent = assetsPaid.Sum(card => card.Value);
            string eventLine = string.Empty;
            if ( totalRent > 0 )
            {
                eventLine = $"{rentee.Name} paid {renter.Name } ${totalRent}M with the following assets: {string.Join(", ", assetsPaid.Select(card => $"[{card.CardID}]"))}";
            }
            else
            {
                eventLine = $"{rentee.Name} does not have any assets to pay {renter.Name} with";
            }

            this.PublishEvent(eventLine);
        }

        public void PublishSlyDealEvent( Player thief, Player victim, Card property )
        {
            string eventLine = $"{thief.Name} would like to steal [{property.CardID}] from {victim.Name}";

            this.PublishEvent(eventLine);
        }

        public void PublishForcedDealEvent( Player thief, Player victim, Card thiefProperty, Card victimProperty )
        {
            string eventLine = $"{thief.Name} would like to trade [{thiefProperty.CardID}] for [{victimProperty.CardID}] from {victim.Name}";

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
            string eventLine = $"{winner.Name} has won the game!";

            this.PublishEvent(eventLine);
        }

        public void PublishDeckEmptyEvent(bool discardPileReshuffled)
        {
            string eventLine = "The deck has run out of cards! " + (discardPileReshuffled 
                ? "All cards in the discard pile will be shuffled and used as the deck." 
                : "No cards will be reshuffled because the discard pile is empty.");

            this.PublishEvent(eventLine);
        }

        private void PublishEvent( string eventLogline )
        {
            ServerUtilities.SendMessage(this.netClient, Datatype.GameEvent, eventLogline);
        }

        private EventLogItem CreateEventLogItemFromSerializedEvent(string serializedEvent)
        {
            var eventTextBlock = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontFamily = new FontFamily("Courier New"),
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            StringBuilder currentPiece = new StringBuilder();
            foreach ( char token in serializedEvent )
            {
                switch( token )
                {
                    case '[':
                    {
                        if ( currentPiece.Length > 0 )
                        {
                            eventTextBlock.Inlines.Add(new Run(currentPiece.ToString()));
                            currentPiece.Clear();
                        }
                        break;
                    }
                    case ']':
                    {
                        int cardId;
                        if ( currentPiece.Length > 0 && 
                             int.TryParse(currentPiece.ToString(), out cardId) && 
                             this.allCards.ContainsKey(cardId))
                        {
                            Card card = this.allCards[cardId];
                            DrawingImage cardImageSource = this.TryFindResource(card.CardImageUriPath) as DrawingImage;
                            var cardGraphic = new TextBlock()
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Background = this.propertyTypeToDisplayColors[card.Color].Background,
                                Foreground = this.propertyTypeToDisplayColors[card.Color].Foreground,
                                ToolTip = new Image()
                                {
                                    Source = cardImageSource,
                                    MaxWidth = Convert.ToInt32(GameObjectsResourceList.TooltipMaxWidth)
                                }
                            };
                            cardGraphic.Inlines.Add(new Underline(new Bold(new Run(card.Name))));

                            eventTextBlock.Inlines.Add(cardGraphic);
                            currentPiece.Clear();
                        }
                        break;
                    }
                    default:
                    {
                        currentPiece.Append(token);
                        break;
                    }
                }
            }

            if ( currentPiece.Length > 0 )
            {
                eventTextBlock.Inlines.Add(new Run(currentPiece.ToString()));
            }

            if (eventTextBlock.Inlines.Count > 0)
            {
                string timestamp = $"[{DateTime.Now:h:mm:ss tt}] ";
                eventTextBlock.Inlines.InsertBefore(eventTextBlock.Inlines.ElementAt(0), new Run(timestamp));
            }

            return new EventLogItem() { Content = eventTextBlock };
        }

        public void DisplayEvent( string serializedEvent )
        {
            if (this.EventList.Count >= Convert.ToInt32(GameObjectsResourceList.EventLogItemLimit))
            {
                this.EventList.RemoveAt(0);
            }

            EventLogItem logItem = this.CreateEventLogItemFromSerializedEvent(serializedEvent);
            this.EventList.Add(logItem);


            this.EventLogScrollViewer.ScrollToBottom();
        }
    }
}
