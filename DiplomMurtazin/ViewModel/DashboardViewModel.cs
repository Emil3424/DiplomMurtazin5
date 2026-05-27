// DiplomMurtazin/ViewModel/DashboardViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DiplomMurtazin.Core;
using LiveCharts;
using LiveCharts.Wpf;

namespace DiplomMurtazin.ViewModel
{
    public class DashboardViewModel : BaseViewModel
    {
        private KPMurtazinEntities _context;

        // Основные показатели
        private decimal _totalRevenue;
        public decimal TotalRevenue { get => _totalRevenue; set => Set(ref _totalRevenue, value); }

        private int _salesCount;
        public int SalesCount { get => _salesCount; set => Set(ref _salesCount, value); }

        private int _productsCount;
        public int ProductsCount { get => _productsCount; set => Set(ref _productsCount, value); }

        private int _lowStockCount;
        public int LowStockCount { get => _lowStockCount; set => Set(ref _lowStockCount, value); }

        private ObservableCollection<Sales> _lastSales = new ObservableCollection<Sales>();
        public ObservableCollection<Sales> LastSales { get => _lastSales; set => Set(ref _lastSales, value); }

        // График продаж
        public SeriesCollection SalesSeries { get; set; }
        public string[] SalesLabels { get; set; }
        public Func<double, string> Formatter { get; set; }

        // Фильтры
        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today.AddDays(1);
        private Employees _selectedCashier;
        private Categories _selectedCategory;
        private string _productSearch;
        private ObservableCollection<Employees> _cashiers;
        private ObservableCollection<Categories> _categories;


        public DateTime StartDate { get => _startDate; set => Set(ref _startDate, value); }
        public DateTime EndDate { get => _endDate; set => Set(ref _endDate, value); }
        public Employees SelectedCashier { get => _selectedCashier; set => Set(ref _selectedCashier, value); }
        public Categories SelectedCategory { get => _selectedCategory; set => Set(ref _selectedCategory, value); }
        public string ProductSearch { get => _productSearch; set => Set(ref _productSearch, value); }

        public ObservableCollection<Employees> Cashiers { get => _cashiers; set => Set(ref _cashiers, value); }
        public ObservableCollection<Categories> Categories { get => _categories; set => Set(ref _categories, value); }

        // Статистика по кассирам
        private ObservableCollection<CashierStat> _cashierStats;
        public ObservableCollection<CashierStat> CashierStats { get => _cashierStats; set => Set(ref _cashierStats, value); }

        // Топ-10 товаров
        private ObservableCollection<TopProductStat> _topProducts;
        public ObservableCollection<TopProductStat> TopProducts { get => _topProducts; set => Set(ref _topProducts, value); }

        // Низкие остатки с прогнозом
        private ObservableCollection<LowStockItem> _lowStockItems;
        public ObservableCollection<LowStockItem> LowStockItems { get => _lowStockItems; set => Set(ref _lowStockItems, value); }

        // Команды
        public ICommand RefreshCommand { get; }
        public ICommand SetTodayCommand { get; }
        public ICommand SetYesterdayCommand { get; }
        public ICommand SetWeekCommand { get; }
        public ICommand SetMonthCommand { get; }
        public ICommand SetYearCommand { get; }
        public ICommand CreatePurchaseOrderCommand { get; }

        public DashboardViewModel()
        {
            Formatter = value => value.ToString("N0") + " ₽";
            SalesSeries = new SeriesCollection();

            RefreshCommand = new RelayCommand(_ => LoadData());
            SetTodayCommand = new RelayCommand(_ => SetPeriod(Period.Today));
            SetYesterdayCommand = new RelayCommand(_ => SetPeriod(Period.Yesterday));
            SetWeekCommand = new RelayCommand(_ => SetPeriod(Period.Week));
            SetMonthCommand = new RelayCommand(_ => SetPeriod(Period.Month));
            SetYearCommand = new RelayCommand(_ => SetPeriod(Period.Year));
            CreatePurchaseOrderCommand = new RelayCommand(_ => CreatePurchaseOrder());

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            LoadCashiers();
            LoadCategories();
            LoadData();
        }

        private void LoadCashiers()
        {
            using (var ctx = new KPMurtazinEntities())
            {
                var list = ctx.Employees.Where(e => e.IsActive).OrderBy(e => e.LastName).ToList();
                Cashiers = new ObservableCollection<Employees>(list);
            }
        }

        private void LoadCategories()
        {
            using (var ctx = new KPMurtazinEntities())
            {
                var list = ctx.Categories.OrderBy(c => c.CategoryName).ToList();
                list.Insert(0, new Categories { CategoryID = 0, CategoryName = "Все категории" });
                Categories = new ObservableCollection<Categories>(list);
                SelectedCategory = Categories.First();
            }
        }

