using DiplomMurtazin.Core;
using DiplomMurtazin.View;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class ProductsViewModel : BaseViewModel
    {
        private KPMurtazinEntities _context;
        private ObservableCollection<Products> _allProducts;
        private ObservableCollection<Products> _filteredProducts;
        private ObservableCollection<Categories> _categories;
        private ObservableCollection<string> _brands;
        private ObservableCollection<string> _availabilityOptions;
        private Products _selectedProduct;
        private Categories _selectedCategoryFilter;
        private string _selectedBrandFilter;
        private decimal? _minPrice;
        private decimal? _maxPrice;
        private string _availabilityFilter = "Все";
        private string _searchText;
        private SortOption _selectedSortOption;
        private ObservableCollection<SortOption> _sortOptions;
        private string _statusMessage;
        private string _statusColor;
        private int _totalCount;
        private int _filteredCount;

        public ObservableCollection<Products> FilteredProducts
        {
            get => _filteredProducts;
            set => Set(ref _filteredProducts, value);
        }

        public ObservableCollection<Categories> Categories
        {
            get => _categories;
            set => Set(ref _categories, value);
        }

        public ObservableCollection<string> Brands
        {
            get => _brands;
            set => Set(ref _brands, value);
        }

        public ObservableCollection<string> AvailabilityOptions
        {
            get => _availabilityOptions;
            set => Set(ref _availabilityOptions, value);
        }

        public Products SelectedProduct
        {
            get => _selectedProduct;
            set => Set(ref _selectedProduct, value);
        }

        public Categories SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (Set(ref _selectedCategoryFilter, value))
                    ApplyFilters();
            }
        }

        public string SelectedBrandFilter
        {
            get => _selectedBrandFilter;
            set
            {
                if (Set(ref _selectedBrandFilter, value))
                    ApplyFilters();
            }
        }

        public decimal? MinPrice
        {
            get => _minPrice;
            set
            {
                if (Set(ref _minPrice, value))
                    ApplyFilters();
            }
        }

        public decimal? MaxPrice
        {
            get => _maxPrice;
            set
            {
                if (Set(ref _maxPrice, value))
                    ApplyFilters();
            }
        }

        public string AvailabilityFilter
        {
            get => _availabilityFilter;
            set
            {
                if (Set(ref _availabilityFilter, value))
                    ApplyFilters();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                    ApplyFilters();
            }
        }

        public SortOption SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (Set(ref _selectedSortOption, value))
                    ApplyFilters();
            }
        }

        public ObservableCollection<SortOption> SortOptions
        {
            get => _sortOptions;
            set => Set(ref _sortOptions, value);
        }

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

        public string FilteredProductsCount => $"{_filteredCount} / {_totalCount}";

        public ICommand LoadedCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand ShowPriceHistoryCommand { get; }

        public ProductsViewModel()
        {
            LoadedCommand = new RelayCommand(OnLoaded);
            AddCommand = new RelayCommand(OpenAddWindow);
            EditCommand = new RelayCommand(OpenEditWindow, CanEditDelete);
            DeleteCommand = new RelayCommand(DeleteProduct, CanEditDelete);
            RefreshCommand = new RelayCommand(RefreshData);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            ShowPriceHistoryCommand = new RelayCommand(ShowPriceHistory, CanEditDelete);

            InitializeSortOptions();
            InitializeAvailabilityOptions();
            _allProducts = new ObservableCollection<Products>();
            _filteredProducts = new ObservableCollection<Products>();
            _categories = new ObservableCollection<Categories>();
            _brands = new ObservableCollection<string>();
            DataRefreshBus.ExternalChangesDetected += OnExternalChangesDetected;
        }

        private void InitializeSortOptions()
        {
            SortOptions = new ObservableCollection<SortOption>
            {
                new SortOption { Name = "Название (А-Я)", Value = "ProductName", IsAscending = true },
                new SortOption { Name = "Название (Я-А)", Value = "ProductName", IsAscending = false },
                new SortOption { Name = "Цена (сначала дешёвые)", Value = "UnitPrice", IsAscending = true },
                new SortOption { Name = "Цена (сначала дорогие)", Value = "UnitPrice", IsAscending = false },
                new SortOption { Name = "Остаток (сначала больше)", Value = "StockQuantity", IsAscending = false },
                new SortOption { Name = "Остаток (сначала меньше)", Value = "StockQuantity", IsAscending = true }
            };
            SelectedSortOption = SortOptions.FirstOrDefault();
        }

        private void InitializeAvailabilityOptions()
        {
            AvailabilityOptions = new ObservableCollection<string> { "Все", "В наличии", "Нет в наличии" };
        }

        private void OnExternalChangesDetected(int _)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshData(null);
                SetStatus("Данные обновлены после внешних изменений", false);
            }));
        }

        private bool CanEditDelete(object parameter) => SelectedProduct != null;

        private void OnLoaded(object parameter)
        {
            LoadData();
            LoadCategories();
            LoadBrands();
        }

        private void LoadData()
        {
            try
            {
                _context = new KPMurtazinEntities();
                var productsList = _context.Products
                    .Include(p => p.Categories)
                    .OrderBy(p => p.ProductName)
                    .ToList();

                var stockDict = _context.StockBalances
                    .GroupBy(sb => sb.ProductID)
                    .Select(g => new { ProductID = g.Key, TotalStock = g.Sum(sb => sb.Quantity) })
                    .ToDictionary(k => k.ProductID, v => v.TotalStock);

                foreach (var product in productsList)
                {
                    product.StockQuantity = stockDict.ContainsKey(product.ProductID) ? stockDict[product.ProductID] : 0;
                }

                _allProducts.Clear();
                foreach (var product in productsList)
                    _allProducts.Add(product);

                _totalCount = _allProducts.Count;
                ApplyFilters();
                SetStatus("Готов к работе", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки: {ex.Message}", true);
            }
        }

        private void LoadCategories()
        {
            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    var list = ctx.Categories.OrderBy(c => c.CategoryName).ToList();
                    list.Insert(0, new Categories { CategoryID = 0, CategoryName = "Все категории" });
                    Categories = new ObservableCollection<Categories>(list);
                    SelectedCategoryFilter = Categories.First();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки категорий: {ex.Message}", true);
            }
        }

        private void LoadBrands()
        {
            try
            {
                using (var ctx = new KPMurtazinEntities())
                {
                    var brands = ctx.Products.Where(p => p.Manufacturer != null && p.Manufacturer != "")
                        .Select(p => p.Manufacturer).Distinct().OrderBy(b => b).ToList();
                    brands.Insert(0, "Все бренды");
                    Brands = new ObservableCollection<string>(brands);
                    SelectedBrandFilter = Brands.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки брендов: {ex.Message}", true);
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var query = _allProducts.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string search = SearchText.ToLower();
                    query = query.Where(p =>
                        (p.ProductName ?? "").ToLower().Contains(search) ||
                        (p.Barcode ?? "").ToLower().Contains(search) ||
                        (p.Manufacturer ?? "").ToLower().Contains(search) ||
                        (p.Model ?? "").ToLower().Contains(search)
                    );
                }

                if (SelectedCategoryFilter != null && SelectedCategoryFilter.CategoryID > 0)
                    query = query.Where(p => p.CategoryID == SelectedCategoryFilter.CategoryID);

                if (!string.IsNullOrWhiteSpace(SelectedBrandFilter) && SelectedBrandFilter != "Все бренды")
                    query = query.Where(p => p.Manufacturer == SelectedBrandFilter);

                if (MinPrice.HasValue)
                    query = query.Where(p => p.UnitPrice >= MinPrice.Value);
                if (MaxPrice.HasValue)
                    query = query.Where(p => p.UnitPrice <= MaxPrice.Value);

                if (AvailabilityFilter == "В наличии")
                    query = query.Where(p => p.StockQuantity > 0);
                else if (AvailabilityFilter == "Нет в наличии")
                    query = query.Where(p => p.StockQuantity == 0);

                if (SelectedSortOption != null)
                {
                    switch (SelectedSortOption.Value)
                    {
                        case "ProductName":
                            query = SelectedSortOption.IsAscending
                                ? query.OrderBy(p => p.ProductName)
                                : query.OrderByDescending(p => p.ProductName);
                            break;
                        case "UnitPrice":
                            query = SelectedSortOption.IsAscending
                                ? query.OrderBy(p => p.UnitPrice)
                                : query.OrderByDescending(p => p.UnitPrice);
                            break;
                        case "StockQuantity":
                            query = SelectedSortOption.IsAscending
                                ? query.OrderBy(p => p.StockQuantity)
                                : query.OrderByDescending(p => p.StockQuantity);
                            break;
                    }
                }

                FilteredProducts = new ObservableCollection<Products>(query);
                _filteredCount = FilteredProducts.Count;
                OnPropertyChanged(nameof(FilteredProductsCount));
                SetStatus($"Найдено: {_filteredCount}", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка фильтрации: {ex.Message}", true);
            }
        }

        private void ResetFilters(object parameter)
        {
            SearchText = "";
            SelectedCategoryFilter = Categories.FirstOrDefault(c => c.CategoryID == 0);
            SelectedBrandFilter = Brands.FirstOrDefault();
            MinPrice = null;
            MaxPrice = null;
            AvailabilityFilter = "Все";
            SelectedSortOption = SortOptions.FirstOrDefault();
            ApplyFilters();
            SetStatus("Фильтры сброшены", false);
        }

        private void OpenAddWindow(object parameter)
        {
            try
            {
                var editWindow = new ProductEditWindow();
                editWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                if (editWindow.ShowDialog() == true)
                {
                    var newProduct = editWindow.GetProduct();
                    using (var context = new KPMurtazinEntities())
                    {
                        context.Products.Add(newProduct);
                        context.SaveChanges();
                        AuditLogger.Log("CREATE", "Product", $"Добавлен товар '{newProduct.ProductName}'", newProduct.ProductID.ToString());
                        RefreshData(null);
                        SetStatus("Товар успешно добавлен", false);
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка: {ex.Message}", true);
            }
        }

        private void OpenEditWindow(object parameter)
        {
            if (SelectedProduct == null) return;
            try
            {
                var editWindow = new ProductEditWindow(SelectedProduct);
                editWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                if (editWindow.ShowDialog() == true)
                {
                    var editedProduct = editWindow.GetProduct();
                    using (var context = new KPMurtazinEntities())
                    {
                        var dbProduct = context.Products.Find(editedProduct.ProductID);
                        if (dbProduct != null)
                        {
                            var oldPrice = dbProduct.UnitPrice;
                            dbProduct.ProductName = editedProduct.ProductName;
                            dbProduct.Barcode = editedProduct.Barcode;
                            dbProduct.CategoryID = editedProduct.CategoryID;
                            dbProduct.Manufacturer = editedProduct.Manufacturer;
                            dbProduct.Model = editedProduct.Model;
                            dbProduct.UnitPrice = editedProduct.UnitPrice;
                            dbProduct.WarrantyMonths = editedProduct.WarrantyMonths;
                            dbProduct.MinStockLevel = editedProduct.MinStockLevel;
                            dbProduct.Description = editedProduct.Description;
                            context.SaveChanges();

                            if (oldPrice != dbProduct.UnitPrice)
                            {
                                context.Database.ExecuteSqlCommand(@"
                                    INSERT INTO dbo.ProductPriceHistory (ProductID, OldPrice, NewPrice, ChangedAt, ChangedByEmployeeID, Source)
                                    VALUES (@pid, @old, @new, @dt, @eid, @src)",
                                    new System.Data.SqlClient.SqlParameter("@pid", dbProduct.ProductID),
                                    new System.Data.SqlClient.SqlParameter("@old", oldPrice),
                                    new System.Data.SqlClient.SqlParameter("@new", dbProduct.UnitPrice),
                                    new System.Data.SqlClient.SqlParameter("@dt", DateTime.Now),
                                    new System.Data.SqlClient.SqlParameter("@eid", (object)App.CurrentUser?.EmployeeID ?? DBNull.Value),
                                    new System.Data.SqlClient.SqlParameter("@src", "ProductEdit"));
                            }

                            AuditLogger.Log("UPDATE", "Product", $"Обновлен товар '{dbProduct.ProductName}'", dbProduct.ProductID.ToString());
                            RefreshData(null);
                            SetStatus("Товар успешно обновлен", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка: {ex.Message}", true);
            }
        }

        private void DeleteProduct(object parameter)
        {
            if (SelectedProduct == null)
                return;

            var result = MessageBox.Show(
                $"Удалить товар '{SelectedProduct.ProductName}'?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    int productId = SelectedProduct.ProductID;
                    var saleItems = context.SaleItems
    .Where(x => x.ProductID == productId)
    .ToList();
                    // SaleItemID связанных товаров
                    var saleItemIds = context.SaleItems
                        .Where(x => x.ProductID == productId)
                        .Select(x => x.SaleItemID)
                        .ToList();

                    // ProductUnits
                    var productUnits = context.ProductUnits
                        .Where(x => saleItemIds.Contains((int)x.SaleItemID))
                        .ToList();

                    // ID юнитов
                    var unitIds = productUnits
                        .Select(x => x.UnitID)
                        .ToList();

                    // 1. ProductReturns
                    var productReturns = context.ProductReturns
                        .Where(x => unitIds.Contains(x.UnitID))
                        .ToList();

                    foreach (var item in productReturns)
                    {
                        context.ProductReturns.Remove(item);
                    }

                    // 2. ProductUnits
                    foreach (var unit in productUnits)
                    {
                        context.ProductUnits.Remove(unit);
                    }


                    foreach (var item in saleItems)
                    {
                        context.SaleItems.Remove(item);
                    }
                    var torg12Items = context.Torg12Items
    .Where(t => t.ProductID == SelectedProduct.ProductID)
    .ToList();

                    foreach (var item in torg12Items)
                        context.Torg12Items.Remove(item);

                    var inventoryItems = context.InventoryDetails
                        .Where(x => x.ProductID == productId)
                        .ToList();

                    foreach (var item in inventoryItems)
                    {
                        context.InventoryDetails.Remove(item);
                    }

                    var invoiceItems = context.InvoiceItems
                        .Where(x => x.ProductID == productId)
                        .ToList();

                    foreach (var item in invoiceItems)
                    {
                        context.InvoiceItems.Remove(item);
                    }

                    var stockBalances = context.StockBalances
                        .Where(x => x.ProductID == productId)
                        .ToList();

                    foreach (var item in stockBalances)
                    {
                        context.StockBalances.Remove(item);
                    }

                    var movementHistory = context.ProductMovementHistory
                        .Where(x => x.ProductID == productId)
                        .ToList();

                    foreach (var item in movementHistory)
                    {
                        context.ProductMovementHistory.Remove(item);
                    }
                    //bool isUsed =
                    //    context.SaleItems.Any(si => si.ProductID == productId) ||
                    //    context.InventoryDetails.Any(id => id.ProductID == productId) ||
                    //    context.InvoiceItems.Any(ii => ii.ProductID == productId);

                    //if (isUsed)
                    //{
                    //    SetStatus("Нельзя удалить: товар используется в документах", true);
                    //    return;
                    //}



                    var productToDelete = context.Products.Find(productId);

                    if (productToDelete != null)
                    {
                        context.Products.Remove(productToDelete);
                        context.SaveChanges();

                        AuditLogger.Log(
                            "DELETE",
                            "Product",
                            $"Удален товар '{productToDelete.ProductName}'",
                            productToDelete.ProductID.ToString());

                        RefreshData(null);
                        SetStatus("Товар удален", false);
                    }
                }
            }
            catch (Exception ex)
            {
                var error = ex.Message;

                if (ex.InnerException != null)
                    error += "\n\n" + ex.InnerException.Message;

                if (ex.InnerException?.InnerException != null)
                    error += "\n\n" + ex.InnerException.InnerException.Message;

                MessageBox.Show(error);

                SetStatus($"Ошибка удаления: {ex.Message}", true);
            }
        }

        private void RefreshData(object parameter)
        {
            _context?.Dispose();
            LoadData();
            LoadCategories();
            LoadBrands();
            SetStatus("Данные обновлены", false);
        }

        private void ShowPriceHistory(object parameter)
        {
            if (SelectedProduct == null) return;
            var window = new PriceHistoryWindow(SelectedProduct.ProductID);
            window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            window.ShowDialog();
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusColor = isError ? "#e74c3c" : "#3498db";
        }

        public void DisposeContext()
        {
            _context?.Dispose();
        }
    }
}