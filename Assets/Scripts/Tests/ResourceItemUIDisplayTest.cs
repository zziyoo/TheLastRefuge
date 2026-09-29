using UnityEngine;
using NUnit.Framework;
using TMPro;
using LastRefuge.UI;

namespace LastRefuge.Tests
{
    /// <summary>
    /// ResourceItemUI is display-only: ResourceSystem computes daysRemaining
    /// (netChange >= 0 -> -1, empty stock with netChange < 0 -> 0), UIManager
    /// passes the value through, and Setup() must render the agreed strings.
    /// </summary>
    public class ResourceItemUIDisplayTest
    {
        private ResourceItemUI item;
        private TextMeshProUGUI daysText;

        [SetUp]
        public void Setup()
        {
            item = new GameObject("ResourceItem").AddComponent<ResourceItemUI>();
            daysText = new GameObject("DaysText").AddComponent<TextMeshProUGUI>();
            item.daysRemainingText = daysText;
        }

        [TearDown]
        public void TearDown()
        {
            if (item != null) Object.Destroy(item.gameObject);
            if (daysText != null) Object.Destroy(daysText.gameObject);
        }

        [Test]
        public void Setup_RendersAgreedDaysRemainingStrings()
        {
            // Case 1: netChange >= 0 is reported as -1 -> infinite.
            item.Setup("Food", 30, 100, 2, -1);
            Assert.AreEqual("∞", daysText.text);

            // Case 2: empty stock with netChange < 0 -> less than one day.
            item.Setup("Food", 1, 100, -4, 0);
            Assert.AreEqual("不足1天", daysText.text);

            // Case 3: stock still covers some days -> X天.
            item.Setup("Food", 12, 100, -4, 3);
            Assert.AreEqual("3天", daysText.text);
        }
    }
}