        private void SetPeriod(Period period)
        {
            var now = DateTime.Now;
            switch (period)
            {
                case Period.Today:
                    StartDate = now.Date;
                    EndDate = now.Date.AddDays(1);
                    break;
                case Period.Yesterday:
                    StartDate = now.Date.AddDays(-1);
                    EndDate = now.Date.AddDays(-1);
                    break;
                case Period.Week:
                    StartDate = now.Date.AddDays(-(int)now.DayOfWeek + (now.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                    EndDate = StartDate.AddDays(6);
                    break;
                case Period.Month:
                    StartDate = new DateTime(now.Year, now.Month, 1);
                    EndDate = StartDate.AddMonths(1).AddDays(-1);
                    break;
                case Period.Year:
                    StartDate = new DateTime(now.Year, 1, 1);
                    EndDate = new DateTime(now.Year, 12, 31);
                    break;
            }
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (_context = new KPMurtazinEntities())
                {
                    ProductsCount = _context.Products.Count();
                    LoadLowStockItems();

                    var salesQuery = _context.Sales.AsQueryable();
                    salesQuery = salesQuery.Where(s => s.SaleDateTime >= StartDate && s.SaleDateTime <= EndDate);
                    if (SelectedCashier != null)
                        salesQuery = salesQuery.Where(s => s.EmployeeID == SelectedCashier.EmployeeID);

                    bool filterByProduct = (SelectedCategory != null && SelectedCategory.CategoryID > 0) || !string.IsNullOrWhiteSpace(ProductSearch);
                    if (filterByProduct)
                    {
                        var productIdsQuery = _context.Products.AsQueryable();
                        if (SelectedCategory != null && SelectedCategory.CategoryID > 0)
                            productIdsQuery = productIdsQuery.Where(p => p.CategoryID == SelectedCategory.CategoryID);
                        if (!string.IsNullOrWhiteSpace(ProductSearch))
                        {
                            var search = ProductSearch.ToLower();
                            productIdsQuery = productIdsQuery.Where(p => p.ProductName.ToLower().Contains(search) || p.Barcode.Contains(ProductSearch));
                        }
                        var validProductIds = productIdsQuery.Select(p => p.ProductID).ToList();
                        var saleIdsWithProducts = _context.SaleItems.Where(si => validProductIds.Contains(si.ProductID)).Select(si => si.SaleID).Distinct();
                        salesQuery = salesQuery.Where(s => saleIdsWithProducts.Contains(s.SaleID));
                    }

                    var salesList = salesQuery.ToList();

                    TotalRevenue = salesList.Sum(s => s.TotalAmount);
                    SalesCount = salesList.Count;

                    LastSales = new ObservableCollection<Sales>(salesList.OrderByDescending(s => s.SaleDateTime).Take(10));

                    // График продаж
                    var salesByDay = salesList
                        .GroupBy(s => s.SaleDateTime.Date)
                        .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.TotalAmount) })
                        .OrderBy(x => x.Date)
                        .ToList();
                    SalesLabels = salesByDay.Select(x => x.Date.ToString("dd.MM")).ToArray();
                    SalesSeries.Clear();
                    SalesSeries.Add(new LineSeries
                    {
                        Title = "Продажи",
                        Values = new ChartValues<decimal>(salesByDay.Select(x => x.Amount))
                    });
                    OnPropertyChanged(nameof(SalesLabels));
                    OnPropertyChanged(nameof(SalesSeries));

                    // Статистика по кассирам – используем словарь для подстановки ФИО
                    var employeesDict = _context.Employees.ToDictionary(e => e.EmployeeID, e => e.FullName);
                    var cashierStats = salesList
                        .GroupBy(s => s.EmployeeID)
                        .Select(g => new CashierStat
                        {
                            EmployeeName = employeesDict.ContainsKey(g.Key) ? employeesDict[g.Key] : "Неизвестно",
                            SalesCount = g.Count(),
                            TotalAmount = g.Sum(s => s.TotalAmount),
                            AverageCheck = g.Average(s => s.TotalAmount)
                        })
                        .OrderByDescending(c => c.TotalAmount)
                        .ToList();
                    CashierStats = new ObservableCollection<CashierStat>(cashierStats);

                    // Топ-10 товаров – сначала получаем данные из БД, затем подставляем названия
                    var productIdsInSales = salesList.SelectMany(s => s.SaleItems).Select(si => si.ProductID).Distinct().ToList();
                    var productsDict = _context.Products.ToDictionary(p => p.ProductID, p => p.ProductName);

                    var topProductsData = _context.SaleItems
                        .Where(si => productIdsInSales.Contains(si.ProductID))
                        .GroupBy(si => si.ProductID)
                        .Select(g => new { ProductID = g.Key, TotalQuantity = g.Sum(si => si.Quantity) })
                        .OrderByDescending(x => x.TotalQuantity)
                        .Take(10)
                        .ToList();

                    var topProducts = topProductsData.Select(x => new TopProductStat
                    {
                        ProductName = productsDict.ContainsKey(x.ProductID) ? productsDict[x.ProductID] : "Неизвестно",
                        TotalQuantity = x.TotalQuantity
                    }).ToList();

                    TopProducts = new ObservableCollection<TopProductStat>(topProducts);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки дашборда: {ex.Message}", "Ошибка");
            }
        }

