using DiplomMurtazin.Core;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;

namespace DiplomMurtazin.ViewModel
{
    public class DefectiveProductItem
    {
        public int DefectiveID { get; set; }

        public int UnitID { get; set; }

        public string ProductName { get; set; }

        public string Reason { get; set; }

        public string Status { get; set; }

        public System.DateTime CreatedDate { get; set; }
    }

    public class DefectiveProductsViewModel : BaseViewModel
    {
        public ObservableCollection<DefectiveProductItem>
            DefectiveProducts
        { get; }
                = new ObservableCollection<DefectiveProductItem>();

        public DefectiveProductsViewModel()
        {
            LoadData();
        }

        private void LoadData()
        {
            using (var context = new KPMurtazinEntities())
            {
                var data =
                    context.Database.SqlQuery<DefectiveProductItem>(@"
SELECT
d.DefectiveID,
d.UnitID,
p.ProductName,
d.Reason,
d.Status,
d.CreatedDate
FROM dbo.DefectiveProducts d
JOIN dbo.Products p
ON p.ProductID = d.ProductID
ORDER BY d.CreatedDate DESC")
                    .ToList();

                DefectiveProducts.Clear();

                foreach (var item in data)
                {
                    DefectiveProducts.Add(item);
                }
            }
        }
    }
}