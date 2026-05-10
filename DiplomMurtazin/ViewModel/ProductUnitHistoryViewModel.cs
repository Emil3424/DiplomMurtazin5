using DiplomMurtazin.Core;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class ProductUnitHistoryViewModel : BaseViewModel
    {
        private ObservableCollection<Products> _allProducts = new ObservableCollection<Products>();
        private ObservableCollection<Products> _filteredProducts = new ObservableCollection<Products>();
        private Products _selectedProduct;
        private string _searchText;
        private string _statusMessage = "Выберите товар";
        private string _statusColor = "#3498db";

        public ObservableCollection<Products> FilteredProducts
        {
            get => _filteredProducts;
            set => Set(ref _filteredProducts, value);
        }

        public Products SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (Set(ref _selectedProduct, value) && value != null)
                {
                    LoadTimeline();
                    LoadUnits();
                }
            }
        }

        public ObservableCollection<ProductTimelineItem> Timeline { get; } = new ObservableCollection<ProductTimelineItem>();
        public ObservableCollection<SoldUnitItem> Units { get; } = new ObservableCollection<SoldUnitItem>();

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

        public ICommand LoadCommand { get; }

        public ProductUnitHistoryViewModel()
        {
            LoadCommand = new RelayCommand(_ => LoadProducts());
        }

        private void LoadProducts()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    var list = context.Products.OrderBy(p => p.ProductName).ToList();
                    _allProducts = new ObservableCollection<Products>(list);
                    FilteredProducts = new ObservableCollection<Products>(list);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки товаров: {ex.Message}", true);
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredProducts = new ObservableCollection<Products>(_allProducts);
                return;
            }

            var q = SearchText.ToLower();
            FilteredProducts = new ObservableCollection<Products>(
                _allProducts.Where(p => (p.ProductName ?? "").ToLower().Contains(q) || (p.Barcode ?? "").Contains(SearchText)));
        }

        private void LoadUnits()
        {
            Units.Clear();
            if (SelectedProduct == null) return;

            using (var context = new KPMurtazinEntities())
            {
                var units = context.Database.SqlQuery<SoldUnitItem>(@"
SELECT pu.UnitID, pu.ProductID, @ProductName AS ProductName, pu.SoldDate, pu.ReturnEndDate, pu.WarrantyEndDate, ISNULL(si.UnitPrice, 0) AS UnitPrice, pu.SaleID, pu.SaleItemID, pu.Status
FROM dbo.ProductUnits pu
LEFT JOIN dbo.SaleItems si ON si.SaleItemID = pu.SaleItemID
WHERE pu.ProductID = @ProductID
ORDER BY pu.UnitID DESC",
                    new SqlParameter("@ProductID", SelectedProduct.ProductID),
                    new SqlParameter("@ProductName", SelectedProduct.ProductName ?? "")).ToList();

                foreach (var item in units)
                {
                    Units.Add(item);
                }
            }
        }

        private void LoadTimeline()
        {
            Timeline.Clear();
            if (SelectedProduct == null) return;

            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    var movements = context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT MovementDate AS EventDate,
       CONCAT(N'Движение: ', MovementType) AS EventType,
       CONCAT(N'Кол-во: ', Quantity, N', Документ: ', ISNULL(SourceDocumentType, N''), N' #', ISNULL(CAST(SourceDocumentID AS NVARCHAR(20)), N'')) AS Description
FROM dbo.ProductMovementHistory
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID", SelectedProduct.ProductID)).ToList();

                    var prices = context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT ChangedAt AS EventDate,
       N'Изменение цены' AS EventType,
       CONCAT(N'С ', OldPrice, N' на ', NewPrice) AS Description
FROM dbo.ProductPriceHistory
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID", SelectedProduct.ProductID)).ToList();

                    var returns = context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT ReturnDate AS EventDate,
       N'Возврат' AS EventType,
       CONCAT(N'Возврат экземпляра #', UnitID, N'. Причина: ', ISNULL(ReturnReason, N'')) AS Description
FROM dbo.ProductReturns
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID", SelectedProduct.ProductID)).ToList();

                    foreach (var item in movements.Concat(prices).Concat(returns).OrderByDescending(x => x.EventDate))
                    {
                        Timeline.Add(item);
                    }
                }

                SetStatus($"История загружена: {Timeline.Count} событий", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка истории товара: {ex.Message}", true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusColor = isError ? "#e74c3c" : "#3498db";
        }
    }
}