        private void LoadLowStockItems()
        {
            using (var ctx = new KPMurtazinEntities())
            {
                var products = ctx.Products.ToList();
                var stock = ctx.StockBalances
                    .GroupBy(sb => sb.ProductID)
                    .Select(g => new { ProductID = g.Key, Quantity = g.Sum(sb => sb.Quantity) })
                    .ToDictionary(k => k.ProductID, v => v.Quantity);

                // Вычисляем среднюю дневную продажу за последние 30 дней (для прогноза)
                var thirtyDaysAgo = DateTime.Today.AddDays(-30);
                var dailySales = ctx.SaleItems
                    .Where(si => si.Sales.SaleDateTime >= thirtyDaysAgo)
                    .GroupBy(si => si.ProductID)
                    .Select(g => new { ProductID = g.Key, TotalQty = g.Sum(si => si.Quantity) })
                    .ToDictionary(k => k.ProductID, v => (double)v.TotalQty / 30.0);

                var lowStockList = new ObservableCollection<LowStockItem>();
                foreach (var p in products)
                {
                    int current = stock.ContainsKey(p.ProductID) ? stock[p.ProductID] : 0;
                    int minStock = p.MinStockLevel ?? 5;
                    if (current < minStock)
                    {
                        double avgPerDay = dailySales.ContainsKey(p.ProductID) ? dailySales[p.ProductID] : 0;
                        int daysUntilOut = avgPerDay > 0 ? (int)Math.Ceiling(current / avgPerDay) : int.MaxValue;
                        lowStockList.Add(new LowStockItem
                        {
                            ProductName = p.ProductName,
                            CurrentStock = current,
                            MinStockLevel = minStock,
                            AvgDailySales = avgPerDay,
                            DaysUntilOut = daysUntilOut == int.MaxValue ? -1 : daysUntilOut
                        });
                    }
                }
                LowStockItems = lowStockList;
                LowStockCount = LowStockItems.Count;
            }
        }

        private void CreatePurchaseOrder()
        {
            if (LowStockItems == null || LowStockItems.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет товаров с низким остатком.", "Информация");
                return;
            }

            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    // Создаём новую накладную (Invoice)
                    var invoice = new Invoices
                    {
                        SupplierID = 1, // можно выбрать первого поставщика или добавить выбор
                        InvoiceNumber = $"ЗАКАЗ-{DateTime.Now:yyyyMMddHHmmss}",
                        InvoiceDate = DateTime.Now,
                        ExpectedDeliveryDate = DateTime.Now.AddDays(7),
                        ShiftID = 1,
                        Status = "Draft",
                        CreatedBy = App.CurrentUser?.EmployeeID ?? 1,
                        CreatedDate = DateTime.Now,
                        Notes = "Автоматически создан из дашборда (низкий остаток)"
                    };
                    ctx.Invoices.Add(invoice);
                    ctx.SaveChanges();

                    foreach (var item in LowStockItems)
                    {
                        var product = ctx.Products.FirstOrDefault(p => p.ProductName == item.ProductName);
                        if (product == null) continue;
                        int orderQty = (item.MinStockLevel * 2) - item.CurrentStock;
                        if (orderQty <= 0) orderQty = item.MinStockLevel;
                        ctx.InvoiceItems.Add(new InvoiceItems
                        {
                            InvoiceID = invoice.InvoiceID,
                            ProductID = product.ProductID,
                            ExpectedQuantity = orderQty,
                            UnitPrice = product.UnitPrice
                        });
                    }
                    ctx.SaveChanges();

                    AuditLogger.Log("CREATE", "PurchaseOrder", $"Создан заказ поставщику №{invoice.InvoiceNumber}", invoice.InvoiceID.ToString());
                    System.Windows.MessageBox.Show($"Создан заказ поставщику №{invoice.InvoiceNumber}. Перейдите в раздел 'Накладные' для редактирования.", "Заказ создан");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка создания заказа: {ex.Message}", "Ошибка");
            }
        }

        private enum Period { Today, Yesterday, Week, Month, Year }
    }

    // Вспомогательные классы для статистики
    public class CashierStat
    {
        public string EmployeeName { get; set; }
        public int SalesCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageCheck { get; set; }
    }

    public class TopProductStat
    {
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
    }

    public class LowStockItem
    {
        public string ProductName { get; set; }
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public double AvgDailySales { get; set; }
        public int DaysUntilOut { get; set; }
        public string DaysUntilOutDisplay => DaysUntilOut < 0 ? "нет продаж" : $"~{DaysUntilOut} дн.";
    }
}