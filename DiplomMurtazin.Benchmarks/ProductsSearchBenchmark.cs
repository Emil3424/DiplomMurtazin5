// ProductsSearchBenchmark.cs
using BenchmarkDotNet.Attributes;
using DiplomMurtazin;
using DiplomMurtazin.ViewModel;
using System.Linq;

namespace DiplomMurtazin.Benchmarks
{
    public class ProductsSearchBenchmark : BenchmarkBase
    {
        private ProductsViewModel _vm;
        private Categories _testCategory;

        [GlobalSetup]
        public void Setup()
        {
            _vm = new ProductsViewModel();
            // Предварительно загружаем данные, чтобы категории и товары были в кэше EF
            _vm.LoadedCommand.Execute(null);

            using (var ctx = new KPMurtazinEntities())
            {
                _testCategory = ctx.Categories.FirstOrDefault();
            }
        }

        [Benchmark(Description = "Search products by name and category")]
        public void SearchAndFilter()
        {
            _vm.SearchText = "смартфон";        // реально существующее слово
            _vm.SelectedCategoryFilter = _testCategory;
            // Принудительно вызываем применение фильтров
            _vm.GetType().GetMethod("ApplyFilters",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_vm, null);
        }
    }
}   