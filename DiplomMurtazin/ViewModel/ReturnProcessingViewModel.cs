using DiplomMurtazin.Core;
using DiplomMurtazin.Model;
using DiplomMurtazin.View;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
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
        private bool _isBrokenSelected;
        public bool IsBrokenSelected
        {
            get => _isBrokenSelected;
            set
            {
                Set(ref _isBrokenSelected, value);
                UpdateReason();
                UpdateReturnTypeMessage();
            }
        }

        private bool _isDamagedSelected;
        public bool IsDamagedSelected
        {
            get => _isDamagedSelected;
            set
            {
                Set(ref _isDamagedSelected, value);
                UpdateReason();
                UpdateReturnTypeMessage();
            }
        }

        private bool _isFactoryDefectSelected;
        public bool IsFactoryDefectSelected
        {
            get => _isFactoryDefectSelected;
            set
            {
                Set(ref _isFactoryDefectSelected, value);
                UpdateReason();
                UpdateReturnTypeMessage();
            }
        }

        private bool _isWrongItemSelected;
        public bool IsWrongItemSelected
        {
            get => _isWrongItemSelected;
            set
            {
                Set(ref _isWrongItemSelected, value);
                UpdateReason();
                UpdateReturnTypeMessage();
            }
        }

        private bool _isClientChangedMindSelected;
        public bool IsClientChangedMindSelected
        {
            get => _isClientChangedMindSelected;
            set
            {
                Set(ref _isClientChangedMindSelected, value);
                UpdateReason();
                UpdateReturnTypeMessage();
            }
        }

        private string _selectedReasonText;
        public string SelectedReasonText
        {
            get => _selectedReasonText;
            set => Set(ref _selectedReasonText, value);
        }
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
        private void UpdateReason()
        {
            var reasons = new List<string>();

            if (IsBrokenSelected)
                reasons.Add("Сломан товар");

            if (IsDamagedSelected)
                reasons.Add("Поврежден");

            if (IsFactoryDefectSelected)
                reasons.Add("Заводской брак");

            if (IsWrongItemSelected)
                reasons.Add("Выдан не тот товар");

            if (IsClientChangedMindSelected)
                reasons.Add("Клиент передумал");

            SelectedReasonText = string.Join(", ", reasons);

            ReturnReason = SelectedReasonText;
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
        private void UpdateReturnTypeMessage()
        {
            bool isDefective =
                IsBrokenSelected ||
                IsDamagedSelected ||
                IsFactoryDefectSelected;

            if (isDefective)
            {
                StatusMessage = "Товар будет отправлен в дефектные товары";
                StatusColor = "#e74c3c";
            }
            else
            {
                StatusMessage = "Товар будет возвращен на склад";
                StatusColor = "#27ae60";
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

                    bool isDefective =
    IsBrokenSelected ||
    IsDamagedSelected ||
    IsFactoryDefectSelected;

                    if (isDefective)
                    {
                        context.DefectiveProducts.Add(new DefectiveProducts
                        {
                            ProductID = SelectedUnit.ProductID,
                            SaleID = SelectedUnit.SaleID,
                            Quantity = 1,
                            Reason = SelectedReasonText,
                            ReturnDate = now,
                            EmployeeID = App.CurrentUser != null
    ? App.CurrentUser.EmployeeID
    : 1,
                            Status = "На проверке",
                            Notes = ManagerComment
                        });

                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.ProductUnits SET Status = N'DEFECTIVE', LastUpdated = @Now WHERE UnitID = @UnitID",
                            new SqlParameter("@Now", now),
                            new SqlParameter("@UnitID", SelectedUnit.UnitID));
                    }
                    else
                    {
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.StockBalances SET Quantity = Quantity + 1, LastUpdated = @Now WHERE ProductID = @ProductID",
                            new SqlParameter("@Now", now),
                            new SqlParameter("@ProductID", SelectedUnit.ProductID));

                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.ProductUnits SET Status = N'RETURNED', LastUpdated = @Now WHERE UnitID = @UnitID",
                            new SqlParameter("@Now", now),
                            new SqlParameter("@UnitID", SelectedUnit.UnitID));
                    }

                    context.SaveChanges();
                    
                        var receipt = new ReceiptModel
                        {
                            SaleNumber = SelectedUnit.SaleID,
                            ShiftNumber = 1,
                            Cashier = App.CurrentUser?.Login ?? "АДМИНИСТРАТОР",
                            DateTime = DateTime.Now,
                            TotalAmount = -refund,  // отрицательная сумма
                            AmountWithoutVat = -refund,
                            CashPayment = -refund,
                            FdNumber = new Random().Next(100000, 999999),
                            Fp = new Random().Next(100000000, 999999999).ToString(),
                            DocumentNumber = new Random().Next(1, 9999),
                            CompanyName = "ВОЗВРАТ ТОВАРА"
                        };
                        receipt.Items.Add(new ReceiptItem
                        {
                            Name = SelectedUnit.ProductName,
                            Price = SelectedUnit.UnitPrice,
                            Quantity = -1   // отрицательное количество
                        });

                        var receiptWindow = new ReceiptWindow(receipt);
                        receiptWindow.Owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w is MainWindow);
                        receiptWindow.ShowDialog();
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
                var error = ex.Message;

                if (ex.InnerException != null)
                    error += "\n" + ex.InnerException.Message;

                if (ex.InnerException?.InnerException != null)
                    error += "\n" + ex.InnerException.InnerException.Message;

                SetStatus(error, true);

                MessageBox.Show(error);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusColor = isError ? "#e74c3c" : "#3498db";
        }
    }
}
