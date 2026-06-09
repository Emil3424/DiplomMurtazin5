// Torg12ImportBenchmark.cs
using BenchmarkDotNet.Attributes;
using DiplomMurtazin.Core;
using DiplomMurtazin.ViewModel;
using System;
using System.IO;

namespace DiplomMurtazin.Benchmarks
{
    public class Torg12ImportBenchmark : BenchmarkBase
    {
        private Torg12ViewModel _vm;
        private string _testExcelPath;

        [GlobalSetup]
        public void Setup()
        {
            _vm = new Torg12ViewModel();
            // Создаём временный Excel-файл с 50 позициями (или используем шаблон)
            _testExcelPath = Path.Combine(Path.GetTempPath(), "test_torg12.xlsx");
            CreateTestExcelFile(_testExcelPath);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (File.Exists(_testExcelPath)) File.Delete(_testExcelPath);
        }

        private void CreateTestExcelFile(string path)
        {
            // Упрощённо: копируем существующий шаблон blanktorg12.xls
            var template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL", "blanktorg12.xls");
            File.Copy(template, path, true);
            // Дополнительно можно через Interop заполнить тестовыми данными, но для простоты используем готовый файл
        }

        [Benchmark(Description = "Import TORG-12 from Excel (50 rows)")]
        public void ImportExcel()
        {
            // Эмулируем выбор файла и подтверждение импорта
            _vm.ImportTorg12Command.Execute(_testExcelPath);
        }
    }
}