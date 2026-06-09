// SaleCreationBenchmark.cs
using BenchmarkDotNet.Attributes;
using DiplomMurtazin.ViewModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace DiplomMurtazin.Benchmarks
{
    public class SaleCreationBenchmark : BenchmarkBase
    {
        private SalesViewModel _vm;

        [GlobalSetup]
        public void Setup()
        {
            _vm = new SalesViewModel();
            _vm.LoadedCommand.Execute(null);   // загружаем товары

            // Добавляем два товара в чек
            var product1 = _vm.FilteredProducts.FirstOrDefault(p => p.StockQuantity >= 1);
            var product2 = _vm.FilteredProducts.FirstOrDefault(p => p.ProductID != product1?.ProductID && p.StockQuantity >= 1);
            if (product1 != null) _vm.AddToSaleCommand.Execute(product1);
            if (product2 != null) _vm.AddToSaleCommand.Execute(product2);
        }

        [Benchmark(Description = "Complete a sale (insert sale, items, history, units)")]
        public void CreateSale()
        {
            _vm.CompleteSaleCommand.Execute(null);
        }
    }
}   