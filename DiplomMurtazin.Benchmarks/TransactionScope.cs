// BenchmarkBase.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System.Data;
using System.Transactions;

namespace DiplomMurtazin.Benchmarks
{
    [SimpleJob(RunStrategy.ColdStart, iterationCount: 5, warmupCount: 1)]
    [MemoryDiagnoser]
    public abstract class BenchmarkBase
    {
        protected TransactionScope _scope;

        [IterationSetup]
        public void SetupTransaction()
        {
            // Каждая итерация выполняется в своей транзакции, которая будет откатана
            _scope = new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
        }

        [IterationCleanup]
        public void CleanupTransaction()
        {
            _scope?.Dispose();
        }
    }
}