using DiplomMurtazin;
using DiplomMurtazin.Core;
using DiplomMurtazin.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DiplomMurtazin.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void RelayCommand_CanExecute_WithoutPredicate_ReturnsTrue()
        {
            var command = new RelayCommand(_ => { });

            bool result = command.CanExecute(null);

            Assert.IsTrue(result);
        }
        [TestMethod]
        public void RelayCommand_Execute_CallsAction()
        {
            bool executed = false;

            var command = new RelayCommand(_ =>
            {
                executed = true;
            });

            command.Execute(null);

            Assert.IsTrue(executed);
        }
        [TestMethod]
        public void RelayCommand_CanExecute_WithPredicate_ReturnsFalse()
        {
            var command = new RelayCommand(
                _ => { },
                _ => false);

            bool result = command.CanExecute(null);

            Assert.IsFalse(result);
        }
        [TestMethod]
        public void ReceiptItem_Total_CalculatedCorrectly()
        {
            var item = new ReceiptItem
            {
                Price = 150m,
                Quantity = 3
            };

            Assert.AreEqual(450m, item.Total);
        }
        [TestMethod]
        public void SortOption_ToString_ReturnsName()
        {
            var option = new SortOption
            {
                Name = "Фамилия А-Я"
            };

            Assert.AreEqual("Фамилия А-Я", option.ToString());
        }
    }
}
