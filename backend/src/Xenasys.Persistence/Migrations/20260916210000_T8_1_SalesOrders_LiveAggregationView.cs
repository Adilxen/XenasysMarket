using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916210000_T8_1_SalesOrders_LiveAggregationView")]
public partial class T8_1_SalesOrders_LiveAggregationView : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.SalesOrders', N'U') IS NULL
            BEGIN
              CREATE TABLE dbo.SalesOrders
              (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesOrders PRIMARY KEY,
                CompanyId int NOT NULL,
                OrderCode nvarchar(16) NOT NULL,
                OrderDate datetime2 NOT NULL,
                CustomerName nvarchar(150) NULL,
                Notes nvarchar(500) NULL,
                GrossAmount decimal(18,2) NOT NULL,
                Status nvarchar(20) NOT NULL,
                IsActive bit NOT NULL CONSTRAINT DF_SalesOrders_IsActive DEFAULT 1,
                InsertDate datetime2 NOT NULL,
                InsertUserId int NOT NULL,
                UpdateDate datetime2 NULL,
                UpdateUserId int NULL,
                CONSTRAINT FK_SalesOrders_Tenants_CompanyId
                  FOREIGN KEY (CompanyId) REFERENCES dbo.Tenants(Id)
              );
              CREATE UNIQUE INDEX UX_SalesOrders_CompanyId_OrderCode
                ON dbo.SalesOrders(CompanyId, OrderCode);
              CREATE INDEX IX_SalesOrders_CompanyId_Id
                ON dbo.SalesOrders(CompanyId, Id);
              CREATE INDEX IX_SalesOrders_CompanyId_IsActive_Id
                ON dbo.SalesOrders(CompanyId, IsActive, Id)
                INCLUDE (OrderCode, GrossAmount, Status, OrderDate);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.SalesOrderLines', N'U') IS NULL
            BEGIN
              CREATE TABLE dbo.SalesOrderLines
              (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesOrderLines PRIMARY KEY,
                CompanyId int NOT NULL,
                SalesOrderId int NOT NULL,
                ProductId int NOT NULL,
                ProductCode nvarchar(16) NOT NULL,
                ProductName nvarchar(200) NOT NULL,
                Quantity int NOT NULL,
                UnitPrice decimal(18,2) NOT NULL,
                LineTotal decimal(18,2) NOT NULL,
                InsertDate datetime2 NOT NULL,
                InsertUserId int NOT NULL,
                UpdateDate datetime2 NULL,
                UpdateUserId int NULL,
                CONSTRAINT FK_SalesOrderLines_Tenants_CompanyId
                  FOREIGN KEY (CompanyId) REFERENCES dbo.Tenants(Id),
                CONSTRAINT FK_SalesOrderLines_SalesOrders_SalesOrderId
                  FOREIGN KEY (SalesOrderId) REFERENCES dbo.SalesOrders(Id),
                CONSTRAINT FK_SalesOrderLines_Products_ProductId
                  FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id),
                CONSTRAINT CK_SalesOrderLines_Quantity_Positive CHECK ([Quantity] > 0)
              );
              CREATE INDEX IX_SalesOrderLines_SalesOrderId_Id
                ON dbo.SalesOrderLines(SalesOrderId, Id);
              CREATE INDEX IX_SalesOrderLines_CompanyId_Id
                ON dbo.SalesOrderLines(CompanyId, Id);
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW dbo.vw_super_admin_sales_by_tenant
            AS
            SELECT
              t.Id AS CompanyId,
              t.Name AS CompanyName,
              t.TenantCode,
              CAST(ISNULL(SUM(CASE WHEN o.IsActive = 1 AND o.Status = N'Posted' THEN o.GrossAmount ELSE 0 END), 0) AS decimal(18,2)) AS GrossSales,
              CAST(ISNULL(SUM(CASE WHEN o.IsActive = 1 AND o.Status = N'Posted' THEN 1 ELSE 0 END), 0) AS int) AS OrderCount,
              CAST(N'Live' AS nvarchar(40)) AS Status
            FROM dbo.Tenants t
            LEFT JOIN dbo.SalesOrders o ON o.CompanyId = t.Id
            WHERE t.IsActive = 1
            GROUP BY t.Id, t.Name, t.TenantCode;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW dbo.vw_super_admin_sales_by_tenant
            AS
            SELECT
              t.Id AS CompanyId,
              t.Name AS CompanyName,
              t.TenantCode,
              CAST(0 AS decimal(18,2)) AS GrossSales,
              CAST(0 AS int) AS OrderCount,
              CAST(N'PendingSalesModule' AS nvarchar(40)) AS Status
            FROM dbo.Tenants t
            WHERE 1 = 0;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.SalesOrderLines', N'U') IS NOT NULL DROP TABLE dbo.SalesOrderLines;
            IF OBJECT_ID(N'dbo.SalesOrders', N'U') IS NOT NULL DROP TABLE dbo.SalesOrders;
            """);
    }
}
