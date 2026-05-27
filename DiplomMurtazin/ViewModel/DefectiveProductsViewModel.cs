using DiplomMurtazin.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace DiplomMurtazin.ViewModel
{
    public class DefectiveProductItem : BaseViewModel
    {
        public int DefectiveID { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public string Reason { get; set; }

        public DateTime? ReturnDate { get; set; }

        public string Status { get; set; }

        public string Notes { get; set; }
    }

    public class DefectiveProductsViewModel : BaseViewModel
    {
        private string _searchText;

        private string _selectedStatus;

        private DateTime _startDate = DateTime.Today.AddMonths(-1);

        private DateTime _endDate = DateTime.Today;

        public ObservableCollection<DefectiveProductItem> DefectiveItems
        {
            get;
            set;
        }

        public ObservableCollection<string> Statuses
        {
            get;
            set;
        }

        public string SearchText
        {
            get => _searchText;
            set => Set(ref _searchText, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set => Set(ref _selectedStatus, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => Set(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => Set(ref _endDate, value);
        }

        public ICommand RefreshCommand { get; }

        public DefectiveProductsViewModel()
        {
            DefectiveItems =
                new ObservableCollection<DefectiveProductItem>();

            Statuses =
                new ObservableCollection<string>
                {
                    "Все",
                    "На проверке",
                    "Списан",
                    "Отремонтирован"
                };

            SelectedStatus = "Все";

            RefreshCommand =
                new RelayCommand(_ => LoadData());

            LoadData();
        }

        private void LoadData()
        {
            using (var context = new KPMurtazinEntities())
            {
                var query = context.DefectiveProducts.AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    query = query.Where(x =>
                        x.Products.ProductName.Contains(SearchText) ||
                        x.Reason.Contains(SearchText));
                }

                if (SelectedStatus != "Все")
                {
                    query = query.Where(x =>
                        x.Status == SelectedStatus);
                }

                var start = StartDate.Date;
                var end = EndDate.Date.AddDays(1);

                query = query.Where(x =>
                    x.ReturnDate >= start &&
                    x.ReturnDate < end);

                var items = query
                    .ToList()
                    .Select(x => new DefectiveProductItem
                    {
                        DefectiveID = x.DefectiveID,

                        ProductName = x.Products != null
                            ? x.Products.ProductName
                            : "Неизвестный товар",

                        Quantity = x.Quantity,

                        Reason = x.Reason,

                        ReturnDate = x.ReturnDate,

                        Status = x.Status,

                        Notes = x.Notes
                    })
                    .OrderByDescending(x => x.ReturnDate)
                    .ToList();

                DefectiveItems.Clear();

                foreach (var item in items)
                {
                    DefectiveItems.Add(item);
                }

                OnPropertyChanged(nameof(DefectiveItems));
            }
        }
    }
}