using DiplomMurtazin.Core;
using DiplomMurtazin.Model;
using DiplomMurtazin.View;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    // Расширенная модель товара с остатком для отображения в гриде
    public class ProductWithStock : BaseViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public decimal UnitPrice { get; set; }
        public int? WarrantyMonths { get; set; }
        public int? MinStockLevel { get; set; }
        public string Description { get; set; }
        public int? ReturnDays { get; set; }
        public int CategoryID { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value);
        }

        // Конвертация из Products
        public static ProductWithStock FromProduct(Products product, int stockQuantity)
        {
            return new ProductWithStock
            {
                ProductID = product.ProductID,
                ProductName = product.ProductName,
                Barcode = product.Barcode,
                UnitPrice = product.UnitPrice,
                WarrantyMonths = product.WarrantyMonths,
                MinStockLevel = product.MinStockLevel,
                Description = product.Description,
                ReturnDays = product.ReturnDays,
                CategoryID = product.CategoryID,
                Quantity = stockQuantity
            };
        }
    }

    public class MissingImportItem : BaseViewModel
    {
        public int MissingID { get; set; }
        public int Torg12ID { get; set; }
        public string TempProductName { get; set; }
        public string TempBarcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Status { get; set; }
    }

    public class Torg12LineItem : BaseViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int AvailableStock { get; set; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (Set(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public decimal UnitPrice { get; set; }
        public decimal Total => UnitPrice * Quantity;
    }

    // Класс для хранения снапшота импорта (для отката)
    public class ImportSnapshot
    {
        public int Torg12Id { get; set; }
        public List<int> CreatedProductIds { get; set; } = new List<int>();
        public Dictionary<int, int> StockChanges { get; set; } = new Dictionary<int, int>();
        public List<int> AddedItemIds { get; set; } = new List<int>();
        public string OldDocumentNumber { get; set; }
        public string OldReceiverName { get; set; }
        public string OldReceiverAddress { get; set; }
        public string OldBasis { get; set; }
        public DateTime OldDocumentDate { get; set; }
    }

    public class Torg12ViewModel : BaseViewModel
    {
        private ObservableCollection<ProductWithStock> _allProducts = new ObservableCollection<ProductWithStock>();
        private ObservableCollection<ProductWithStock> _filteredProducts = new ObservableCollection<ProductWithStock>();
        private ProductWithStock _selectedProduct;
        private string _searchText;

        private ObservableCollection<Torg12LineItem> _items = new ObservableCollection<Torg12LineItem>();
        private Torg12LineItem _selectedItem;

        private string _documentNumber;
        private DateTime _documentDate = DateTime.Today;
        private string _receiverName;
        private string _receiverAddress;
        private string _basis;

        private string _statusMessage = "Готово";
        private string _statusColor = "#3498db";

        private ObservableCollection<MissingImportItem> _missingItems = new ObservableCollection<MissingImportItem>();
        private MissingImportItem _selectedMissingItem;

        // Снапшот для отката импорта
        private ImportSnapshot _lastImportSnapshot;

        public ObservableCollection<MissingImportItem> MissingItems
        {
            get => _missingItems;
            set => Set(ref _missingItems, value);
        }

        public MissingImportItem SelectedMissingItem
        {
            get => _selectedMissingItem;
            set => Set(ref _selectedMissingItem, value);
        }

        public ObservableCollection<ProductWithStock> FilteredProducts
        {
            get => _filteredProducts;
            set => Set(ref _filteredProducts, value);
        }

        public ProductWithStock SelectedProduct
        {
            get => _selectedProduct;
            set => Set(ref _selectedProduct, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        public ObservableCollection<Torg12LineItem> Items
        {
            get => _items;
            set => Set(ref _items, value);
        }

        public Torg12LineItem SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public string DocumentNumber
        {
            get => _documentNumber;
            set => Set(ref _documentNumber, value);
        }

        public DateTime DocumentDate
        {
            get => _documentDate;
            set => Set(ref _documentDate, value);
        }

        public string ReceiverName
        {
            get => _receiverName;
            set => Set(ref _receiverName, value);
        }

        public string ReceiverAddress
        {
            get => _receiverAddress;
            set => Set(ref _receiverAddress, value);
        }

        public string Basis
        {
            get => _basis;
            set => Set(ref _basis, value);
        }

        public decimal TotalAmount => Items.Sum(i => i.Total);

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => Set(ref _statusColor, value);
        }

        private bool _hasActiveImport;
        public bool HasActiveImport
        {
            get => _hasActiveImport;
            set
            {
                if (Set(ref _hasActiveImport, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand LoadedCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand ExportTorg12Command { get; }
        public ICommand ImportTorg12Command { get; }
        public ICommand CreateProductFromMissingCommand { get; }
        public ICommand CancelImportCommand { get; private set; }

        public Torg12ViewModel()
        {
            LoadedCommand = new RelayCommand(_ => LoadProducts());
            AddItemCommand = new RelayCommand(_ => AddSelectedProduct(), _ => SelectedProduct != null && SelectedProduct.Quantity > 0);
            RemoveItemCommand = new RelayCommand(_ => RemoveSelectedItem(), _ => SelectedItem != null);
            ClearCommand = new RelayCommand(_ => Clear());
            SaveDraftCommand = new RelayCommand(_ => SaveDraft(), _ => Items.Any());
            ExportTorg12Command = new RelayCommand(_ => ExportTorg12(), _ => Items.Any());
            ImportTorg12Command = new RelayCommand(_ => ImportTorg12());
            CreateProductFromMissingCommand = new RelayCommand(_ => CreateProductFromMissing(), _ => SelectedMissingItem != null);
            CancelImportCommand = new RelayCommand(_ => CancelImport(), _ => HasActiveImport);

            _lastImportSnapshot = null;
            HasActiveImport = false;

            DocumentNumber = $"ТОРГ12-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private void LoadProducts()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    // Получаем все товары с их текущими остатками
                    var products = context.Products.ToList();
                    var allWithStocks = new List<ProductWithStock>();

                    foreach (var product in products)
                    {
                        var stockQuantity = context.StockBalances
                            .Where(sb => sb.ProductID == product.ProductID)
                            .Sum(sb => (int?)sb.Quantity) ?? 0;

                        allWithStocks.Add(ProductWithStock.FromProduct(product, stockQuantity));
                    }

                    _allProducts = new ObservableCollection<ProductWithStock>(allWithStocks.OrderBy(p => p.ProductName));
                    FilteredProducts = new ObservableCollection<ProductWithStock>(_allProducts);
                }
                LoadMissingItems();
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки товаров: {ex.Message}", true);
            }
        }

        private void LoadMissingItems()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    const string sql = @"
SELECT MissingID, Torg12ID, TempProductName, TempBarcode, Quantity, UnitPrice, Status
FROM dbo.Torg12ImportMissingItems
WHERE Status = N'Pending'
ORDER BY CreatedDate ASC";
                    var list = context.Database.SqlQuery<MissingImportItem>(sql).ToList();
                    MissingItems = new ObservableCollection<MissingImportItem>(list);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredProducts = new ObservableCollection<ProductWithStock>(_allProducts);
                return;
            }

            string search = SearchText.ToLower();
            var filtered = _allProducts.Where(p =>
                (p.ProductName ?? "").ToLower().Contains(search) ||
                (p.Barcode ?? "").Contains(SearchText)).ToList();

            FilteredProducts = new ObservableCollection<ProductWithStock>(filtered);
        }

        private int GetAvailableStock(int productId)
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    return context.StockBalances.Where(sb => sb.ProductID == productId).Sum(sb => (int?)sb.Quantity) ?? 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private void AddSelectedProduct()
        {
            if (SelectedProduct == null) return;

            int stock = SelectedProduct.Quantity; // Используем уже загруженный остаток

            if (stock <= 0)
            {
                SetStatus($"Нет остатка для '{SelectedProduct.ProductName}'", true);
                return;
            }

            var existing = Items.FirstOrDefault(i => i.ProductID == SelectedProduct.ProductID);
            if (existing != null)
            {
                if (existing.Quantity + 1 > stock)
                {
                    SetStatus($"Недостаточно '{existing.ProductName}' на складе (доступно {stock})", true);
                    return;
                }

                existing.Quantity += 1;
                OnPropertyChanged(nameof(TotalAmount));
                SetStatus($"Количество увеличено: {SelectedProduct.ProductName}", false);
                return;
            }

            Items.Add(new Torg12LineItem
            {
                ProductID = SelectedProduct.ProductID,
                ProductName = SelectedProduct.ProductName,
                Barcode = SelectedProduct.Barcode,
                AvailableStock = stock,
                Quantity = 1,
                UnitPrice = SelectedProduct.UnitPrice
            });

            OnPropertyChanged(nameof(TotalAmount));
            SetStatus($"Товар добавлен: {SelectedProduct.ProductName}", false);
        }

        private void RemoveSelectedItem()
        {
            if (SelectedItem == null) return;
            Items.Remove(SelectedItem);
            OnPropertyChanged(nameof(TotalAmount));
            SetStatus("Позиция удалена", false);
        }

        private void Clear()
        {
            Items.Clear();
            ReceiverName = "";
            ReceiverAddress = "";
            Basis = "";
            DocumentNumber = $"ТОРГ12-{DateTime.Now:yyyyMMddHHmmss}";
            DocumentDate = DateTime.Today;
            OnPropertyChanged(nameof(TotalAmount));
            SetStatus("Форма очищена", false);
        }

        private void SaveDraft()
        {
            try
            {
                if (ReceiverName == null) ReceiverName = "";

                using (var context = new KPMurtazinEntities())
                {
                    const string insertDoc = @"
INSERT INTO dbo.Torg12Documents (DocumentNumber, DocumentDate, ReceiverName, ReceiverAddress, Basis, CreatedByUserID, CreatedByEmployeeID, Status, Notes)
VALUES (@num, @date, @recv, @addr, @basis, @uid, @eid, N'Draft', NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    int? uid = App.CurrentUser?.UserID;
                    int? eid = App.CurrentUser?.EmployeeID;

                    int torgId = context.Database.SqlQuery<int>(
                        insertDoc,
                        new SqlParameter("@num", DocumentNumber),
                        new SqlParameter("@date", DocumentDate),
                        new SqlParameter("@recv", string.IsNullOrWhiteSpace(ReceiverName) ? "Получатель" : ReceiverName),
                        new SqlParameter("@addr", (object)ReceiverAddress ?? DBNull.Value),
                        new SqlParameter("@basis", (object)Basis ?? DBNull.Value),
                        new SqlParameter("@uid", (object)uid ?? DBNull.Value),
                        new SqlParameter("@eid", (object)eid ?? DBNull.Value)
                    ).First();

                    foreach (var item in Items)
                    {
                        const string insertItem = @"
INSERT INTO dbo.Torg12Items (Torg12ID, ProductID, Quantity, UnitPrice)
VALUES (@tid, @pid, @qty, @price);";
                        context.Database.ExecuteSqlCommand(
                            insertItem,
                            new SqlParameter("@tid", torgId),
                            new SqlParameter("@pid", item.ProductID),
                            new SqlParameter("@qty", item.Quantity),
                            new SqlParameter("@price", item.UnitPrice)
                        );
                    }

                    AuditLogger.Log("CREATE", "TORG12", $"Создан черновик ТОРГ-12 №{DocumentNumber}", torgId.ToString(), $"Items={Items.Count}");
                }

                SetStatus("Черновик сохранен в базе", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка сохранения ТОРГ-12: {ex.Message}", true);
            }
        }

        private void ExportTorg12()
        {
            foreach (var item in Items)
            {
                int stock = GetAvailableStock(item.ProductID);
                if (item.Quantity > stock)
                {
                    SetStatus($"Недостаточно товара '{item.ProductName}' на складе. Доступно: {stock}", true);
                    return;
                }
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel files (*.xls)|*.xls",
                FileName = $"ТОРГ12_{DocumentNumber}_{DateTime.Now:yyyyMMddHHmmss}.xls",
                DefaultExt = ".xls"
            };

            if (dialog.ShowDialog() != true)
            {
                SetStatus("Экспорт отменен", false);
                return;
            }

            try
            {
                string template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL", "blanktorg12.xls");
                if (!File.Exists(template))
                {
                    template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\SQL\\blanktorg12.xls");
                }

                var rows = Items.Select(i => new Torg12ExcelRow
                {
                    ProductName = i.ProductName,
                    Barcode = i.Barcode,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList();

                Torg12ExcelInteropService.ExportFromTemplate(
                    template,
                    dialog.FileName,
                    DocumentNumber,
                    DocumentDate,
                    ReceiverName,
                    ReceiverAddress,
                    Basis,
                    rows
                );
                try
                {
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
                catch
                {
                    // Не блокируем экспорт
                }

                using (var context = new KPMurtazinEntities())
                {
                    foreach (var item in Items)
                    {
                        var stocks = context.StockBalances.Where(sb => sb.ProductID == item.ProductID).ToList();
                        int need = item.Quantity;

                        foreach (var stock in stocks.OrderBy(sb => sb.StockID))
                        {
                            if (need <= 0) break;
                            int take = Math.Min(stock.Quantity, need);
                            if (take <= 0) continue;
                            stock.Quantity -= take;
                            stock.LastUpdated = DateTime.Now;
                            need -= take;
                        }

                        context.ProductMovementHistory.Add(new ProductMovementHistory
                        {
                            ProductID = item.ProductID,
                            MovementType = "TORG12_OUT",
                            Quantity = item.Quantity,
                            SourceDocumentType = "TORG12",
                            SourceDocumentID = null,
                            MovementDate = DateTime.Now,
                            EmployeeID = App.CurrentUser?.EmployeeID
                        });
                    }
                    context.SaveChanges();
                }

                ShowTorg12Receipt();
                AuditLogger.Log("EXPORT", "TORG12", $"Экспорт ТОРГ-12 №{DocumentNumber}", metadata: $"File={Path.GetFileName(dialog.FileName)}");
                SetStatus($"ТОРГ-12 экспортирован: {Path.GetFileName(dialog.FileName)}", false);

                LoadProducts();

                // ОЧИЩАЕМ ФОРМУ
                Clear();
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка экспорта: {ex.Message}", true);
                Console.WriteLine(ex.ToString());
            }
        }

        #region Импорт с предпросмотром и отменой

        private ImportPreviewData PrepareImportPreview(string filePath)
        {
            var data = Torg12ExcelInteropService.Import(filePath);
            var preview = new ImportPreviewData
            {
                DocumentNumber = data.DocumentNumber ?? $"ТОРГ12-{DateTime.Now:yyyyMMddHHmmss}",
                DocumentDate = data.DocumentDate,
                ReceiverName = data.ReceiverName ?? "Получатель",
                ReceiverAddress = data.ReceiverAddress,
                Basis = data.Basis
            };

            using (var context = new KPMurtazinEntities())
            {
                foreach (var row in data.Rows)
                {
                    var barcode = (row.Barcode ?? "").Trim();
                    var name = (row.ProductName ?? "").Trim();

                    var product = !string.IsNullOrWhiteSpace(barcode)
                        ? context.Products.FirstOrDefault(p => p.Barcode == barcode)
                        : null;

                    if (product == null && !string.IsNullOrWhiteSpace(name))
                        product = context.Products.FirstOrDefault(p => p.ProductName == name);

                    var previewRow = new ImportPreviewRow
                    {
                        ProductName = name,
                        Barcode = barcode,
                        Quantity = row.Quantity,
                        UnitPrice = row.UnitPrice <= 0 ? (product?.UnitPrice ?? 0) : row.UnitPrice,
                        Status = product != null ? "✅ Найден" : "❌ Требуется создание",
                        StatusColor = product != null ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red
                    };
                    preview.Rows.Add(previewRow);
                }
            }
            return preview;
        }

        private void RollbackImport(ImportSnapshot snapshot)
        {
            if (snapshot == null) return;

            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    // 1. Восстанавливаем остатки товаров
                    foreach (var stockChange in snapshot.StockChanges)
                    {
                        var stock = context.StockBalances.FirstOrDefault(sb => sb.ProductID == stockChange.Key);
                        if (stock != null)
                        {
                            stock.Quantity -= stockChange.Value;
                            if (stock.Quantity < 0) stock.Quantity = 0;
                            stock.LastUpdated = DateTime.Now;
                        }
                    }

                    // 2. Удаляем добавленные позиции Torg12Items
                    foreach (var itemId in snapshot.AddedItemIds)
                    {
                        context.Database.ExecuteSqlCommand("DELETE FROM dbo.Torg12Items WHERE Torg12ItemID = @id",
                            new SqlParameter("@id", itemId));
                    }

                    // 3. Удаляем созданные товары и их связанные записи
                    foreach (var pid in snapshot.CreatedProductIds)
                    {
                        var product = context.Products.Find(pid);
                        if (product != null)
                        {
                            var stockBalances = context.StockBalances.Where(sb => sb.ProductID == pid).ToList();
                            foreach (var sb in stockBalances)
                                context.StockBalances.Remove(sb);

                            var movements = context.ProductMovementHistory.Where(pm => pm.ProductID == pid).ToList();
                            foreach (var pm in movements)
                                context.ProductMovementHistory.Remove(pm);

                            context.Products.Remove(product);
                        }
                    }

                    // 4. Удаляем документ ТОРГ-12
                    var doc = context.Torg12Documents.Find(snapshot.Torg12Id);
                    if (doc != null)
                        context.Torg12Documents.Remove(doc);

                    context.SaveChanges();
                    AuditLogger.Log("ROLLBACK", "TORG12", $"Откат импорта ТОРГ-12 ID={snapshot.Torg12Id}, " +
                        $"удалено товаров: {snapshot.CreatedProductIds.Count}, восстановлено остатков: {snapshot.StockChanges.Count}");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка отката: {ex.Message}", true);
            }
        }

        private void ImportTorg12()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx",
                Title = "Импорт ТОРГ-12"
            };
            if (dialog.ShowDialog() != true)
            {
                SetStatus("Импорт отменен", false);
                return;
            }

            try
            {
                var preview = PrepareImportPreview(dialog.FileName);
                var previewWindow = new ImportPreviewWindow(preview);
                previewWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                previewWindow.ShowDialog();

                if (!previewWindow.ImportConfirmed)
                {
                    SetStatus("Импорт отменен пользователем", false);
                    return;
                }

                var snapshot = new ImportSnapshot();
                var allProcessedRows = new List<ProcessedRow>();

                using (var context = new KPMurtazinEntities())
                {
                    const string insertDoc = @"
INSERT INTO dbo.Torg12Documents (DocumentNumber, DocumentDate, ReceiverName, ReceiverAddress, Basis, CreatedByUserID, CreatedByEmployeeID, Status, Notes)
VALUES (@num, @date, @recv, @addr, @basis, @uid, @eid, N'Draft', N'Импорт из Excel');
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    int? uid = App.CurrentUser?.UserID;
                    int? eid = App.CurrentUser?.EmployeeID;

                    snapshot.Torg12Id = context.Database.SqlQuery<int>(
                        insertDoc,
                        new SqlParameter("@num", preview.DocumentNumber),
                        new SqlParameter("@date", preview.DocumentDate),
                        new SqlParameter("@recv", preview.ReceiverName),
                        new SqlParameter("@addr", (object)preview.ReceiverAddress ?? DBNull.Value),
                        new SqlParameter("@basis", (object)preview.Basis ?? DBNull.Value),
                        new SqlParameter("@uid", (object)uid ?? DBNull.Value),
                        new SqlParameter("@eid", (object)eid ?? DBNull.Value)
                    ).First();

                    foreach (var row in preview.Rows)
                    {
                        var barcode = (row.Barcode ?? "").Trim();
                        var name = (row.ProductName ?? "").Trim();

                        var product = !string.IsNullOrWhiteSpace(barcode)
                            ? context.Products.FirstOrDefault(p => p.Barcode == barcode)
                            : null;

                        if (product == null && !string.IsNullOrWhiteSpace(name))
                            product = context.Products.FirstOrDefault(p => p.ProductName == name);

                        bool isNewProduct = false;

                        if (product == null)
                        {
                            var newProduct = ShowProductEditWindow(name, barcode, row.UnitPrice);
                            if (newProduct == null)
                            {
                                RollbackImport(snapshot);
                                SetStatus("Импорт прерван: не удалось создать товар", true);
                                return;
                            }

                            context.Products.Add(newProduct);
                            context.SaveChanges();
                            snapshot.CreatedProductIds.Add(newProduct.ProductID);
                            product = newProduct;
                            isNewProduct = true;
                        }

                        const string insertItem = @"
INSERT INTO dbo.Torg12Items (Torg12ID, ProductID, Quantity, UnitPrice)
VALUES (@tid, @pid, @qty, @price);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        var itemId = context.Database.SqlQuery<int>(
                            insertItem,
                            new SqlParameter("@tid", snapshot.Torg12Id),
                            new SqlParameter("@pid", product.ProductID),
                            new SqlParameter("@qty", row.Quantity),
                            new SqlParameter("@price", row.UnitPrice <= 0 ? product.UnitPrice : row.UnitPrice)
                        ).First();

                        snapshot.AddedItemIds.Add(itemId);

                        var stock = context.StockBalances.FirstOrDefault(sb => sb.ProductID == product.ProductID);
                        if (stock == null)
                        {
                            stock = new StockBalances
                            {
                                ProductID = product.ProductID,
                                ZoneID = 1,
                                Quantity = 0,
                                LastUpdated = DateTime.Now
                            };
                            context.StockBalances.Add(stock);
                        }

                        if (snapshot.StockChanges.ContainsKey(product.ProductID))
                            snapshot.StockChanges[product.ProductID] += row.Quantity;
                        else
                            snapshot.StockChanges[product.ProductID] = row.Quantity;

                        stock.Quantity += row.Quantity;
                        stock.LastUpdated = DateTime.Now;

                        context.ProductMovementHistory.Add(new ProductMovementHistory
                        {
                            ProductID = product.ProductID,
                            MovementType = "TORG12_IN",
                            Quantity = row.Quantity,
                            SourceDocumentID = snapshot.Torg12Id,
                            SourceDocumentType = "TORG12",
                            MovementDate = DateTime.Now,
                            EmployeeID = App.CurrentUser?.EmployeeID
                        });

                        allProcessedRows.Add(new ProcessedRow
                        {
                            ProductID = product.ProductID,
                            ProductName = product.ProductName,
                            Barcode = product.Barcode,
                            Quantity = row.Quantity,
                            UnitPrice = row.UnitPrice <= 0 ? product.UnitPrice : row.UnitPrice,
                            IsNewProduct = isNewProduct
                        });
                    }
                    context.SaveChanges();
                }

                // Сохраняем снапшот и активируем кнопку отмены
                _lastImportSnapshot = snapshot;
                HasActiveImport = true;

                // Обновляем UI
                DocumentNumber = preview.DocumentNumber;
                DocumentDate = preview.DocumentDate;
                ReceiverName = preview.ReceiverName;
                ReceiverAddress = preview.ReceiverAddress;
                Basis = preview.Basis;

                Items.Clear();
                foreach (var row in allProcessedRows)
                {
                    Items.Add(new Torg12LineItem
                    {
                        ProductID = row.ProductID,
                        ProductName = row.ProductName,
                        Barcode = row.Barcode,
                        AvailableStock = GetAvailableStock(row.ProductID),
                        Quantity = row.Quantity,
                        UnitPrice = row.UnitPrice
                    });
                }

                LoadMissingItems();
                LoadProducts(); // Перезагружаем товары с новыми остатками

                // Принудительно обновляем команды
                RefreshCommands();

                AuditLogger.Log("IMPORT", "TORG12", $"Импортирован ТОРГ-12 из Excel (ID={snapshot.Torg12Id})",
                    snapshot.Torg12Id.ToString(), $"Created={snapshot.CreatedProductIds.Count}, StockChanges={snapshot.StockChanges.Count}");
                SetStatus($"Импорт выполнен. Создано новых товаров: {snapshot.CreatedProductIds.Count}. Нажмите 'Отменить импорт' для отката.", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка импорта: {ex.Message}", true);
                if (_lastImportSnapshot != null)
                {
                    RollbackImport(_lastImportSnapshot);
                    _lastImportSnapshot = null;
                    HasActiveImport = false;
                    RefreshCommands();
                }
            }
        }

        private Products ShowProductEditWindow(string name, string barcode, decimal unitPrice)
        {
            var prefill = new Products
            {
                ProductName = string.IsNullOrWhiteSpace(name) ? "Новый товар" : name,
                Barcode = barcode ?? string.Empty,
                UnitPrice = unitPrice > 0 ? unitPrice : 1,
                WarrantyMonths = 12,
                MinStockLevel = 1,
                CategoryID = 1
            };

            var wnd = new ProductEditWindow(prefill);
            wnd.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            if (wnd.ShowDialog() == true)
                return wnd.GetProduct();
            return null;
        }

        private void RefreshCommands()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(HasActiveImport));
            OnPropertyChanged(nameof(CancelImportCommand));
        }

        public void CancelImport()
        {
            if (_lastImportSnapshot == null)
            {
                SetStatus("Нет активного импорта для отмены", true);
                return;
            }

            var result = MessageBox.Show(
                "Отменить последний импорт? Все добавленные товары и изменения остатков будут отменены.\nЭто действие нельзя отменить.",
                "Подтверждение отмены",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                RollbackImport(_lastImportSnapshot);
                _lastImportSnapshot = null;
                HasActiveImport = false;

                Clear();
                LoadProducts();
                LoadMissingItems();

                RefreshCommands();

                SetStatus("Импорт отменён. Данные восстановлены.", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка при отмене импорта: {ex.Message}", true);
            }
        }

        #endregion

        private void CreateProductFromMissing()
        {
            if (SelectedMissingItem == null) return;

            try
            {
                var prefill = new Products
                {
                    ProductName = SelectedMissingItem.TempProductName ?? "",
                    Barcode = SelectedMissingItem.TempBarcode ?? "",
                    UnitPrice = SelectedMissingItem.UnitPrice,
                    WarrantyMonths = 12,
                    MinStockLevel = 5,
                    CategoryID = 1
                };

                var wnd = new ProductEditWindow(prefill);
                wnd.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                if (wnd.ShowDialog() == true)
                {
                    var created = wnd.GetProduct();
                    using (var context = new KPMurtazinEntities())
                    {
                        context.Products.Add(created);
                        context.SaveChanges();

                        context.Database.ExecuteSqlCommand(@"
UPDATE dbo.Torg12ImportMissingItems
SET CreatedProductID=@pid, Status=N'Resolved'
WHERE MissingID=@mid;
INSERT INTO dbo.Torg12Items (Torg12ID, ProductID, Quantity, UnitPrice)
VALUES (@tid, @pid, @qty, @price);",
                            new SqlParameter("@pid", created.ProductID),
                            new SqlParameter("@mid", SelectedMissingItem.MissingID),
                            new SqlParameter("@tid", SelectedMissingItem.Torg12ID),
                            new SqlParameter("@qty", SelectedMissingItem.Quantity),
                            new SqlParameter("@price", SelectedMissingItem.UnitPrice <= 0 ? created.UnitPrice : SelectedMissingItem.UnitPrice)
                        );
                    }

                    AuditLogger.Log("CREATE", "Product", $"Создан товар из импорта ТОРГ-12: '{created.ProductName}'", created.ProductID.ToString());
                    LoadProducts();
                    SetStatus("Товар создан и добавлен в ТОРГ-12", false);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка создания товара: {ex.Message}", true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusColor = isError ? "#e74c3c" : "#3498db";
        }

        private void ShowTorg12Receipt()
        {
            var random = new Random();
            var receipt = new ReceiptModel
            {
                SaleNumber = random.Next(1000, 9999),
                ShiftNumber = 1,
                Cashier = App.CurrentUser?.Login ?? "АДМИНИСТРАТОР",
                DateTime = DateTime.Now,
                TotalAmount = TotalAmount,
                AmountWithoutVat = TotalAmount,
                CashPayment = TotalAmount,
                FdNumber = random.Next(100000, 999999),
                Fp = random.Next(100000000, 999999999).ToString(),
                DocumentNumber = random.Next(1, 9999),
                CompanyName = "ТОРГ-12 отпуск"
            };

            foreach (var item in Items)
            {
                receipt.Items.Add(new ReceiptItem
                {
                    Name = item.ProductName,
                    Price = item.UnitPrice,
                    Quantity = item.Quantity
                });
            }

            var receiptWindow = new ReceiptWindow(receipt);
            receiptWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            receiptWindow.ShowDialog();
        }

        private class Torg12ImportedItem
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public string Barcode { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private class ProcessedRow
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public string Barcode { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public bool IsNewProduct { get; set; }
        }
    }
}