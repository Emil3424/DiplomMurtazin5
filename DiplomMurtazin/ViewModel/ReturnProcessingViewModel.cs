using DiplomMurtazin.Core;
using DiplomMurtazin.Model;
using DiplomMurtazin.View;
using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class ReturnProcessingViewModel : BaseViewModel
    {
        private SoldUnitItem _selectedUnit;
        private string _searchText;
        private string _returnReason;
        private string _managerComment;
        private string _statusMessage = "Готово";
        private string _statusColor = "#3498db";

        public ObservableCollection<SoldUnitItem> SoldUnits { get; } = new ObservableCollection<SoldUnitItem>();
        private bool _isDefective;

        public bool IsDefective
        {
            get => _isDefective;
            set
            {
                Set(ref _isDefective, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public string ReturnDestinationText =>
            IsDefective
                ? "⚠️ Товар будет отправлен в дефектные товары"
                : "✅ Товар будет возвращен на склад";
        public SoldUnitItem SelectedUnit
        {
            get => _selectedUnit;
            set => Set(ref _selectedUnit, value);
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

        public string ManagerComment
        {
            get => _managerComment;
            set => Set(ref _managerComment, value);
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

        public ReturnProcessingViewModel()
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
       ISNULL(si.UnitPrice, 0) AS UnitPrice, u.SaleID, u.SaleItemID, u.Status
FROM dbo.ProductUnits u
JOIN dbo.Products p ON p.ProductID = u.ProductID
LEFT JOIN dbo.SaleItems si ON si.SaleItemID = u.SaleItemID
WHERE u.Status = N'SOLD'
  AND (@Search = N'' OR p.ProductName LIKE N'%' + @Search + N'%' OR CAST(u.UnitID AS NVARCHAR(20)) = @Search)
ORDER BY u.SoldDate DESC";
                    var rows = context.Database.SqlQuery<SoldUnitItem>(sql, new SqlParameter("@Search", SearchText ?? string.Empty)).ToList();
                    SoldUnits.Clear();
                    foreach (var row in rows) SoldUnits.Add(row);
                }
                SetStatus($"К возврату доступно: {SoldUnits.Count}", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка загрузки: {ex.Message}", true);
            }
        }

        private void ProcessReturn()
        {
            if (SelectedUnit == null)
                return;

            try
            {
                var now = DateTime.Now;

                var isWarranty =
                    SelectedUnit.WarrantyEndDate.HasValue &&
                    now.Date <= SelectedUnit.WarrantyEndDate.Value.Date;

                var refund =
                    SelectedUnit.ReturnEndDate.HasValue &&
                    now.Date <= SelectedUnit.ReturnEndDate.Value.Date
                        ? SelectedUnit.UnitPrice
                        : 0m;

                var reason =
                    $"{ReturnReason ?? ""} {ManagerComment ?? ""}".Trim();

                int returnId;

                using (var context = new KPMurtazinEntities())
                {
                    // =====================================
                    // СОЗДАНИЕ ВОЗВРАТА
                    // =====================================

                    returnId = context.Database.SqlQuery<int>(@"
INSERT INTO dbo.ProductReturns
(
UnitID,
ProductID,
SaleID,
SaleItemID,
ReturnDate,
ReturnReason,
IsWarrantyCase,
RefundAmount,
ProcessedByEmployeeID
)
VALUES
(
@UnitID,
@ProductID,
@SaleID,
@SaleItemID,
@ReturnDate,
@ReturnReason,
@IsWarrantyCase,
@RefundAmount,
@ProcessedBy
);

SELECT CAST(SCOPE_IDENTITY() AS INT);",

                        new SqlParameter("@UnitID", SelectedUnit.UnitID),
                        new SqlParameter("@ProductID", SelectedUnit.ProductID),
                        new SqlParameter("@SaleID", SelectedUnit.SaleID),
                        new SqlParameter("@SaleItemID", SelectedUnit.SaleItemID),
                        new SqlParameter("@ReturnDate", now),
                        new SqlParameter("@ReturnReason", (object)reason ?? DBNull.Value),
                        new SqlParameter("@IsWarrantyCase", isWarranty),
                        new SqlParameter("@RefundAmount", refund),
                        new SqlParameter("@ProcessedBy",
                            (object)App.CurrentUser?.EmployeeID ?? DBNull.Value))
                        .First();

                    // =====================================
                    // ЕСЛИ БРАК
                    // =====================================

                    if (IsDefective)
                    {
                        context.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.DefectiveProducts
(
UnitID,
ProductID,
SaleID,
ReturnID,
Quantity,
Reason,
Status,
Notes,
CreatedDate,
EmployeeID
)
VALUES
(
@UnitID,
@ProductID,
@SaleID,
@ReturnID,
1,
@Reason,
@Status,
@Notes,
@CreatedDate,
@EmployeeID
)",

                            new SqlParameter("@UnitID", SelectedUnit.UnitID),

                            new SqlParameter("@ProductID", SelectedUnit.ProductID),

                            new SqlParameter("@SaleID", SelectedUnit.SaleID),

                            new SqlParameter("@ReturnID", returnId),

                            new SqlParameter("@Reason", reason),

                            new SqlParameter("@Status", "На диагностике"),

                            new SqlParameter("@Notes",
                                (object)ManagerComment ?? DBNull.Value),

                            new SqlParameter("@CreatedDate", now),

                            new SqlParameter("@EmployeeID",
                                (object)App.CurrentUser?.EmployeeID ?? DBNull.Value));

                        // Меняем статус Unit

                        context.Database.ExecuteSqlCommand(@"
UPDATE dbo.ProductUnits
SET Status = N'DEFECTIVE',
    LastUpdated = @Now,
    ReturnDocumentID = @ReturnID
WHERE UnitID = @UnitID",

                            new SqlParameter("@Now", now),
                            new SqlParameter("@ReturnID", returnId),
                            new SqlParameter("@UnitID", SelectedUnit.UnitID));

                        // История

                        context.ProductMovementHistory.Add(
                            new ProductMovementHistory
                            {
                                ProductID = SelectedUnit.ProductID,
                                MovementType = "DEFECTIVE_RETURN",
                                Quantity = 1,
                                SourceDocumentID = returnId,
                                SourceDocumentType = "RETURN",
                                MovementDate = now,
                                EmployeeID = App.CurrentUser?.EmployeeID
                            });
                    }
                    else
                    {
                        // =====================================
                        // ОБЫЧНЫЙ ВОЗВРАТ
                        // =====================================

                        context.Database.ExecuteSqlCommand(@"
UPDATE dbo.ProductUnits
SET Status = N'RETURNED',
    LastUpdated = @Now,
    ReturnDocumentID = @ReturnID
WHERE UnitID = @UnitID",

                            new SqlParameter("@Now", now),
                            new SqlParameter("@ReturnID", returnId),
                            new SqlParameter("@UnitID", SelectedUnit.UnitID));

                        // Возврат на склад

                        context.Database.ExecuteSqlCommand(@"
UPDATE dbo.StockBalances
SET Quantity = Quantity + 1,
    LastUpdated = @Now
WHERE ProductID = @ProductID",

                            new SqlParameter("@Now", now),
                            new SqlParameter("@ProductID", SelectedUnit.ProductID));

                        // История

                        context.ProductMovementHistory.Add(
                            new ProductMovementHistory
                            {
                                ProductID = SelectedUnit.ProductID,
                                MovementType = "RETURN",
                                Quantity = 1,
                                SourceDocumentID = returnId,
                                SourceDocumentType = "RETURN",
                                MovementDate = now,
                                EmployeeID = App.CurrentUser?.EmployeeID
                            });
                    }

                    context.SaveChanges();
                }

                SetStatus(
                    IsDefective
                        ? "Товар отправлен в дефектные товары"
                        : $"Возврат оформлен. Сумма: {refund:F2} ₽",
                    false);

                ReturnReason = "";
                ManagerComment = "";

                LoadSoldUnits();
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка возврата: {ex.Message}", true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusColor = isError ? "#e74c3c" : "#3498db";
        }
    }
}
