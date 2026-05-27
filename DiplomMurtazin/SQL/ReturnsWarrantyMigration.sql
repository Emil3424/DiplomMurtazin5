IF COL_LENGTH('dbo.Products', 'ReturnDays') IS NULL
BEGIN
    ALTER TABLE dbo.Products
    ADD ReturnDays INT NULL;
END
GO

IF OBJECT_ID(N'dbo.ProductUnits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductUnits
    (
        UnitID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductUnits PRIMARY KEY,
        ProductID INT NOT NULL,
        SerialNumber NVARCHAR(100) NULL,
        ReceivedDate DATETIME2(7) NOT NULL CONSTRAINT DF_ProductUnits_ReceivedDate DEFAULT (SYSDATETIME()),
        SoldDate DATETIME2(7) NULL,
        SaleID INT NULL,
        SaleItemID INT NULL,
        ReturnEndDate DATE NULL,
        WarrantyEndDate DATE NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_ProductUnits_Status DEFAULT (N'IN_STOCK'),
        ReturnDocumentID INT NULL,
        LastUpdated DATETIME2(7) NOT NULL CONSTRAINT DF_ProductUnits_LastUpdated DEFAULT (SYSDATETIME())
    );
END
GO

IF OBJECT_ID(N'dbo.ProductReturns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductReturns
    (
        ReturnID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductReturns PRIMARY KEY,
        UnitID INT NOT NULL,
        ProductID INT NOT NULL,
        SaleID INT NULL,
        SaleItemID INT NULL,
        ReturnDate DATETIME2(7) NOT NULL CONSTRAINT DF_ProductReturns_ReturnDate DEFAULT (SYSDATETIME()),
        ReturnReason NVARCHAR(500) NULL,
        IsWarrantyCase BIT NOT NULL CONSTRAINT DF_ProductReturns_IsWarrantyCase DEFAULT (0),
        RefundAmount DECIMAL(10,2) NOT NULL CONSTRAINT DF_ProductReturns_RefundAmount DEFAULT (0),
        ProcessedByEmployeeID INT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.ProductPriceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductPriceHistory
    (
        PriceHistoryID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductPriceHistory PRIMARY KEY,
        ProductID INT NOT NULL,
        OldPrice DECIMAL(10,2) NOT NULL,
        NewPrice DECIMAL(10,2) NOT NULL,
        ChangedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductPriceHistory_ChangedAt DEFAULT (SYSDATETIME()),
        ChangedByEmployeeID INT NULL,
        Source NVARCHAR(50) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Products')
BEGIN
    ALTER TABLE dbo.ProductUnits WITH CHECK
    ADD CONSTRAINT FK_ProductUnits_Products
        FOREIGN KEY (ProductID) REFERENCES dbo.Products(ProductID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_Sales')
AND OBJECT_ID(N'dbo.Sales', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ProductUnits WITH CHECK
    ADD CONSTRAINT FK_ProductUnits_Sales
        FOREIGN KEY (SaleID) REFERENCES dbo.Sales(SaleID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductUnits_SaleItems')
AND OBJECT_ID(N'dbo.SaleItems', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ProductUnits WITH CHECK
    ADD CONSTRAINT FK_ProductUnits_SaleItems
        FOREIGN KEY (SaleItemID) REFERENCES dbo.SaleItems(SaleItemID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductReturns_ProductUnits')
BEGIN
    ALTER TABLE dbo.ProductReturns WITH CHECK
    ADD CONSTRAINT FK_ProductReturns_ProductUnits
        FOREIGN KEY (UnitID) REFERENCES dbo.ProductUnits(UnitID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductReturns_Products')
BEGIN
    ALTER TABLE dbo.ProductReturns WITH CHECK
    ADD CONSTRAINT FK_ProductReturns_Products
        FOREIGN KEY (ProductID) REFERENCES dbo.Products(ProductID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductPriceHistory_Products')
BEGIN
    ALTER TABLE dbo.ProductPriceHistory WITH CHECK
    ADD CONSTRAINT FK_ProductPriceHistory_Products
        FOREIGN KEY (ProductID) REFERENCES dbo.Products(ProductID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductUnits_ProductStatus' AND object_id = OBJECT_ID(N'dbo.ProductUnits'))
BEGIN
    CREATE INDEX IX_ProductUnits_ProductStatus ON dbo.ProductUnits(ProductID, Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductUnits_ReturnWarranty' AND object_id = OBJECT_ID(N'dbo.ProductUnits'))
BEGIN
    CREATE INDEX IX_ProductUnits_ReturnWarranty ON dbo.ProductUnits(ReturnEndDate, WarrantyEndDate);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductReturns_ReturnDate' AND object_id = OBJECT_ID(N'dbo.ProductReturns'))
BEGIN
    CREATE INDEX IX_ProductReturns_ReturnDate ON dbo.ProductReturns(ReturnDate DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductPriceHistory_ProductDate' AND object_id = OBJECT_ID(N'dbo.ProductPriceHistory'))
BEGIN
    CREATE INDEX IX_ProductPriceHistory_ProductDate ON dbo.ProductPriceHistory(ProductID, ChangedAt DESC);
END
GO
