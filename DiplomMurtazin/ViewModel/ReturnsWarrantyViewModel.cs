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

        private string _managerComment;

        private string _statusMessage = "Готово";

        private string _statusColor = "#3498db";

        private int _selectedProductId;

        // =========================
        // REASONS
        // =========================

        private bool _isClientReason;

        private bool _isWrongKitReason;

        private bool _isBrokenReason;

        private bool _isNoPowerReason;

        private bool _isDamageReason;

        private bool _isDefectReason;

        public bool IsClientReason
        {
            get => _isClientReason;
            set
            {
                Set(ref _isClientReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public bool IsWrongKitReason
        {
            get => _isWrongKitReason;
            set
            {
                Set(ref _isWrongKitReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public bool IsBrokenReason
        {
            get => _isBrokenReason;
            set
            {
                Set(ref _isBrokenReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public bool IsNoPowerReason
        {
            get => _isNoPowerReason;
            set
            {
                Set(ref _isNoPowerReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public bool IsDamageReason
        {
            get => _isDamageReason;
            set
            {
                Set(ref _isDamageReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public bool IsDefectReason
        {
            get => _isDefectReason;
            set
            {
                Set(ref _isDefectReason, value);
                OnPropertyChanged(nameof(ReturnDestinationText));
            }
        }

        public string ReturnDestinationText
        {
            get
            {
                if (IsBrokenReason ||
                    IsNoPowerReason ||
                    IsDamageReason ||
                    IsDefectReason)
                {
                    return "⚠️ Товар будет отправлен в дефектные товары.";
                }

                return "✅ Товар будет возвращен на склад.";
            }
        }

        public ObservableCollection<SoldUnitItem> SoldUnits { get; }
            = new ObservableCollection<SoldUnitItem>();

        public ObservableCollection<ProductTimelineItem> ProductTimeline { get; }
            = new ObservableCollection<ProductTimelineItem>();

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

        public ReturnsWarrantyViewModel()
        {
            RefreshCommand =
                new RelayCommand(_ => LoadSoldUnits());

            ProcessReturnCommand =
                new RelayCommand(_ => ProcessReturn(),
                    _ => SelectedUnit != null);
        }

        public void LoadSoldUnits()
        {
            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    const string sql = @"
SELECT u.UnitID,
       u.ProductID,
       p.ProductName,
       u.SoldDate,
       u.ReturnEndDate,
       u.WarrantyEndDate,
       si.UnitPrice,
       u.SaleID,
       u.SaleItemID,
       u.Status
FROM dbo.ProductUnits u
JOIN dbo.Products p ON p.ProductID = u.ProductID
LEFT JOIN dbo.SaleItems si ON si.SaleItemID = u.SaleItemID
WHERE u.Status = N'SOLD'
AND (@Search = N''
OR p.ProductName LIKE N'%' + @Search + N'%'
OR CAST(u.UnitID AS NVARCHAR(20)) = @Search)
ORDER BY u.SoldDate DESC";

                    var data =
                        context.Database.SqlQuery<SoldUnitItem>(
                            sql,
                            new SqlParameter("@Search",
                                SearchText ?? string.Empty))
                        .ToList();

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
                return;

            try
            {
                string reason = GetSelectedReason();

                if (string.IsNullOrWhiteSpace(reason))
                {
                    SetStatus("Выберите причину возврата.", true);
                    return;
                }

                bool isDefective =
                    IsBrokenReason ||
                    IsNoPowerReason ||
                    IsDamageReason ||
                    IsDefectReason;

                var now = DateTime.Now;

                using (var context = new KPMurtazinEntities())
                {
                    // =========================
                    // СОХРАНЕНИЕ ВОЗВРАТА
                    // =========================

                    context.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.ProductReturns
(
UnitID,
ProductID,
SaleID,
SaleItemID,
ReturnDate,
ReturnReason,
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
@ProcessedBy
)",
                        new SqlParameter("@UnitID", SelectedUnit.UnitID),
                        new SqlParameter("@ProductID", SelectedUnit.ProductID),
                        new SqlParameter("@SaleID", SelectedUnit.SaleID),
                        new SqlParameter("@SaleItemID", SelectedUnit.SaleItemID),
                        new SqlParameter("@ReturnDate", now),
                        new SqlParameter("@ReturnReason", reason),
                        new SqlParameter("@ProcessedBy",
                            (object)App.CurrentUser?.EmployeeID ?? DBNull.Value));

                    // =========================
                    // ОБНОВЛЕНИЕ СТАТУСА UNIT
                    // =========================

                    context.Database.ExecuteSqlCommand(@"
UPDATE dbo.ProductUnits
SET Status = @Status,
    LastUpdated = @Now
WHERE UnitID = @UnitID",
                        new SqlParameter("@Status",
                            isDefective ? "DEFECTIVE" : "RETURNED"),

                        new SqlParameter("@Now", now),

                        new SqlParameter("@UnitID",
                            SelectedUnit.UnitID));

                    // =========================
                    // ЕСЛИ ДЕФЕКТНЫЙ
                    // =========================

                    if (isDefective)
                    {
                        context.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.DefectiveProducts
(
ProductID,
SaleID,
Quantity,
Reason,
ReturnDate,
EmployeeID,
Status,
Notes
)
VALUES
(
@ProductID,
@SaleID,
1,
@Reason,
@ReturnDate,
@EmployeeID,
@Status,
@Notes
)",
                            new SqlParameter("@ProductID",
                                SelectedUnit.ProductID),

                            new SqlParameter("@SaleID",
                                SelectedUnit.SaleID),

                            new SqlParameter("@Reason",
                                reason),

                            new SqlParameter("@ReturnDate",
                                now),

                            new SqlParameter("@EmployeeID",
                                (object)App.CurrentUser?.EmployeeID ?? DBNull.Value),

                            new SqlParameter("@Status",
                                "На диагностике"),

                            new SqlParameter("@Notes",
                                (object)ManagerComment ?? DBNull.Value));
                    }
                    else
                    {
                        // =========================
                        // ОБЫЧНЫЙ СКЛАД
                        // =========================

                        context.Database.ExecuteSqlCommand(@"
UPDATE dbo.StockBalances
SET Quantity = Quantity + 1,
    LastUpdated = @Now
WHERE ProductID = @ProductID",
                            new SqlParameter("@Now", now),

                            new SqlParameter("@ProductID",
                                SelectedUnit.ProductID));
                    }

                    // =========================
                    // ИСТОРИЯ ДВИЖЕНИЯ
                    // =========================

                    context.ProductMovementHistory.Add(
                        new ProductMovementHistory
                        {
                            ProductID = SelectedUnit.ProductID,

                            MovementType =
                                isDefective
                                    ? "DEFECTIVE_RETURN"
                                    : "RETURN",

                            Quantity = 1,

                            SourceDocumentID =
                                SelectedUnit.SaleID,

                            SourceDocumentType = "RETURN",

                            MovementDate = now,

                            EmployeeID =
                                App.CurrentUser?.EmployeeID
                        });

                    context.SaveChanges();
                }

                SetStatus(
                    isDefective
                        ? "Товар отправлен в дефектные товары."
                        : "Товар возвращен на склад.",
                    false);

                LoadSoldUnits();

                LoadProductTimeline();
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка возврата: {ex.Message}", true);
            }
        }

        private string GetSelectedReason()
        {
            if (IsClientReason)
                return "Не подошел клиенту";

            if (IsWrongKitReason)
                return "Ошибка комплектации";

            if (IsBrokenReason)
                return "Товар неисправен";

            if (IsNoPowerReason)
                return "Не включается";

            if (IsDamageReason)
                return "Повреждение корпуса";

            if (IsDefectReason)
                return "Брак";

            return null;
        }

        private void LoadProductTimeline()
        {
            ProductTimeline.Clear();

            if (SelectedProductId <= 0)
                return;

            try
            {
                using (var context = new KPMurtazinEntities())
                {
                    var movements =
                        context.Database.SqlQuery<ProductTimelineItem>(@"
SELECT MovementDate AS EventDate,
       CONCAT(N'Движение: ', MovementType) AS EventType,
       CONCAT(N'Кол-во: ', Quantity) AS Description
FROM dbo.ProductMovementHistory
WHERE ProductID = @ProductID",
                        new SqlParameter("@ProductID",
                            SelectedProductId))
                        .ToList();

                    foreach (var item in movements
                                 .OrderByDescending(x => x.EventDate))
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

            StatusColor =
                isError
                    ? "#e74c3c"
                    : "#3498db";
        }
    }
}