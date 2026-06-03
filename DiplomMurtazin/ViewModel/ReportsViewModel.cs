using DiplomMurtazin.Core;
using DiplomMurtazin.Model;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class ReturnReportItem
    {
        public int ReturnID { get; set; }
        public int UnitID { get; set; }
        public string ProductName { get; set; }
        public DateTime ReturnDate { get; set; }
        public string ReturnReason { get; set; }
        public bool IsWarrantyCase { get; set; }
        public decimal RefundAmount { get; set; }
        public string Status { get; set; }
        public string ProcessedBy { get; set; }
    }

    public class Torg12ReportItem
    {
        public int Torg12ID { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime DocumentDate { get; set; }
        public string ReceiverName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; }
        public int ItemsCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; }
    }

    public class ProductHistoryItem
    {
        public DateTime EventDate { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
        public string SourceDocument { get; set; }
    }

    public class ProductSelectionItem : BaseViewModel
    {
        public int ProductID { get; set; }

        public string ProductName { get; set; }

        public event Action SelectionChanged;

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (Set(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke();
                }
            }
        }
    }

    public class ReportsViewModel : BaseViewModel
    {
        private KPMurtazinEntities _context;

        // Существующие отчёты
        private ObservableCollection<SalesReportItem> _salesReport;
        private ObservableCollection<SalesSummaryItem> _salesSummary;
        private ObservableCollection<StockReportItem> _stockReport;
        private DateTime _startDate;
        private DateTime _endDate;
        private string _statusMessage;
        private string _statusColor;
        public int TotalProducts => StockReport?.Count ?? 0;
        public int OutOfStockCount => StockReport?.Count(s => s.CurrentStock == 0) ?? 0;
        public int BelowMinCount => StockReport?.Count(s => s.Status == "Ниже минимума") ?? 0;

        // Новые отчёты
        private ObservableCollection<ReturnReportItem> _returnReport;
        private DateTime _returnStartDate = DateTime.Today.AddMonths(-1);
        private DateTime _returnEndDate = DateTime.Today;
        private string _returnStatusFilter = "Все";
        private bool? _isWarrantyFilter = null;

        private ObservableCollection<Torg12ReportItem> _torg12Report;
        private DateTime _torg12StartDate = DateTime.Today.AddMonths(-1);
        private DateTime _torg12EndDate = DateTime.Today;
        private string _torg12StatusFilter = "Все";
        private string _torg12ReceiverFilter = "";

        private ObservableCollection<ProductHistoryItem> _productHistory;
        private ObservableCollection<Products> _allProducts;
        private Products _selectedProductForHistory;

        // Фильтр каталога товаров
        private bool _enableProductFilter = false;
        private ObservableCollection<Categories> _categories;
        private Categories _selectedCategoryFilterForProducts;
        private ObservableCollection<string> _productNames;
        private ObservableCollection<ProductSelectionItem> _selectedProductItems;

        // Свойства
        public ObservableCollection<ReturnReportItem> ReturnReport { get => _returnReport; set => Set(ref _returnReport, value); }
        public ObservableCollection<Torg12ReportItem> Torg12Report { get => _torg12Report; set => Set(ref _torg12Report, value); }
        public ObservableCollection<ProductHistoryItem> ProductHistory { get => _productHistory; set => Set(ref _productHistory, value); }
        public ObservableCollection<Products> AllProducts { get => _allProducts; set => Set(ref _allProducts, value); }
        public Products SelectedProductForHistory { get => _selectedProductForHistory; set { if (Set(ref _selectedProductForHistory, value) && value != null) LoadProductHistory(); } }

        public DateTime ReturnStartDate { get => _returnStartDate; set => Set(ref _returnStartDate, value); }
        public DateTime ReturnEndDate { get => _returnEndDate; set => Set(ref _returnEndDate, value); }
        public string ReturnStatusFilter { get => _returnStatusFilter; set => Set(ref _returnStatusFilter, value); }
        public bool? IsWarrantyFilter { get => _isWarrantyFilter; set => Set(ref _isWarrantyFilter, value); }
        public ObservableCollection<string> ReturnStatuses { get; } = new ObservableCollection<string> { "Все", "SOLD", "RETURNED", "DEFECTIVE" };
        public ObservableCollection<string> WarrantyOptions { get; } = new ObservableCollection<string> { "Все", "Только гарантийные", "Только негарантийные" };
        public string SelectedWarrantyOption
        {
            get => IsWarrantyFilter == null ? "Все" : (IsWarrantyFilter == true ? "Только гарантийные" : "Только негарантийные");
            set
            {
                if (value == "Все") IsWarrantyFilter = null;
                else if (value == "Только гарантийные") IsWarrantyFilter = true;
                else IsWarrantyFilter = false;
            }
        }

        public DateTime Torg12StartDate { get => _torg12StartDate; set => Set(ref _torg12StartDate, value); }
        public DateTime Torg12EndDate { get => _torg12EndDate; set => Set(ref _torg12EndDate, value); }
        public string Torg12StatusFilter { get => _torg12StatusFilter; set => Set(ref _torg12StatusFilter, value); }
        public string Torg12ReceiverFilter { get => _torg12ReceiverFilter; set => Set(ref _torg12ReceiverFilter, value); }
        public ObservableCollection<string> Torg12Statuses { get; } = new ObservableCollection<string> { "Все", "Draft", "Completed" };

        public bool EnableProductFilter { get => _enableProductFilter; set => Set(ref _enableProductFilter, value); }
        public ObservableCollection<Categories> Categories { get => _categories; set => Set(ref _categories, value); }
        public Categories SelectedCategoryFilterForProducts
        {
            get => _selectedCategoryFilterForProducts;
            set
            {
                if (Set(ref _selectedCategoryFilterForProducts, value))
                    UpdateProductListByCategory();
            }
        }
        public ObservableCollection<string> ProductNames { get => _productNames; set => Set(ref _productNames, value); }
        public ObservableCollection<ProductSelectionItem> SelectedProductItems
        {
            get => _selectedProductItems;
            set => Set(ref _selectedProductItems, value);
        }
        public string SelectedProductsText => SelectedProductItems != null && SelectedProductItems.Any(x => x.IsSelected)
            ? string.Join(", ", SelectedProductItems.Where(x => x.IsSelected).Select(x => x.ProductName))
            : "Не выбрано";

        public ObservableCollection<SalesReportItem> SalesReport { get => _salesReport; set => Set(ref _salesReport, value); }
        public ObservableCollection<SalesSummaryItem> SalesSummary { get => _salesSummary; set => Set(ref _salesSummary, value); }
        public ObservableCollection<StockReportItem> StockReport { get => _stockReport; set => Set(ref _stockReport, value); }
        public DateTime StartDate { get => _startDate; set => Set(ref _startDate, value); }
        public DateTime EndDate { get => _endDate; set => Set(ref _endDate, value); }
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
        public string StatusColor { get => _statusColor; set => Set(ref _statusColor, value); }

        // Команды
        public ICommand LoadedCommand { get; }
        public ICommand GenerateSalesReportCommand { get; }
        public ICommand GenerateStockReportCommand { get; }
        public ICommand ExportSalesReportPdfCommand { get; }
        public ICommand ExportSalesReportCsvCommand { get; }
        public ICommand ExportSalesReportExcelCommand { get; }
        public ICommand ExportStockReportPdfCommand { get; }
        public ICommand ExportStockReportCsvCommand { get; }
        public ICommand ExportStockReportExcelCommand { get; }

        public ICommand GenerateReturnReportCommand { get; }
        public ICommand GenerateTorg12ReportCommand { get; }
        public ICommand GenerateProductHistoryCommand { get; }
        public ICommand ExportReturnReportPdfCommand { get; }
        public ICommand ExportReturnReportCsvCommand { get; }
        public ICommand ExportReturnReportExcelCommand { get; }
        public ICommand ExportTorg12ReportPdfCommand { get; }
        public ICommand ExportTorg12ReportCsvCommand { get; }
        public ICommand ExportTorg12ReportExcelCommand { get; }

        public ReportsViewModel()
        {
            LoadedCommand = new RelayCommand(OnLoaded);
            GenerateSalesReportCommand = new RelayCommand(GenerateSalesReport);
            GenerateStockReportCommand = new RelayCommand(GenerateStockReport);

            ExportSalesReportPdfCommand = new RelayCommand(ExportSalesReportPdf, _ => SalesReport != null && SalesReport.Any());
            ExportSalesReportCsvCommand = new RelayCommand(ExportSalesReportCsv, _ => SalesReport != null && SalesReport.Any());
            ExportSalesReportExcelCommand = new RelayCommand(ExportSalesReportExcel, _ => SalesReport != null && SalesReport.Any());
            ExportStockReportPdfCommand = new RelayCommand(ExportStockReportPdf, _ => StockReport != null && StockReport.Any());
            ExportStockReportCsvCommand = new RelayCommand(ExportStockReportCsv, _ => StockReport != null && StockReport.Any());
            ExportStockReportExcelCommand = new RelayCommand(ExportStockReportExcel, _ => StockReport != null && StockReport.Any());

            GenerateReturnReportCommand = new RelayCommand(GenerateReturnReport);
            GenerateTorg12ReportCommand = new RelayCommand(GenerateTorg12Report);
            GenerateProductHistoryCommand = new RelayCommand(GenerateProductHistory);

            ExportReturnReportPdfCommand = new RelayCommand(ExportReturnReportPdf, _ => ReturnReport != null && ReturnReport.Any());
            ExportReturnReportCsvCommand = new RelayCommand(ExportReturnReportCsv, _ => ReturnReport != null && ReturnReport.Any());
            ExportReturnReportExcelCommand = new RelayCommand(ExportReturnReportExcel, _ => ReturnReport != null && ReturnReport.Any());

            ExportTorg12ReportPdfCommand = new RelayCommand(ExportTorg12ReportPdf, _ => Torg12Report != null && Torg12Report.Any());
            ExportTorg12ReportCsvCommand = new RelayCommand(ExportTorg12ReportCsv, _ => Torg12Report != null && Torg12Report.Any());
            ExportTorg12ReportExcelCommand = new RelayCommand(ExportTorg12ReportExcel, _ => Torg12Report != null && Torg12Report.Any());

            ExportProductCatalogPdfCommand = new RelayCommand(ExportProductCatalogPdf);
            ExportProductCatalogCsvCommand = new RelayCommand(ExportProductCatalogCsv);
            ExportProductCatalogExcelCommand = new RelayCommand(ExportProductCatalogExcel);

            AddSelectedProductCommand =
    new RelayCommand(AddSelectedProducts);

            _context = new KPMurtazinEntities();
            _context.Configuration.ProxyCreationEnabled = false;

            StartDate = DateTime.Now.AddMonths(-1);
            EndDate = DateTime.Now;
            StatusColor = "#3498db";
            StatusMessage = "Готов к работе";

            LoadAllProducts();
            LoadCategories();
            SelectedProductItems =
    new ObservableCollection<ProductSelectionItem>(
        AllProducts.Select(p =>
        {
            var item = new ProductSelectionItem
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                IsSelected = false
            };

            item.SelectionChanged += () =>
            {
                OnPropertyChanged(nameof(SelectedProductsText));
            };

            return item;
        }));
        }

        private void OnLoaded(object parameter)
        {
            _context = new KPMurtazinEntities();
            _context.Configuration.ProxyCreationEnabled = false;
        }
        private void LoadAllProducts()
        {
            using (var ctx = new KPMurtazinEntities())
            {
                AllProducts = new ObservableCollection<Products>(
                    ctx.Products.OrderBy(p => p.ProductName).ToList());

                ProductNames = new ObservableCollection<string>(
                    AllProducts.Select(p => p.ProductName));

                SelectedProductItems = new ObservableCollection<ProductSelectionItem>();

                foreach (var p in AllProducts)
                {
                    var item = new ProductSelectionItem
                    {
                        ProductID = p.ProductID,
                        ProductName = p.ProductName,
                        IsSelected = false
                    };

                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ProductSelectionItem.IsSelected))
                        {
                            OnPropertyChanged(nameof(SelectedProductsText));
                        }
                    };

                    SelectedProductItems.Add(item);
                }
            }
        }
        private void AddSelectedProducts(object parameter)
        {
            OnPropertyChanged(nameof(SelectedProductsText));

            StatusMessage = "Товары добавлены в фильтр";
            StatusColor = "#27ae60";
        }
        public ICommand AddSelectedProductCommand { get; }
        private void ExportProductCatalogExcel(object parameter)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xls)|*.xls",
                    FileName = $"Каталог_товаров_{DateTime.Now:yyyyMMddHHmmss}.xls",
                    DefaultExt = ".xls"
                };

                if (dialog.ShowDialog() == true)
                {
                    CreateProductCatalogExcel(dialog.FileName);
                    OnReportSaved(dialog.FileName, "Каталог товаров", "Excel");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }
        private void CreateProductCatalogExcel(string filename)
        {
            var products = _context.Products
                .Include(p => p.Categories)
                .ToList();

            var stockBalances = _context.StockBalances
                .GroupBy(sb => sb.ProductID)
                .Select(g => new { ProductId = g.Key, TotalStock = g.Sum(sb => sb.Quantity) })
                .ToDictionary(x => x.ProductId, x => x.TotalStock);

            using (var writer = new StreamWriter(filename, false, System.Text.Encoding.Unicode))
            {
                writer.WriteLine("ID\tНаименование\tКатегория\tШтрих-код\tПроизводитель\tМодель\tЦена\tГарантия (мес)\tОстаток");
                foreach (var product in products.OrderBy(p => p.ProductName))
                {
                    int stock = stockBalances.ContainsKey(product.ProductID) ? stockBalances[product.ProductID] : 0;
                    writer.WriteLine($"{product.ProductID}\t{product.ProductName}\t{product.Categories?.CategoryName ?? ""}\t{product.Barcode}\t{product.Manufacturer ?? ""}\t{product.Model ?? ""}\t{product.UnitPrice:F2}\t{product.WarrantyMonths}\t{stock}");
                }
            }
        }
        private void ExportProductCatalogPdf(object parameter)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Каталог_товаров_{DateTime.Now:yyyyMMddHHmmss}.pdf",
                    DefaultExt = ".pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    CreateProductCatalogPdf(dialog.FileName);
                    OnReportSaved(dialog.FileName, "Каталог товаров", "PDF");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }
        private void OnReportSaved(string fileName, string reportName, string format)
        {
            StatusMessage = $"{reportName} сохранен: {Path.GetFileName(fileName)}";
            StatusColor = "#27ae60";
            AuditLogger.Log("REPORT_EXPORT", reportName, $"{reportName} экспортирован в формат {format}", metadata: $"File={Path.GetFileName(fileName)}");

            var result = MessageBox.Show("Файл сохранен. Открыть его?",
                "Успешно", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Process.Start(fileName);
            }
        }
        private void CreateProductCatalogPdf(string filename)
        {
            var products = _context.Products
                .Include(p => p.Categories)
                .OrderBy(p => p.ProductName)
                .ToList();

            using (var document = new PdfDocument())
            {
                document.Info.Title = "Каталог товаров";
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                var font = new XFont("Arial", 9, XFontStyleEx.Regular);
                double y = 30;
                const double left = 30;

                gfx.DrawString("КАТАЛОГ ТОВАРОВ", fontTitle, XBrushes.DarkBlue, left, y);
                y += 25;
                gfx.DrawString("Наименование", font, XBrushes.Black, left, y);
                gfx.DrawString("Категория", font, XBrushes.Black, left + 220, y);
                gfx.DrawString("Цена", font, XBrushes.Black, left + 360, y);
                y += 15;

                foreach (var product in products)
                {
                    if (y > page.Height.Point - 40)
                    {
                        page = document.AddPage();
                        page.Width = XUnit.FromPoint(595);
                        page.Height = XUnit.FromPoint(842);
                        gfx.Dispose();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 30;
                    }

                    gfx.DrawString(TruncateString(product.ProductName, 40), font, XBrushes.Black, left, y);
                    gfx.DrawString(TruncateString(product.Categories?.CategoryName ?? "", 20), font, XBrushes.Black, left + 220, y);
                    gfx.DrawString($"{product.UnitPrice:F2} ₽", font, XBrushes.Black, left + 360, y);
                    y += 14;
                }

                gfx.Dispose();
                document.Save(filename);
            }
        }
        private void ExportProductCatalogCsv(object parameter)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Каталог_товаров_{DateTime.Now:yyyyMMddHHmmss}.csv",
                    DefaultExt = ".csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    CreateProductCatalogCsv(dialog.FileName);
                    OnReportSaved(dialog.FileName, "Каталог товаров", "CSV");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }
        private void CreateProductCatalogCsv(string filename)
        {
            var products = _context.Products
                .Include(p => p.Categories)
                .ToList();

            var stockBalances = _context.StockBalances
                .GroupBy(sb => sb.ProductID)
                .Select(g => new { ProductId = g.Key, TotalStock = g.Sum(sb => sb.Quantity) })
                .ToDictionary(x => x.ProductId, x => x.TotalStock);

            using (var writer = new StreamWriter(filename, false, System.Text.Encoding.UTF8))
            {
                // Заголовки
                writer.WriteLine("ID;Наименование;Категория;Штрих-код;Производитель;Модель;Цена;Гарантия (мес);Остаток");

                // Данные
                foreach (var product in products.OrderBy(p => p.ProductName))
                {
                    int stock = stockBalances.ContainsKey(product.ProductID) ? stockBalances[product.ProductID] : 0;

                    writer.WriteLine($"{product.ProductID};" +
                                    $"{EscapeCsv(product.ProductName)};" +
                                    $"{EscapeCsv(product.Categories?.CategoryName ?? "")};" +
                                    $"{product.Barcode};" +
                                    $"{EscapeCsv(product.Manufacturer ?? "")};" +
                                    $"{EscapeCsv(product.Model ?? "")};" +
                                    $"{product.UnitPrice:F2};" +
                                    $"{product.WarrantyMonths};" +
                                    $"{stock}");
                }
            }
        }
        private void CreateSalesReportPdf(string filename)
        {
            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Отчет по продажам за {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";
                document.Info.Creator = "KPMurtazin";

                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595); // A4 ширина
                page.Height = XUnit.FromPoint(842); // A4 высота

                // Не используем using для gfx, а создаем новую переменную при смене страницы
                var gfx = XGraphics.FromPdfPage(page);

                try
                {
                    var fontTitle = new XFont("Arial", 16, XFontStyleEx.Bold);
                    var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
                    var fontNormal = new XFont("Arial", 9, XFontStyleEx.Regular);

                    double yPos = 30;
                    double leftMargin = 40;

                    // Заголовок
                    gfx.DrawString("ОТЧЕТ ПО ПРОДАЖАМ", fontTitle, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 25;
                    gfx.DrawString($"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}", fontNormal, XBrushes.Black, leftMargin, yPos);
                    yPos += 25;
                    gfx.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", fontNormal, XBrushes.Black, leftMargin, yPos);
                    yPos += 30;

                    // Сводка
                    if (SalesSummary != null && SalesSummary.Count > 0)
                    {
                        gfx.DrawString("СВОДКА ПО СОТРУДНИКАМ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                        yPos += 20;

                        // Заголовки таблицы
                        gfx.DrawString("Сотрудник", fontHeader, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString("Продаж", fontHeader, XBrushes.Black, leftMargin + 200, yPos);
                        gfx.DrawString("Сумма", fontHeader, XBrushes.Black, leftMargin + 280, yPos);
                        gfx.DrawString("Ср. чек", fontHeader, XBrushes.Black, leftMargin + 360, yPos);
                        yPos += 15;

                        foreach (var item in SalesSummary)
                        {
                            gfx.DrawString(item.EmployeeName, fontNormal, XBrushes.Black, leftMargin, yPos);
                            gfx.DrawString(item.SalesCount.ToString(), fontNormal, XBrushes.Black, leftMargin + 200, yPos);
                            gfx.DrawString(item.TotalAmount.ToString("F2") + " ₽", fontNormal, XBrushes.Black, leftMargin + 280, yPos);
                            gfx.DrawString(item.AverageCheck.ToString("F2") + " ₽", fontNormal, XBrushes.Black, leftMargin + 360, yPos);
                            yPos += 15;
                        }
                        yPos += 20;
                    }

                    // Детали продаж
                    gfx.DrawString("ДЕТАЛИ ПРОДАЖ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 20;

                    // Заголовки деталей
                    gfx.DrawString("№", fontHeader, XBrushes.Black, leftMargin, yPos);
                    gfx.DrawString("Дата", fontHeader, XBrushes.Black, leftMargin + 40, yPos);
                    gfx.DrawString("Сотрудник", fontHeader, XBrushes.Black, leftMargin + 120, yPos);
                    gfx.DrawString("Способ оплаты", fontHeader, XBrushes.Black, leftMargin + 250, yPos);
                    gfx.DrawString("Кол-во", fontHeader, XBrushes.Black, leftMargin + 350, yPos);
                    gfx.DrawString("Сумма", fontHeader, XBrushes.Black, leftMargin + 400, yPos);
                    yPos += 15;

                    int count = 0;
                    foreach (var item in SalesReport.Take(50)) // Ограничим для примера
                    {
                        if (yPos > page.Height.Point - 50)
                        {
                            // Новая страница
                            page = document.AddPage();
                            page.Width = XUnit.FromPoint(595);
                            page.Height = XUnit.FromPoint(842);

                            // Освобождаем предыдущий gfx и создаем новый
                            gfx.Dispose();
                            gfx = XGraphics.FromPdfPage(page);

                            yPos = 30;

                            // Повторяем заголовки на новой странице
                            gfx.DrawString("№", fontHeader, XBrushes.Black, leftMargin, yPos);
                            gfx.DrawString("Дата", fontHeader, XBrushes.Black, leftMargin + 40, yPos);
                            gfx.DrawString("Сотрудник", fontHeader, XBrushes.Black, leftMargin + 120, yPos);
                            gfx.DrawString("Способ оплаты", fontHeader, XBrushes.Black, leftMargin + 250, yPos);
                            gfx.DrawString("Кол-во", fontHeader, XBrushes.Black, leftMargin + 350, yPos);
                            gfx.DrawString("Сумма", fontHeader, XBrushes.Black, leftMargin + 400, yPos);
                            yPos += 15;
                        }

                        gfx.DrawString(item.SaleId.ToString(), fontNormal, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString(item.SaleDate.ToString("dd.MM.yy HH:mm"), fontNormal, XBrushes.Black, leftMargin + 40, yPos);
                        gfx.DrawString(item.EmployeeName, fontNormal, XBrushes.Black, leftMargin + 120, yPos);
                        gfx.DrawString(item.PaymentMethod, fontNormal, XBrushes.Black, leftMargin + 250, yPos);
                        gfx.DrawString(item.ItemsCount.ToString(), fontNormal, XBrushes.Black, leftMargin + 350, yPos);
                        gfx.DrawString(item.TotalAmount.ToString("F2") + " ₽", fontNormal, XBrushes.Black, leftMargin + 400, yPos);
                        yPos += 15;
                        count++;
                    }

                    if (SalesReport.Count > 50)
                    {
                        yPos += 10;
                        gfx.DrawString($"... и еще {SalesReport.Count - 50} записей", fontNormal, XBrushes.Gray, leftMargin, yPos);
                    }

                    // Итог
                    yPos = page.Height.Point - 50;
                    gfx.DrawString("────────────────────────────────────", fontNormal, XBrushes.Black, leftMargin, yPos);
                    yPos += 15;
                    gfx.DrawString($"ОБЩИЙ ИТОГ: {SalesReport.Sum(s => s.TotalAmount):F2} ₽", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                }
                finally
                {
                    gfx.Dispose();
                }

                document.Save(filename);
            }
        }

        private void LoadCategories()
        {
            using (var ctx = new KPMurtazinEntities())
            {
                var cats = ctx.Categories.OrderBy(c => c.CategoryName).ToList();
                cats.Insert(0, new Categories { CategoryID = 0, CategoryName = "Все категории" });
                Categories = new ObservableCollection<Categories>(cats);
                SelectedCategoryFilterForProducts = Categories.First();
            }
        }

        private void UpdateProductListByCategory()
        {
            if (SelectedCategoryFilterForProducts == null) return;
            if (SelectedCategoryFilterForProducts.CategoryID == 0)
                ProductNames = new ObservableCollection<string>(AllProducts.Select(p => p.ProductName));
            else
            {
                var catId = SelectedCategoryFilterForProducts.CategoryID;
                ProductNames = new ObservableCollection<string>(AllProducts.Where(p => p.CategoryID == catId).Select(p => p.ProductName));
            }
        }

        // ========== ОТЧЁТ ПО ПРОДАЖАМ ==========
        private void GenerateSalesReport(object parameter)
        {
            try
            {
                var sales = _context.Sales
                    .Include(s => s.Employees)
                    .Include(s => s.SaleItems)
                    .Where(s => s.SaleDateTime >= StartDate && s.SaleDateTime <= EndDate)
                    .ToList();

                SalesReport = new ObservableCollection<SalesReportItem>(
                    sales.Select(s => new SalesReportItem
                    {
                        SaleId = s.SaleID,
                        SaleDate = s.SaleDateTime,
                        EmployeeName = s.Employees != null ? $"{s.Employees.LastName} {s.Employees.FirstName}" : "Неизвестно",
                        PaymentMethod = s.PaymentMethod,
                        ItemsCount = s.SaleItems.Count,
                        TotalAmount = s.TotalAmount
                    }).OrderByDescending(s => s.SaleDate)
                );

                SalesSummary = new ObservableCollection<SalesSummaryItem>(
                    sales.GroupBy(s => s.EmployeeID)
                         .Select(g => new SalesSummaryItem
                         {
                             EmployeeName = g.First().Employees != null ? $"{g.First().Employees.LastName} {g.First().Employees.FirstName}" : "Неизвестно",
                             SalesCount = g.Count(),
                             TotalAmount = g.Sum(s => s.TotalAmount),
                             AverageCheck = g.Count() > 0 ? g.Sum(s => s.TotalAmount) / g.Count() : 0
                         }).OrderByDescending(s => s.TotalAmount)
                );

                StatusMessage = $"Отчет по продажам сгенерирован. Продаж: {SalesReport.Count}";
                StatusColor = "#27ae60";
                AuditLogger.Log("REPORT_GENERATE", "SalesReport", $"Сформирован отчет по продажам за {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка генерации отчета: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }

        private void ExportSalesReportPdf(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = $"Отчет_по_продажам_{DateTime.Now:yyyyMMddHHmmss}.pdf" };
            if (dialog.ShowDialog() != true) return;
            CreateSalesReportPdf(dialog.FileName);
            OpenFile(dialog.FileName);
        }

        private void ExportSalesReportCsv(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Отчет_по_продажам_{DateTime.Now:yyyyMMddHHmmss}.csv" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
            {
                writer.WriteLine("ID продажи;Дата;Сотрудник;Способ оплаты;Позиций;Сумма");
                foreach (var s in SalesReport)
                    writer.WriteLine($"{s.SaleId};{s.SaleDate:dd.MM.yyyy HH:mm};{EscapeCsv(s.EmployeeName)};{EscapeCsv(s.PaymentMethod)};{s.ItemsCount};{s.TotalAmount:F2}");
            }
            OpenFile(dialog.FileName);
        }

        private void ExportSalesReportExcel(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "Excel files (*.xls)|*.xls", FileName = $"Отчет_по_продажам_{DateTime.Now:yyyyMMddHHmmss}.xls" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.Unicode))
            {
                writer.WriteLine("ID продажи\tДата\tСотрудник\tСпособ оплаты\tПозиций\tСумма");
                foreach (var s in SalesReport)
                    writer.WriteLine($"{s.SaleId}\t{s.SaleDate:dd.MM.yyyy HH:mm}\t{s.EmployeeName}\t{s.PaymentMethod}\t{s.ItemsCount}\t{s.TotalAmount:F2}");
            }
            OpenFile(dialog.FileName);
        }

        // ========== ОТЧЁТ ПО ОСТАТКАМ ==========
        private void GenerateStockReport(object parameter)
        {
            try
            {
                var products = _context.Products.Include(p => p.Categories).ToList();
                var stockBalances = _context.StockBalances.GroupBy(sb => sb.ProductID)
                    .ToDictionary(g => g.Key, g => g.Sum(sb => sb.Quantity));
                StockReport = new ObservableCollection<StockReportItem>();
                foreach (var p in products)
                {
                    int current = stockBalances.ContainsKey(p.ProductID) ? stockBalances[p.ProductID] : 0;
                    int min = p.MinStockLevel ?? 5;
                    string status = current == 0 ? "Отсутствует" : current < min ? "Ниже минимума" : current < min * 2 ? "Близко к минимуму" : "Норма";
                    StockReport.Add(new StockReportItem
                    {
                        ProductId = p.ProductID,
                        ProductName = p.ProductName,
                        Category = p.Categories?.CategoryName ?? "Без категории",
                        CurrentStock = current,
                        MinStockLevel = min,
                        Status = status,
                        UnitPrice = p.UnitPrice,
                        TotalValue = current * p.UnitPrice
                    });
                }
                StatusMessage = $"Отчет по остаткам сгенерирован. Товаров: {StockReport.Count}";
                StatusColor = "#27ae60";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка генерации отчета: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }

        private void ExportStockReportPdf(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = $"Отчет_по_остаткам_{DateTime.Now:yyyyMMddHHmmss}.pdf" };
            if (dialog.ShowDialog() != true) return;
            CreateStockReportPdf(dialog.FileName);
            OpenFile(dialog.FileName);
        }

        private void CreateStockReportPdf(string filename)
        {
            using (var document = new PdfDocument())
            {
                document.Info.Title = "Отчет по остаткам товаров";
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
                var fontNormal = new XFont("Arial", 9, XFontStyleEx.Regular);
                double y = 30, left = 40;

                gfx.DrawString("ОТЧЕТ ПО ОСТАТКАМ", fontTitle, XBrushes.DarkBlue, left, y);
                y += 25;
                gfx.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", fontNormal, XBrushes.Black, left, y);
                y += 20;
                gfx.DrawString($"Всего товаров: {StockReport.Count}", fontNormal, XBrushes.Black, left, y);
                y += 15;
                gfx.DrawString($"Отсутствуют: {StockReport.Count(s => s.CurrentStock == 0)}", fontNormal, XBrushes.Red, left, y);
                y += 15;
                gfx.DrawString($"Ниже минимума: {StockReport.Count(s => s.Status == "Ниже минимума")}", fontNormal, XBrushes.Orange, left, y);
                y += 20;

                gfx.DrawString("Товар", fontHeader, XBrushes.Black, left, y);
                gfx.DrawString("Категория", fontHeader, XBrushes.Black, left + 180, y);
                gfx.DrawString("Остаток", fontHeader, XBrushes.Black, left + 280, y);
                gfx.DrawString("Статус", fontHeader, XBrushes.Black, left + 380, y);
                y += 15;

                foreach (var item in StockReport.OrderBy(s => s.Status).ThenBy(s => s.ProductName).Take(100))
                {
                    if (y > page.Height.Point - 40)
                    {
                        page = document.AddPage();
                        gfx.Dispose();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 30;
                    }
                    XBrush statusBrush = item.Status == "Норма" ? XBrushes.Green : item.Status == "Ниже минимума" ? XBrushes.Red : XBrushes.Orange;
                    gfx.DrawString(TruncateString(item.ProductName, 30), fontNormal, XBrushes.Black, left, y);
                    gfx.DrawString(TruncateString(item.Category, 20), fontNormal, XBrushes.Black, left + 180, y);
                    gfx.DrawString(item.CurrentStock.ToString(), fontNormal, XBrushes.Black, left + 280, y);
                    gfx.DrawString(item.Status, fontNormal, statusBrush, left + 380, y);
                    y += 15;
                }
                gfx.Dispose();
                document.Save(filename);
            }
        }

        private void ExportStockReportCsv(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Отчет_по_остаткам_{DateTime.Now:yyyyMMddHHmmss}.csv" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
            {
                writer.WriteLine("ID;Наименование;Категория;Остаток;Мин.остаток;Статус;Цена;Стоимость");
                foreach (var s in StockReport)
                    writer.WriteLine($"{s.ProductId};{EscapeCsv(s.ProductName)};{EscapeCsv(s.Category)};{s.CurrentStock};{s.MinStockLevel};{EscapeCsv(s.Status)};{s.UnitPrice:F2};{s.TotalValue:F2}");
            }
            OpenFile(dialog.FileName);
        }

        private void ExportStockReportExcel(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "Excel files (*.xls)|*.xls", FileName = $"Отчет_по_остаткам_{DateTime.Now:yyyyMMddHHmmss}.xls" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.Unicode))
            {
                writer.WriteLine("ID\tНаименование\tКатегория\tОстаток\tМин.остаток\tСтатус\tЦена\tСтоимость");
                foreach (var s in StockReport)
                    writer.WriteLine($"{s.ProductId}\t{s.ProductName}\t{s.Category}\t{s.CurrentStock}\t{s.MinStockLevel}\t{s.Status}\t{s.UnitPrice:F2}\t{s.TotalValue:F2}");
            }
            OpenFile(dialog.FileName);
        }

        // ========== ОТЧЁТ ПО ВОЗВРАТАМ ==========
        private void GenerateReturnReport(object parameter)
        {
            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    var query = from r in ctx.ProductReturns
                                join u in ctx.ProductUnits on r.UnitID equals u.UnitID
                                join p in ctx.Products on r.ProductID equals p.ProductID
                                join e in ctx.Employees on r.ProcessedByEmployeeID equals e.EmployeeID into empJoin
                                from emp in empJoin.DefaultIfEmpty()
                                where r.ReturnDate >= ReturnStartDate && r.ReturnDate <= ReturnEndDate
                                select new ReturnReportItem
                                {
                                    ReturnID = r.ReturnID,
                                    UnitID = r.UnitID,
                                    ProductName = p.ProductName,
                                    ReturnDate = r.ReturnDate,
                                    ReturnReason = r.ReturnReason,
                                    IsWarrantyCase = r.IsWarrantyCase,
                                    RefundAmount = r.RefundAmount,
                                    Status = u.Status,
                                    ProcessedBy = emp != null ? $"{emp.LastName} {emp.FirstName}" : "Система"
                                };
                    var list = query.ToList();
                    if (ReturnStatusFilter != "Все") list = list.Where(x => x.Status == ReturnStatusFilter).ToList();
                    if (IsWarrantyFilter.HasValue) list = list.Where(x => x.IsWarrantyCase == IsWarrantyFilter.Value).ToList();
                    ReturnReport = new ObservableCollection<ReturnReportItem>(list.OrderByDescending(x => x.ReturnDate));
                    StatusMessage = $"Отчет по возвратам сгенерирован. Записей: {ReturnReport.Count}";
                    StatusColor = "#27ae60";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка генерации отчета: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }

        private void ExportReturnReportPdf(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = $"Отчет_по_возвратам_{DateTime.Now:yyyyMMddHHmmss}.pdf" };
            if (dialog.ShowDialog() != true) return;
            CreateReturnReportPdf(dialog.FileName);
            OpenFile(dialog.FileName);
        }
        public ICommand ExportProductCatalogPdfCommand { get; }
        public ICommand ExportProductCatalogCsvCommand { get; }
        public ICommand ExportProductCatalogExcelCommand { get; }
        private void CreateReturnReportPdf(string filename)
        {
            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Отчет по возвратам за {ReturnStartDate:dd.MM.yyyy} - {ReturnEndDate:dd.MM.yyyy}";
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
                var fontNormal = new XFont("Arial", 9, XFontStyleEx.Regular);
                double y = 30, left = 40;

                gfx.DrawString("ОТЧЕТ ПО ВОЗВРАТАМ", fontTitle, XBrushes.DarkBlue, left, y);
                y += 20;
                gfx.DrawString($"Период: {ReturnStartDate:dd.MM.yyyy} - {ReturnEndDate:dd.MM.yyyy}", fontNormal, XBrushes.Black, left, y);
                y += 20;
                gfx.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", fontNormal, XBrushes.Black, left, y);
                y += 25;

                gfx.DrawString("ID", fontHeader, XBrushes.Black, left, y);
                gfx.DrawString("Товар", fontHeader, XBrushes.Black, left + 50, y);
                gfx.DrawString("Дата", fontHeader, XBrushes.Black, left + 200, y);
                gfx.DrawString("Причина", fontHeader, XBrushes.Black, left + 300, y);
                gfx.DrawString("Сумма", fontHeader, XBrushes.Black, left + 450, y);
                y += 15;

                foreach (var item in ReturnReport.Take(50))
                {
                    if (y > page.Height.Point - 40)
                    {
                        page = document.AddPage();
                        gfx.Dispose();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 30;
                    }
                    gfx.DrawString(item.ReturnID.ToString(), fontNormal, XBrushes.Black, left, y);
                    gfx.DrawString(TruncateString(item.ProductName, 25), fontNormal, XBrushes.Black, left + 50, y);
                    gfx.DrawString(item.ReturnDate.ToString("dd.MM.yyyy"), fontNormal, XBrushes.Black, left + 200, y);
                    gfx.DrawString(TruncateString(item.ReturnReason, 30), fontNormal, XBrushes.Black, left + 300, y);
                    gfx.DrawString($"{item.RefundAmount:F2} ₽", fontNormal, XBrushes.Black, left + 450, y);
                    y += 15;
                }
                gfx.Dispose();
                document.Save(filename);
            }
        }

        private void ExportReturnReportCsv(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Отчет_по_возвратам_{DateTime.Now:yyyyMMddHHmmss}.csv" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
            {
                writer.WriteLine("ID;Товар;Дата;Причина;Гарантия;Сумма;Статус;Обработал");
                foreach (var r in ReturnReport)
                    writer.WriteLine($"{r.ReturnID};{EscapeCsv(r.ProductName)};{r.ReturnDate:dd.MM.yyyy};{EscapeCsv(r.ReturnReason)};{(r.IsWarrantyCase ? "Да" : "Нет")};{r.RefundAmount:F2};{r.Status};{EscapeCsv(r.ProcessedBy)}");
            }
            OpenFile(dialog.FileName);
        }

        private void ExportReturnReportExcel(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "Excel files (*.xls)|*.xls", FileName = $"Отчет_по_возвратам_{DateTime.Now:yyyyMMddHHmmss}.xls" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.Unicode))
            {
                writer.WriteLine("ID\tТовар\tДата\tПричина\tГарантия\tСумма\tСтатус\tОбработал");
                foreach (var r in ReturnReport)
                    writer.WriteLine($"{r.ReturnID}\t{r.ProductName}\t{r.ReturnDate:dd.MM.yyyy}\t{r.ReturnReason}\t{(r.IsWarrantyCase ? "Да" : "Нет")}\t{r.RefundAmount:F2}\t{r.Status}\t{r.ProcessedBy}");
            }
            OpenFile(dialog.FileName);
        }

        // ========== ОТЧЁТ ПО НАКЛАДНЫМ ТОРГ-12 ==========
        private void GenerateTorg12Report(object parameter)
        {
            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    var docs = ctx.Torg12Documents
                        .Include(d => d.Employees)
                        .Include(d => d.Torg12Items)
                        .Where(d => d.DocumentDate >= Torg12StartDate && d.DocumentDate <= Torg12EndDate)
                        .ToList();
                    if (!string.IsNullOrWhiteSpace(Torg12ReceiverFilter))
                        docs = docs.Where(d => d.ReceiverName.Contains(Torg12ReceiverFilter)).ToList();
                    if (Torg12StatusFilter != "Все")
                        docs = docs.Where(d => d.Status == Torg12StatusFilter).ToList();

                    var report = docs.Select(d => new Torg12ReportItem
                    {
                        Torg12ID = d.Torg12ID,
                        DocumentNumber = d.DocumentNumber,
                        DocumentDate = d.DocumentDate,
                        ReceiverName = d.ReceiverName,
                        CreatedBy = d.Employees != null ? $"{d.Employees.LastName} {d.Employees.FirstName}" : "Неизвестно",
                        CreatedDate = d.CreatedDate,
                        Status = d.Status,
                        ItemsCount = d.Torg12Items.Count,
                        TotalAmount = d.Torg12Items.Sum(i => i.Quantity * i.UnitPrice),
                        Notes = d.Notes
                    }).OrderByDescending(d => d.DocumentDate).ToList();

                    Torg12Report = new ObservableCollection<Torg12ReportItem>(report);
                    StatusMessage = $"Отчет по ТОРГ-12 сгенерирован. Документов: {Torg12Report.Count}";
                    StatusColor = "#27ae60";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка генерации отчета: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }

        private void ExportTorg12ReportPdf(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = $"Отчет_ТОРГ12_{DateTime.Now:yyyyMMddHHmmss}.pdf" };
            if (dialog.ShowDialog() != true) return;
            CreateTorg12ReportPdf(dialog.FileName);
            OpenFile(dialog.FileName);
        }

        private void CreateTorg12ReportPdf(string filename)
        {
            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Отчет по ТОРГ-12 за {Torg12StartDate:dd.MM.yyyy} - {Torg12EndDate:dd.MM.yyyy}";
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
                var fontNormal = new XFont("Arial", 9, XFontStyleEx.Regular);
                double y = 30, left = 40;

                gfx.DrawString("ОТЧЕТ ПО ТОРГ-12", fontTitle, XBrushes.DarkBlue, left, y);
                y += 20;
                gfx.DrawString($"Период: {Torg12StartDate:dd.MM.yyyy} - {Torg12EndDate:dd.MM.yyyy}", fontNormal, XBrushes.Black, left, y);
                y += 20;
                gfx.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", fontNormal, XBrushes.Black, left, y);
                y += 25;

                gfx.DrawString("№ документа", fontHeader, XBrushes.Black, left, y);
                gfx.DrawString("Дата", fontHeader, XBrushes.Black, left + 120, y);
                gfx.DrawString("Получатель", fontHeader, XBrushes.Black, left + 200, y);
                gfx.DrawString("Сумма", fontHeader, XBrushes.Black, left + 400, y);
                gfx.DrawString("Статус", fontHeader, XBrushes.Black, left + 480, y);
                y += 15;

                foreach (var item in Torg12Report.Take(50))
                {
                    if (y > page.Height.Point - 40)
                    {
                        page = document.AddPage();
                        gfx.Dispose();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 30;
                    }
                    gfx.DrawString(item.DocumentNumber, fontNormal, XBrushes.Black, left, y);
                    gfx.DrawString(item.DocumentDate.ToString("dd.MM.yyyy"), fontNormal, XBrushes.Black, left + 120, y);
                    gfx.DrawString(TruncateString(item.ReceiverName, 30), fontNormal, XBrushes.Black, left + 200, y);
                    gfx.DrawString($"{item.TotalAmount:F2} ₽", fontNormal, XBrushes.Black, left + 400, y);
                    gfx.DrawString(item.Status, fontNormal, XBrushes.Black, left + 480, y);
                    y += 15;
                }
                gfx.Dispose();
                document.Save(filename);
            }
        }

        private void ExportTorg12ReportCsv(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Отчет_ТОРГ12_{DateTime.Now:yyyyMMddHHmmss}.csv" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
            {
                writer.WriteLine("Номер;Дата;Получатель;Создал;Позиций;Сумма;Статус");
                foreach (var t in Torg12Report)
                    writer.WriteLine($"{EscapeCsv(t.DocumentNumber)};{t.DocumentDate:dd.MM.yyyy};{EscapeCsv(t.ReceiverName)};{EscapeCsv(t.CreatedBy)};{t.ItemsCount};{t.TotalAmount:F2};{t.Status}");
            }
            OpenFile(dialog.FileName);
        }

        private void ExportTorg12ReportExcel(object parameter)
        {
            var dialog = new SaveFileDialog { Filter = "Excel files (*.xls)|*.xls", FileName = $"Отчет_ТОРГ12_{DateTime.Now:yyyyMMddHHmmss}.xls" };
            if (dialog.ShowDialog() != true) return;
            using (var writer = new StreamWriter(dialog.FileName, false, Encoding.Unicode))
            {
                writer.WriteLine("Номер\tДата\tПолучатель\tСоздал\tПозиций\tСумма\tСтатус");
                foreach (var t in Torg12Report)
                    writer.WriteLine($"{t.DocumentNumber}\t{t.DocumentDate:dd.MM.yyyy}\t{t.ReceiverName}\t{t.CreatedBy}\t{t.ItemsCount}\t{t.TotalAmount:F2}\t{t.Status}");
            }
            OpenFile(dialog.FileName);
        }

        // ========== ИСТОРИЯ ТОВАРА ==========
        private void LoadProductHistory()
        {
            if (SelectedProductForHistory == null) return;
            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    int pid = SelectedProductForHistory.ProductID;
                    var history = new ObservableCollection<ProductHistoryItem>();
                    foreach (var m in ctx.ProductMovementHistory.Where(m => m.ProductID == pid).ToList())
                        history.Add(new ProductHistoryItem
                        {
                            EventDate = m.MovementDate,
                            EventType = "Движение",
                            Description = $"{m.MovementType}: {m.Quantity} шт., {m.SourceDocumentType} #{m.SourceDocumentID}",
                            Quantity = m.Quantity
                        });
                    foreach (var pc in ctx.ProductPriceHistory.Where(p => p.ProductID == pid).ToList())
                        history.Add(new ProductHistoryItem
                        {
                            EventDate = pc.ChangedAt,
                            EventType = "Изменение цены",
                            Description = $"с {pc.OldPrice:F2} ₽ на {pc.NewPrice:F2} ₽",
                            Price = pc.NewPrice,
                            SourceDocument = pc.Source
                        });
                    var returns = from r in ctx.ProductReturns
                                  where r.ProductID == pid
                                  join u in ctx.ProductUnits on r.UnitID equals u.UnitID
                                  select new { r, u };
                    foreach (var ret in returns.ToList())
                        history.Add(new ProductHistoryItem
                        {
                            EventDate = ret.r.ReturnDate,
                            EventType = "Возврат",
                            Description = $"Unit #{ret.u.UnitID}, причина: {ret.r.ReturnReason}, сумма {ret.r.RefundAmount:F2} ₽",
                            Quantity = 1,
                            Price = ret.r.RefundAmount
                        });
                    ProductHistory = new ObservableCollection<ProductHistoryItem>(history.OrderByDescending(h => h.EventDate));
                    OnPropertyChanged(nameof(ProductHistory));
                    StatusMessage = $"История товара '{SelectedProductForHistory.ProductName}' загружена. Событий: {ProductHistory.Count}";
                    StatusColor = "#27ae60";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки истории: {ex.Message}";
                StatusColor = "#e74c3c";
            }
        }
        private void GenerateProductHistory(object parameter) => LoadProductHistory();

        private ObservableCollection<Products> _exportPreviewProducts;

        public ObservableCollection<Products> ExportPreviewProducts
        {
            get => _exportPreviewProducts;
            set => Set(ref _exportPreviewProducts, value);
        }
        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
        private void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { MessageBox.Show($"Файл сохранен: {path}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= maxLength ? s : s.Substring(0, maxLength - 3) + "...";
        }

        private string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(";") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public void DisposeContext() => _context?.Dispose();
    }
}