using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using GameClient;
using GameObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameClient.Tests
{
    [TestClass]
    public class EventLogTests
    {
        [TestMethod]
        public void DisplayEvent_AnyString_AddsToEventLogList()
        {
            EventLog eventLog = new EventLog(new Dictionary<int, Card>());

            eventLog.DisplayEvent(string.Empty);
            EventLogItem result = eventLog.EventList.FirstOrDefault();

            Assert.IsNotNull(result);
        }

        [TestMethod]        
        public void DisplayEvent_GivenStringWithMixOfTagsAndNonTags_CreatesValidEventLogItem()
        {
            EventLog eventLog = new EventLog(new Dictionary<int, Card>()
            {
                {1, new Card() { Name = "TestCard1" } },
                {23, new Card() { Name = "TestCard23" } },
                {0, new Card() { Name = "TestCard0" } }
            });

            string serializedEvent = "Player A traded [1] for property [23] from Player B, and [0] too!";

            eventLog.DisplayEvent(serializedEvent);
            EventLogItem result = eventLog.EventList.FirstOrDefault();

            InlineCollection inlines = (result.Content as TextBlock).Inlines;

            Assert.AreEqual(7, inlines.Count);
            Assert.IsTrue(inlines.ElementAt(0).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(1).GetType() == typeof(InlineUIContainer));
            Assert.IsTrue(inlines.ElementAt(2).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(3).GetType() == typeof(InlineUIContainer));
            Assert.IsTrue(inlines.ElementAt(4).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(5).GetType() == typeof(InlineUIContainer));
            Assert.IsTrue(inlines.ElementAt(6).GetType() == typeof(Run));
        }

        [TestMethod]
        public void DisplayEvent_GivenStringWithOnlyTags_CreatesValidEventLogItem()
        {
            EventLog eventLog = new EventLog(new Dictionary<int, Card>()
            {
                {1, new Card() { Name = "TestCard1" } },
                {23, new Card() { Name = "TestCard23" } },
                {0, new Card() { Name = "TestCard0" } }
            });

            string serializedEvent = "[1][23][0]";

            eventLog.DisplayEvent(serializedEvent);
            EventLogItem result = eventLog.EventList.FirstOrDefault();

            InlineCollection inlines = (result.Content as TextBlock).Inlines;

            Assert.AreEqual(3, inlines.Count);
            Assert.IsTrue(inlines.ElementAt(0).GetType() == typeof(InlineUIContainer));
            Assert.IsTrue(inlines.ElementAt(1).GetType() == typeof(InlineUIContainer));
            Assert.IsTrue(inlines.ElementAt(2).GetType() == typeof(InlineUIContainer));
        }

        [TestMethod]
        public void DisplayEvent_GivenStringWithNoTags_CreatesValidEventLogItem()
        {
            EventLog eventLog = new EventLog(new Dictionary<int, Card>());

            string serializedEvent = "Event with no tags";

            eventLog.DisplayEvent(serializedEvent);
            EventLogItem result = eventLog.EventList.FirstOrDefault();

            InlineCollection inlines = (result.Content as TextBlock).Inlines;

            Assert.AreEqual(1, inlines.Count);
            Assert.IsTrue(inlines.ElementAt(0).GetType() == typeof(Run));
        }
    }
}
