// SalesReportBenchmark.cs
using BenchmarkDotNet.Attributes;
using DiplomMurtazin.ViewModel;
using System;

namespace DiplomMurtazin.Benchmarks
{
    public class SalesReportBenchmark : BenchmarkBase
    {
        private ReportsViewModel _vm;

        [GlobalSetup]
        public void Setup()
        {
            _vm = new ReportsViewModel();
            _vm.StartDate = DateTime.Today.AddMonths(-1);
            _vm.EndDate = DateTime.Today;
        }

        [Benchmark(Description = "Generate sales report (last month)")]
        public void GenerateReport()
        {
            _vm.GenerateSalesReportCommand.Execute(null);
        }
    }
}