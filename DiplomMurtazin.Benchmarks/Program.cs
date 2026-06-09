using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace DiplomMurtazin.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Запуск всех бенчмарков
            BenchmarkRunner.Run<ProductsLoadBenchmark>();
            BenchmarkRunner.Run<ProductsSearchBenchmark>();
            BenchmarkRunner.Run<SaleCreationBenchmark>();
            BenchmarkRunner.Run<SalesReportBenchmark>();
            BenchmarkRunner.Run<Torg12ImportBenchmark>();

            var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithOptions(ConfigOptions.DisableOptimizationsValidator)
    .AddJob(Job.Default
        .WithWarmupCount(2)
        .WithIterationCount(10)
        .WithInvocationCount(1));

            BenchmarkRunner.Run<ProductsLoadBenchmark>(config);
        }
    }
}