using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DiplomMurtazin.View
{
    public partial class PriceHistoryWindow : Window
    {
        public ObservableCollection<PriceHistoryItem> PriceHistory { get; set; }
        public PriceHistoryWindow(int productId)
        {
            InitializeComponent();
            using (var ctx = new KPMurtazinEntities())
            {
                var history = ctx.Database.SqlQuery<PriceHistoryItem>(@"
                    SELECT ph.ChangedAt, ph.OldPrice, ph.NewPrice, ph.Source,
                           e.LastName + ' ' + e.FirstName AS ChangedByEmployeeName
                    FROM dbo.ProductPriceHistory ph
                    LEFT JOIN dbo.Employees e ON e.EmployeeID = ph.ChangedByEmployeeID
                    WHERE ph.ProductID = @pid
                    ORDER BY ph.ChangedAt DESC",
                    new System.Data.SqlClient.SqlParameter("@pid", productId)).ToList();
                PriceHistory = new ObservableCollection<PriceHistoryItem>(history);
            }
            DataContext = this;
        }
    }
    public class PriceHistoryItem
    {
        public System.DateTime ChangedAt { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Source { get; set; }
        public string ChangedByEmployeeName { get; set; }
    }
}