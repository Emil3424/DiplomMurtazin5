// ProductsLoadBenchmark.cs
using BenchmarkDotNet.Attributes;
using DiplomMurtazin.ViewModel;

namespace DiplomMurtazin.Benchmarks
{
    [SimpleJob(iterationCount: 10, warmupCount: 2, invocationCount: 1)]
    [MemoryDiagnoser]
    public class ProductsLoadBenchmark: BenchmarkBase
    {
        private ProductsViewModel _vm;

        [GlobalSetup]
        public void Setup()
        {
            _vm = new ProductsViewModel();
        }

        [Benchmark(Description = "Load all products with stock calculation")]
        public void LoadAllProducts()
        {
            // Имитируем вызов команды загрузки (без UI)
            _vm.LoadedCommand.Execute(null);
        }
    }
}