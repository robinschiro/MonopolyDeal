using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using GameClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameClient.Tests
{
    [TestClass]
    public class EventLogTests
    {
        [TestMethod]        
        public void DisplayEvent_GivenStringWithValidCardReferences_CreatesValidEventLogItemAndAddsToEventLogList()
        {
            EventLog eventLog = new EventLog();

            string serializedEvent = "Player A traded [1] for property [23] from Player B, and [0] too!";

            eventLog.DisplayEvent(serializedEvent);
            EventLogItem result = eventLog.EventList.FirstOrDefault();

            Assert.IsNotNull(result);

            TextBlock eventLogItemTextBlock = result.Content as TextBlock;
            InlineCollection inlines = eventLogItemTextBlock.Inlines;

            Assert.AreEqual(7, inlines.Count);
            Assert.IsTrue(inlines.ElementAt(0).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(1).GetType() == typeof(TextBlock));
            Assert.IsTrue(inlines.ElementAt(2).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(3).GetType() == typeof(TextBlock));
            Assert.IsTrue(inlines.ElementAt(4).GetType() == typeof(Run));
            Assert.IsTrue(inlines.ElementAt(5).GetType() == typeof(TextBlock));
            Assert.IsTrue(inlines.ElementAt(6).GetType() == typeof(Run));
        }
    }
}
