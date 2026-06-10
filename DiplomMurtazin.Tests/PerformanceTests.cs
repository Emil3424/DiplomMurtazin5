using System;
using System.Diagnostics;
using DiplomMurtazin.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiplomMurtazin.Tests
{
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        public void ReceiptItem_LoadTest_50000Items()
        {
            const int count = 50000;
            decimal total = 0;

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                var item = new ReceiptItem
                {
                    Price = 150m,
                    Quantity = 3
                };

                total += item.Total;
            }

            sw.Stop();

            Assert.AreEqual(22500000m, total);
            Assert.IsTrue(sw.ElapsedMilliseconds < 2000);
        }
    }
}