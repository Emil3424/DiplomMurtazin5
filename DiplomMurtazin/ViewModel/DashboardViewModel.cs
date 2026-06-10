using DiplomMurtazin.Core;
using DiplomMurtazin.View;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Excel = Microsoft.Office.Interop.Excel;

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
        private string _sortCashiersBy = "Выручка"; // "Выручка", "Продажи", "Имя"
        private string _sortLastSalesBy = "Дата";   // "Дата", "Сумма"
        private bool _sortLastSalesDescending = true;

        public string SortCashiersBy
        {
            get => _sortCashiersBy;
            set
            {
                if (Set(ref _sortCashiersBy, value))
                    ApplySorting();
            }
        }

        public string SortLastSalesBy
        {
            get => _sortLastSalesBy;
            set
            {
                if (Set(ref _sortLastSalesBy, value))
                    ApplySorting();
            }
        }

        public bool SortLastSalesDescending
        {
            get => _sortLastSalesDescending;
            set
            {
                if (Set(ref _sortLastSalesDescending, value))
                    ApplySorting();
            }
        }

        // Команды экспорта
        public ICommand ExportDashboardToPdfCommand { get; }
        public ICommand ExportDashboardToCsvCommand { get; }
        public ICommand ExportDashboardToExcelCommand { get; }

        // Команды для сброса сортировки/фильтров (опционально)
        public ICommand ResetSortingCommand { get; }
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
            ExportDashboardToPdfCommand = new RelayCommand(_ => ExportDashboardToPdf());
            ExportDashboardToCsvCommand = new RelayCommand(_ => ExportDashboardToCsv());
            ExportDashboardToExcelCommand = new RelayCommand(_ => ExportDashboardToExcel());
            ResetSortingCommand = new RelayCommand(_ => ResetSorting());

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            LoadCashiers();
            LoadCategories();
            LoadData();
        }
        private void ApplySorting()
        {
            if (CashierStats != null)
            {
                var sorted = SortCashiersBy switch
                {
                    "Выручка" => CashierStats.OrderByDescending(c => c.TotalAmount),
                    "Продажи" => CashierStats.OrderByDescending(c => c.SalesCount),
                    _ => CashierStats.OrderBy(c => c.EmployeeName)
                };
                CashierStats = new ObservableCollection<CashierStat>(sorted);
            }

            if (LastSales != null)
            {
                var sorted = SortLastSalesBy switch
                {
                    "Сумма" => SortLastSalesDescending
                        ? LastSales.OrderByDescending(s => s.TotalAmount)
                        : LastSales.OrderBy(s => s.TotalAmount),
                    _ => SortLastSalesDescending
                        ? LastSales.OrderByDescending(s => s.SaleDateTime)
                        : LastSales.OrderBy(s => s.SaleDateTime)
                };
                LastSales = new ObservableCollection<Sales>(sorted);
            }
        }

        private void ResetSorting()
        {
            SortCashiersBy = "Выручка";
            SortLastSalesBy = "Дата";
            SortLastSalesDescending = true;
            ApplySorting();
        }

        private BitmapSource CaptureChartImage()
        {
            try
            {
                // Ищем элемент CartesianChart на странице (можно передать ссылку через событие, но проще сохранить в статике)
                // Поскольку ViewModel не имеет прямого доступа к визуальным элементам, используем хак: 
                // находим активное окно и ищем нужный контрол по имени.
                // Альтернатива: передать FrameworkElement через параметр команды. Для простоты сделаем через Application.Current.Windows.
                var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow) as MainWindow;
                if (mainWindow == null) return null;
                var frame = mainWindow.MainFrame;
                if (frame == null) return null;
                var dashboardPage = frame.Content as DashboardPage;
                if (dashboardPage == null) return null;

                // Ищем CartesianChart по имени (добавим x:Name="SalesChart" в XAML)
                var chart = dashboardPage.FindName("SalesChart") as LiveCharts.Wpf.CartesianChart;
                if (chart == null) return null;

                // Рендерим контрол в изображение
                var renderTarget = new RenderTargetBitmap((int)chart.ActualWidth, (int)chart.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(chart);
                return renderTarget;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка захвата графика: {ex.Message}");
                return null;
            }
        }

        // В ExportDashboardToPdf() добавляем вставку изображения графика и итоговые строки, а также разрывы страниц.
        private void ExportDashboardToPdf()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Dashboard_{DateTime.Now:yyyyMMddHHmmss}.pdf",
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog() != true) return;

            using (var document = new PdfDocument())
            {
                document.Info.Title = $"Панель управления за {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";
                document.Info.Creator = "KPMurtazin";

                var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                var fontHeader = new XFont("Arial", 10, XFontStyleEx.Bold);
                var fontNormal = new XFont("Arial", 9, XFontStyleEx.Regular);
                double leftMargin = 40;

                // --- СТРАНИЦА 1: Основные показатели и график ---
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                var gfx = XGraphics.FromPdfPage(page);
                double yPos = 30;

                gfx.DrawString("ОТЧЕТ ДАШБОРД", fontTitle, XBrushes.DarkBlue, leftMargin, yPos);
                yPos += 20;
                gfx.DrawString($"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 25;

                gfx.DrawString("ОСНОВНЫЕ ПОКАЗАТЕЛИ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Общая выручка: {TotalRevenue:F2} ₽", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Количество продаж: {SalesCount}", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Всего товаров: {ProductsCount}", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Мало товара: {LowStockCount}", fontNormal, XBrushes.Black, leftMargin, yPos);
                yPos += 25;

                // Вставка изображения графика
                var chartImage = CaptureChartImage();
                if (chartImage != null)
                {
                    // Сохраняем BitmapSource во временный файл, затем вставляем
                    string tempFile = Path.GetTempFileName() + ".png";
                    using (var stream = new FileStream(tempFile, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(chartImage));
                        encoder.Save(stream);
                    }
                    var xImage = XImage.FromFile(tempFile);
                    double imgWidth = page.Width.Point - 80;
                    double imgHeight = xImage.PixelHeight * (imgWidth / xImage.PixelWidth);
                    gfx.DrawImage(xImage, leftMargin, yPos, imgWidth, imgHeight);
                    yPos += imgHeight + 20;
                    File.Delete(tempFile);
                }
                else
                {
                    gfx.DrawString("(График не доступен)", fontNormal, XBrushes.Gray, leftMargin, yPos);
                    yPos += 20;
                }

                gfx.Dispose();

                // --- СТРАНИЦА 2: Статистика по кассирам, Топ-10, Низкие остатки ---
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                yPos = 30;

                if (CashierStats != null && CashierStats.Any())
                {
                    gfx.DrawString("СТАТИСТИКА КАССИРОВ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 15;
                    gfx.DrawString("Кассир", fontHeader, XBrushes.Black, leftMargin, yPos);
                    gfx.DrawString("Продаж", fontHeader, XBrushes.Black, leftMargin + 150, yPos);
                    gfx.DrawString("Выручка", fontHeader, XBrushes.Black, leftMargin + 230, yPos);
                    gfx.DrawString("Ср. чек", fontHeader, XBrushes.Black, leftMargin + 320, yPos);
                    yPos += 15;

                    decimal totalRevenue = 0;
                    int totalSales = 0;
                    foreach (var c in CashierStats)
                    {
                        if (yPos > page.Height.Point - 50)
                        {
                            page = document.AddPage();
                            gfx.Dispose();
                            gfx = XGraphics.FromPdfPage(page);
                            yPos = 30;
                        }
                        gfx.DrawString(c.EmployeeName, fontNormal, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString(c.SalesCount.ToString(), fontNormal, XBrushes.Black, leftMargin + 150, yPos);
                        gfx.DrawString($"{c.TotalAmount:F2} ₽", fontNormal, XBrushes.Black, leftMargin + 230, yPos);
                        gfx.DrawString($"{c.AverageCheck:F2} ₽", fontNormal, XBrushes.Black, leftMargin + 320, yPos);
                        totalRevenue += c.TotalAmount;
                        totalSales += c.SalesCount;
                        yPos += 15;
                    }
                    yPos += 5;
                    gfx.DrawString($"ИТОГО: {totalRevenue:F2} ₽ (всего продаж: {totalSales})", fontNormal, XBrushes.Black, leftMargin, yPos);
                    yPos += 25;
                }

                // Топ-10 товаров
                if (TopProducts != null && TopProducts.Any())
                {
                    if (yPos > page.Height.Point - 150) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); yPos = 30; }
                    gfx.DrawString("ТОП-10 ТОВАРОВ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 15;
                    gfx.DrawString("Товар", fontHeader, XBrushes.Black, leftMargin, yPos);
                    gfx.DrawString("Продано, шт", fontHeader, XBrushes.Black, leftMargin + 250, yPos);
                    yPos += 15;
                    int totalQuantity = 0;
                    foreach (var p in TopProducts.Take(10))
                    {
                        if (yPos > page.Height.Point - 30) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); yPos = 30; }
                        gfx.DrawString(p.ProductName, fontNormal, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString(p.TotalQuantity.ToString(), fontNormal, XBrushes.Black, leftMargin + 250, yPos);
                        totalQuantity += p.TotalQuantity;
                        yPos += 12;
                    }
                    yPos += 5;
                    gfx.DrawString($"ИТОГО продано: {totalQuantity} шт.", fontNormal, XBrushes.Black, leftMargin, yPos);
                    yPos += 25;
                }

                // Низкие остатки
                if (LowStockItems != null && LowStockItems.Any())
                {
                    if (yPos > page.Height.Point - 150) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); yPos = 30; }
                    gfx.DrawString("НИЗКИЕ ОСТАТКИ И ПРОГНОЗ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 15;
                    gfx.DrawString("Товар", fontHeader, XBrushes.Black, leftMargin, yPos);
                    gfx.DrawString("Остаток", fontHeader, XBrushes.Black, leftMargin + 200, yPos);
                    gfx.DrawString("Мин. остаток", fontHeader, XBrushes.Black, leftMargin + 280, yPos);
                    gfx.DrawString("Прогноз, дней", fontHeader, XBrushes.Black, leftMargin + 380, yPos);
                    yPos += 15;
                    foreach (var item in LowStockItems)
                    {
                        if (yPos > page.Height.Point - 30) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); yPos = 30; }
                        gfx.DrawString(item.ProductName, fontNormal, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString(item.CurrentStock.ToString(), fontNormal, XBrushes.Black, leftMargin + 200, yPos);
                        gfx.DrawString(item.MinStockLevel.ToString(), fontNormal, XBrushes.Black, leftMargin + 280, yPos);
                        gfx.DrawString(item.DaysUntilOutDisplay, fontNormal, XBrushes.Black, leftMargin + 380, yPos);
                        yPos += 12;
                    }
                    yPos += 25;
                }

                gfx.Dispose();

                // --- СТРАНИЦА 3: Последние продажи (с итогами) ---
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                yPos = 30;

                if (LastSales != null && LastSales.Any())
                {
                    gfx.DrawString("ПОСЛЕДНИЕ ПРОДАЖИ", fontHeader, XBrushes.DarkBlue, leftMargin, yPos);
                    yPos += 15;
                    gfx.DrawString("ID", fontHeader, XBrushes.Black, leftMargin, yPos);
                    gfx.DrawString("Дата", fontHeader, XBrushes.Black, leftMargin + 50, yPos);
                    gfx.DrawString("Сумма", fontHeader, XBrushes.Black, leftMargin + 180, yPos);
                    gfx.DrawString("Оплата", fontHeader, XBrushes.Black, leftMargin + 260, yPos);
                    yPos += 15;
                    decimal totalSalesAmount = 0;
                    foreach (var sale in LastSales.Take(20))
                    {
                        if (yPos > page.Height.Point - 30) { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); yPos = 30; }
                        gfx.DrawString(sale.SaleID.ToString(), fontNormal, XBrushes.Black, leftMargin, yPos);
                        gfx.DrawString(sale.SaleDateTime.ToString("dd.MM.yy HH:mm"), fontNormal, XBrushes.Black, leftMargin + 50, yPos);
                        gfx.DrawString($"{sale.TotalAmount:F2} ₽", fontNormal, XBrushes.Black, leftMargin + 180, yPos);
                        gfx.DrawString(sale.PaymentMethod, fontNormal, XBrushes.Black, leftMargin + 260, yPos);
                        totalSalesAmount += sale.TotalAmount;
                        yPos += 12;
                    }
                    yPos += 5;
                    gfx.DrawString($"ИТОГО по последним {LastSales.Count} продажам: {totalSalesAmount:F2} ₽", fontNormal, XBrushes.Black, leftMargin, yPos);
                }
                gfx.Dispose();

                document.Save(dialog.FileName);
            }

            // Автоматически открыть файл
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            MessageBox.Show($"Дашборд экспортирован в PDF: {dialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportDashboardToCsv()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Dashboard_{DateTime.Now:yyyyMMddHHmmss}.csv",
                DefaultExt = ".csv"
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();

            sb.AppendLine($"Дашборд за период {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}");
            sb.AppendLine($"Дата выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine();

            // Основные показатели
            sb.AppendLine("ОСНОВНЫЕ ПОКАЗАТЕЛИ");
            sb.AppendLine($"Общая выручка;{TotalRevenue:F2} ₽");
            sb.AppendLine($"Количество продаж;{SalesCount}");
            sb.AppendLine($"Всего товаров;{ProductsCount}");
            sb.AppendLine($"Мало товара;{LowStockCount}");
            sb.AppendLine();

            // График продаж по дням (данные)
            sb.AppendLine("ПРОДАЖИ ПО ДНЯМ");
            sb.AppendLine("Дата;Сумма, ₽");
            for (int i = 0; i < SalesLabels.Length; i++)
            {
                var amount = ((LineSeries)SalesSeries[0]).Values[i];
                sb.AppendLine($"{SalesLabels[i]};{amount:N2}");
            }
            sb.AppendLine($"ИТОГО ЗА ПЕРИОД;{TotalRevenue:F2} ₽");
            sb.AppendLine();

            // Статистика кассиров
            sb.AppendLine("СТАТИСТИКА КАССИРОВ");
            sb.AppendLine("Кассир;Продаж;Выручка, ₽;Средний чек, ₽");
            decimal cashierTotal = 0;
            foreach (var c in CashierStats)
            {
                sb.AppendLine($"{c.EmployeeName};{c.SalesCount};{c.TotalAmount:F2};{c.AverageCheck:F2}");
                cashierTotal += c.TotalAmount;
            }
            sb.AppendLine($"ИТОГО ПО КАССИРАМ;;{cashierTotal:F2} ₽;");
            sb.AppendLine();

            // Топ-10 товаров
            sb.AppendLine("ТОП-10 ТОВАРОВ");
            sb.AppendLine("Товар;Продано, шт");
            int totalTopQuantity = 0;
            foreach (var p in TopProducts.Take(10))
            {
                sb.AppendLine($"{p.ProductName};{p.TotalQuantity}");
                totalTopQuantity += p.TotalQuantity;
            }
            sb.AppendLine($"ИТОГО В ТОП-10;{totalTopQuantity} шт");
            sb.AppendLine();

            // Низкие остатки
            sb.AppendLine("НИЗКИЕ ОСТАТКИ И ПРОГНОЗ");
            sb.AppendLine("Товар;Остаток;Мин. остаток;Прогноз (дней)");
            foreach (var item in LowStockItems)
                sb.AppendLine($"{item.ProductName};{item.CurrentStock};{item.MinStockLevel};{item.DaysUntilOutDisplay}");
            sb.AppendLine();

            // Последние продажи
            sb.AppendLine("ПОСЛЕДНИЕ ПРОДАЖИ");
            sb.AppendLine("ID продажи;Дата;Сумма, ₽;Способ оплаты");
            decimal lastSalesTotal = 0;
            foreach (var sale in LastSales)
            {
                sb.AppendLine($"{sale.SaleID};{sale.SaleDateTime:dd.MM.yyyy HH:mm};{sale.TotalAmount:F2};{sale.PaymentMethod}");
                lastSalesTotal += sale.TotalAmount;
            }
            sb.AppendLine($"ИТОГО ПО {LastSales.Count} ПРОДАЖАМ;;{lastSalesTotal:F2} ₽;");

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            MessageBox.Show($"Дашборд экспортирован в CSV: {dialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public List<DateTime> SalesDates { get; set; }
        private void ExportDashboardToExcel()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = $"Dashboard_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                DefaultExt = ".xlsx"
            };
            if (dialog.ShowDialog() != true) return;

            Excel.Application excel = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;
            try
            {
                excel = new Excel.Application();
                excel.Visible = false;
                workbook = excel.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Worksheets[1];
                worksheet.Name = "Dashboard";

                int row = 1;
                // Заголовок
                worksheet.Cells[row, 1] = $"Дашборд за период {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";
                row++;
                worksheet.Cells[row, 1] = $"Дата выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm}";
                row += 2;

                // Основные показатели
                worksheet.Cells[row, 1] = "ОСНОВНЫЕ ПОКАЗАТЕЛИ";
                row++;
                worksheet.Cells[row, 1] = "Общая выручка";
                worksheet.Cells[row, 2] = $"{TotalRevenue:F2} ₽";
                row++;
                worksheet.Cells[row, 1] = "Количество продаж";
                worksheet.Cells[row, 2] = SalesCount;
                row++;
                worksheet.Cells[row, 1] = "Всего товаров";
                worksheet.Cells[row, 2] = ProductsCount;
                row++;
                worksheet.Cells[row, 1] = "Мало товара";
                worksheet.Cells[row, 2] = LowStockCount;
                row += 2;

                // График продаж (данные)
                if (SalesDates != null && SalesDates.Count > 0 && SalesSeries.Count > 0)
                {
                    worksheet.Cells[row, 1] = "ПРОДАЖИ ПО ДНЯМ";
                    row++;
                    worksheet.Cells[row, 1] = "Дата";
                    worksheet.Cells[row, 2] = "Сумма, ₽";
                    row++;
                    int dataStartRow = row;
                    var series = (LineSeries)SalesSeries[0];
                    for (int i = 0; i < SalesDates.Count; i++)
                    {
                        worksheet.Cells[row, 1] = SalesDates[i];
                        ((Excel.Range)worksheet.Cells[row, 1]).NumberFormat = "dd.MM";
                        worksheet.Cells[row, 2] = series.Values[i];
                        row++;
                    }
                    worksheet.Cells[row, 1] = "ИТОГО ЗА ПЕРИОД";
                    worksheet.Cells[row, 2] = TotalRevenue;
                    row += 2;

                    // Создание графика на основе данных
                    Excel.Range chartRange = worksheet.Range[worksheet.Cells[dataStartRow, 1], worksheet.Cells[row - 3, 2]];
                    Excel.ChartObjects chartObjects = (Excel.ChartObjects)worksheet.ChartObjects();
                    Excel.ChartObject chartObject = chartObjects.Add(100, 100, 400, 250);
                    Excel.Chart chart = chartObject.Chart;
                    chart.SetSourceData(chartRange);
                    chart.ChartType = Excel.XlChartType.xlLine;
                    chart.HasTitle = true;
                    chart.ChartTitle.Text = "Продажи по дням";

                    // Переместим график правее данных
                    chartObject.Left = 450;
                    chartObject.Top = 100;
                    row += 10;
                }

                // Статистика кассиров
                worksheet.Cells[row, 1] = "СТАТИСТИКА КАССИРОВ";
                row++;
                worksheet.Cells[row, 1] = "Кассир";
                worksheet.Cells[row, 2] = "Продаж";
                worksheet.Cells[row, 3] = "Выручка, ₽";
                worksheet.Cells[row, 4] = "Средний чек, ₽";
                row++;
                foreach (var c in CashierStats)
                {
                    worksheet.Cells[row, 1] = c.EmployeeName;
                    worksheet.Cells[row, 2] = c.SalesCount;
                    worksheet.Cells[row, 3] = c.TotalAmount;
                    worksheet.Cells[row, 4] = c.AverageCheck;
                    row++;
                }
                worksheet.Cells[row, 1] = "ИТОГО ПО КАССИРАМ";
                worksheet.Cells[row, 3] = CashierStats.Sum(c => c.TotalAmount);
                row += 2;

                // Топ-10 товаров
                worksheet.Cells[row, 1] = "ТОП-10 ТОВАРОВ";
                row++;
                worksheet.Cells[row, 1] = "Товар";
                worksheet.Cells[row, 2] = "Продано, шт";
                row++;
                foreach (var p in TopProducts.Take(10))
                {
                    worksheet.Cells[row, 1] = p.ProductName;
                    worksheet.Cells[row, 2] = p.TotalQuantity;
                    row++;
                }
                worksheet.Cells[row, 1] = "ИТОГО В ТОП-10";
                worksheet.Cells[row, 2] = TopProducts.Take(10).Sum(p => p.TotalQuantity);
                row += 2;

                // Низкие остатки
                worksheet.Cells[row, 1] = "НИЗКИЕ ОСТАТКИ И ПРОГНОЗ";
                row++;
                worksheet.Cells[row, 1] = "Товар";
                worksheet.Cells[row, 2] = "Остаток";
                worksheet.Cells[row, 3] = "Мин. остаток";
                worksheet.Cells[row, 4] = "Прогноз, дней";
                row++;
                foreach (var item in LowStockItems)
                {
                    worksheet.Cells[row, 1] = item.ProductName;
                    worksheet.Cells[row, 2] = item.CurrentStock;
                    worksheet.Cells[row, 3] = item.MinStockLevel;
                    worksheet.Cells[row, 4] = item.DaysUntilOutDisplay;
                    row++;
                }
                row += 2;

                // Последние продажи
                worksheet.Cells[row, 1] = "ПОСЛЕДНИЕ ПРОДАЖИ";
                row++;
                worksheet.Cells[row, 1] = "ID продажи";
                worksheet.Cells[row, 2] = "Дата";
                worksheet.Cells[row, 3] = "Сумма, ₽";
                worksheet.Cells[row, 4] = "Способ оплаты";
                row++;
                foreach (var sale in LastSales)
                {
                    worksheet.Cells[row, 1] = sale.SaleID;
                    worksheet.Cells[row, 2] = sale.SaleDateTime.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 3] = sale.TotalAmount;
                    worksheet.Cells[row, 4] = sale.PaymentMethod;
                    row++;
                }
                worksheet.Cells[row, 1] = $"ИТОГО ПО {LastSales.Count} ПРОДАЖАМ";
                worksheet.Cells[row, 3] = LastSales.Sum(s => s.TotalAmount);

                // Автоматическая подгонка ширины столбцов
                worksheet.Columns.AutoFit();

                workbook.SaveAs(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                if (workbook != null) workbook.Close(false);
                if (excel != null) excel.Quit();
                ReleaseExcelObject(worksheet);
                ReleaseExcelObject(workbook);
                ReleaseExcelObject(excel);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            MessageBox.Show($"Дашборд экспортирован в Excel: {dialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Вспомогательный метод для освобождения COM-объектов Excel
        private void ReleaseExcelObject(object obj)
        {
            try
            {
                if (obj != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
            }
            catch { }
            finally { obj = null; }
        }
        public ObservableCollection<string> CashierSortOptions { get; } = new ObservableCollection<string> { "Выручка", "Продажи", "Имя" };
        public ObservableCollection<string> LastSalesSortOptions { get; } = new ObservableCollection<string> { "Дата", "Сумма" };
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
                    SalesDates = salesByDay.Select(x => x.Date).ToList();
                    SalesLabels = salesByDay.Select(x => x.Date.ToString("dd.MM")).ToArray();
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