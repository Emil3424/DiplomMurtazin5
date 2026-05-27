using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace DiplomMurtazin.Core
{
    public static class ReturnsWarrantyService
    {
        public static void RegisterSoldUnits(KPMurtazinEntities context, Sales sale, IEnumerable<SaleItems> saleItems)
        {
            foreach (var item in saleItems)
            {
                var product = context.Products.FirstOrDefault(p => p.ProductID == item.ProductID);
                if (product == null)
                {
                    continue;
                }

                var returnDays = context.Database.SqlQuery<int?>(
                    "SELECT ReturnDays FROM dbo.Products WHERE ProductID = @ProductID",
                    new SqlParameter("@ProductID", item.ProductID)).FirstOrDefault() ?? 14;
                var warrantyMonths = product.WarrantyMonths ?? 0;

                for (var i = 0; i < item.Quantity; i++)
                {
                    context.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.ProductUnits
(
    ProductID, ReceivedDate, SoldDate, SaleID, SaleItemID,
    ReturnEndDate, WarrantyEndDate, Status, LastUpdated
)
VALUES
(
    @ProductID, @ReceivedDate, @SoldDate, @SaleID, @SaleItemID,
    @ReturnEndDate, @WarrantyEndDate, N'SOLD', @LastUpdated
)",
                        new SqlParameter("@ProductID", item.ProductID),
                        new SqlParameter("@ReceivedDate", sale.SaleDateTime),
                        new SqlParameter("@SoldDate", sale.SaleDateTime),
                        new SqlParameter("@SaleID", sale.SaleID),
                        new SqlParameter("@SaleItemID", item.SaleItemID),
                        new SqlParameter("@ReturnEndDate", sale.SaleDateTime.Date.AddDays(returnDays)),
                        new SqlParameter("@WarrantyEndDate", sale.SaleDateTime.Date.AddMonths(warrantyMonths)),
                        new SqlParameter("@LastUpdated", DateTime.Now));
                }
            }
        }
    }
}
