using DiplomMurtazin.Core;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DiplomMurtazin.ViewModel
{
    public class DashboardViewModel : BaseViewModel
    {

        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => Set(ref _totalRevenue, value);
        }

        private int _salesCount;
        public int SalesCount
        {
            get => _salesCount;
            set => Set(ref _salesCount, value);
        }

        private int _productsCount;
        public int ProductsCount
        {
            get => _productsCount;
            set => Set(ref _productsCount, value);
        }

        private int _lowStockCount;
        public int LowStockCount
        {
            get => _lowStockCount;
            set => Set(ref _lowStockCount, value);
        }


        private ObservableCollection<Sales> _lastSales =
            new ObservableCollection<Sales>();

        public ObservableCollection<Sales> LastSales
        {
            get => _lastSales;
            set => Set(ref _lastSales, value);
        }


        public SeriesCollection SalesSeries { get; set; }

        public string[] SalesLabels { get; set; }

        public Func<double, string> Formatter { get; set; }


        public DashboardViewModel()
        {
            SalesSeries = new SeriesCollection();

            Formatter = value => value.ToString("N0") + " ₽";

            LoadDashboardData();
        }


        private void LoadDashboardData()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {

                    SalesCount = context.Sales.Count();

                    ProductsCount = context.Products.Count();

                    TotalRevenue =
                        context.Sales.Sum(s => (decimal?)s.TotalAmount) ?? 0;

                    LowStockCount =
                        context.StockBalances.Count(s => s.Quantity <= 5);


                    var latestSales = context.Sales
                        .OrderByDescending(s => s.SaleDateTime)
                        .Take(10)
                        .ToList();

                    LastSales =
                        new ObservableCollection<Sales>(latestSales);


                    var salesByDay = context.Sales
                        .ToList()
                        .GroupBy(s => s.SaleDateTime.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Amount = g.Sum(x => x.TotalAmount)
                        })
                        .OrderBy(x => x.Date)
                        .Take(10)
                        .ToList();

                    SalesLabels = salesByDay
                        .Select(x => x.Date.ToString("dd.MM"))
                        .ToArray();

                    SalesSeries.Clear();

                    SalesSeries.Add(new LineSeries
                    {
                        Title = "Продажи",
                        Values = new ChartValues<decimal>(
                            salesByDay.Select(x => x.Amount))
                    });

                    OnPropertyChanged(nameof(SalesLabels));
                    OnPropertyChanged(nameof(SalesSeries));
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    "Ошибка Dashboard");
            }
        }
    }
}