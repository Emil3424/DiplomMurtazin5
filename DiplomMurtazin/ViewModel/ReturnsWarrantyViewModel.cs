using DiplomMurtazin.Core;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class SoldUnitItem : BaseViewModel
    {
        public int UnitID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public DateTime SoldDate { get; set; }
        public DateTime? ReturnEndDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public decimal UnitPrice { get; set; }
        public int SaleID { get; set; }
        public int SaleItemID { get; set; }
        public string Status { get; set; }
    }

    public class ProductTimelineItem
    {
        public DateTime EventDate { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
    }

    public class ReturnsWarrantyViewModel : BaseViewModel
    {
        private SoldUnitItem _selectedUnit;
        private string _searchText;
        private string _returnReason;
        private string _statusMessage = "Готово";
        private string _statusColor = "#3498db";
        private int _selectedProductId;

        public ObservableCollection<SoldUnitItem> SoldUnits { get; } = new ObservableCollection<SoldUnitItem>();
        public ObservableCollection<ProductTimelineItem> ProductTimeline { get; } = new ObservableCollection<ProductTimelineItem>();

        public SoldUnitItem SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (Set(ref _selectedUnit, value) && value != null)
                {
                    SelectedProductId = value.ProductID;
                }
            }
        }

        public int SelectedProductId
        {
            get => _selectedProductId;
            set
            {
                if (Set(ref _selectedProductId, value))
                {
                    LoadProductTimeline();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        public string ReturnReason
        {
            get => _returnReason;
            set => Set(ref _returnReason, value);
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

        public ICommand RefreshCommand { get; }
        public ICommand ProcessReturnCommand { get; }

        public ReturnsWarrantyViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadSoldUnits());
            ProcessReturnCommand = new RelayCommand(_ => ProcessReturn(), _ => SelectedUnit != null);
        }

        public void LoadSoldUnits()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    const string sql = @"
SELECT u.UnitID, u.ProductID, p.ProductName, u.SoldDate, u.ReturnEndDate, u.WarrantyEndDate,
       si.UnitPrice, u.SaleID, u.SaleItemID, u.Status
FROM dbo.ProductUnits u
JOIN dbo.Products p ON p.ProductID = u.ProductID
LEFT JOIN dbo.SaleItems si ON si.SaleItemID = u.SaleItemID
WHERE u.Status = N'SOLD'
  AND (@Search = N'' OR p.ProductName LIKE N'%' + @Search + N'%' OR CAST(u.UnitID AS NVARCHAR(20)) = @Search)
ORDER BY u.SoldDate DESC";

                    var data = context.Database.SqlQuery<SoldUnitItem>(
                        sql,
                        new SqlParameter("@Search", SearchText ?? string.Empty)).ToList();

                    SoldUnits.Clear();
                    foreach (var item in data)
                    {
                        SoldUnits.Add(item);
                    }
                }

                SetStatus($"Загружено экземпляров: {SoldUnits.Count}", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки: {ex.Message}", true);
            }
        }

        private void ProcessReturn()
        {
            if (SelectedUnit == null)
            {
                return;
            }

            try
            {
                var now = DateTime.Now;
                var isWarranty = SelectedUnit.WarrantyEndDate.HasValue && now.Date <= SelectedUnit.WarrantyEndDate.Value.Date;
                var refund = SelectedUnit.ReturnEndDate.HasValue && now.Date <= SelectedUnit.ReturnEndDate.Value.Date
                    ? SelectedUnit.UnitPrice
                    : 0m;

                using (var context = new KPMurtazinEntities())
                {
                    context.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.ProductReturns
(UnitID, ProductID, SaleID, SaleItemID, ReturnDate, ReturnReason, IsWarrantyCase, RefundAmount, ProcessedByEmployeeID)
VALUES
(@UnitID, @ProductID, @SaleID, @SaleItemID, @ReturnDate, @ReturnReason, @IsWarrantyCase, @RefundAmount, @ProcessedBy)",
                        new SqlParameter("@UnitID", SelectedUnit.UnitID),
                        new SqlParameter("@ProductID", SelectedUnit.ProductID),
                        new SqlParameter("@SaleID", SelectedUnit.SaleID),
                        new SqlParameter("@SaleItemID", SelectedUnit.SaleItemID),
                        new SqlParameter("@ReturnDate", now),
                        new SqlParameter("@ReturnReason", (object)(ReturnReason ?? string.Empty)),
                        new SqlParameter("@IsWarrantyCase", isWarranty),
                        new SqlParameter("@RefundAmount", refund),
                        new SqlParameter("@ProcessedBy", (object)App.CurrentUser?.EmployeeID ?? DBNull.Value));

                    context.Database.ExecuteSqlCommand(
                        "UPDATE dbo.ProductUnits SET Status = N'RETURNED', LastUpdated = @Now WHERE UnitID = @UnitID",
                        new SqlParameter("@Now", now),
                        new SqlParameter("@UnitID", SelectedUnit.UnitID));

                    context.Database.ExecuteSqlCommand(@"
UPDATE dbo.StockBalances
SET Quantity = Quantity + 1, LastUpdated = @Now
WHERE ProductID = @ProductID",
                        new SqlParameter("@Now", now),
                        new SqlParameter("@ProductID", SelectedUnit.ProductID));

                    context.ProductMovementHistory.Add(new ProductMovementHistory
                    {
                        ProductID = SelectedUnit.ProductID,
                        MovementType = "RETURN",
                        Quantity = 1,
                        SourceDocumentID = SelectedUnit.SaleID,
                        SourceDocumentType = "RETURN",
                        MovementDate = now,
                        EmployeeID = App.CurrentUser?.EmployeeID
                    });

                    context.SaveChanges();
                }

                AuditLogger.Log("RETURN", "ProductUnit", $"Возврат экземпляра #{SelectedUnit.UnitID}. Гарантия={isWarranty}, Возвратная сумма={refund:F2}",
                    SelectedUnit.UnitID.ToString(), $"ProductID={SelectedUnit.ProductID}");

                SetStatus($"Возврат оформлен. Сумма к возврату: {refund:F2} ₽", false);
                LoadSoldUnits();
                LoadProductTimeline();
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка возврата: {ex.Message}", true);
            }
        }

        private void LoadProductTimeline()
        {
            ProductTimeline.Clear();
            if (SelectedProductId <= 0)
            {
                return;
            }

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
                        new SqlParameter("@ProductID", SelectedProductId)).ToList();

                    var prices = context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT ChangedAt AS EventDate,
       N'Изменение цены' AS EventType,
       CONCAT(N'С ', OldPrice, N' на ', NewPrice) AS Description
FROM dbo.ProductPriceHistory
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID", SelectedProductId)).ToList();

                    var returns = context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT ReturnDate AS EventDate,
       N'Возврат' AS EventType,
       CONCAT(N'Возврат экземпляра #', UnitID, N'. Сумма: ', RefundAmount, N'. Гарантийный случай: ', CASE WHEN IsWarrantyCase = 1 THEN N'Да' ELSE N'Нет' END) AS Description
FROM dbo.ProductReturns
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID", SelectedProductId)).ToList();

                    foreach (var item in movements.Concat(prices).Concat(returns).OrderByDescending(x => x.EventDate))
                    {
                        ProductTimeline.Add(item);
                    }
                }
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
