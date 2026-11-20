using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916150000_T7_1_SuperAdmin_Aggregation_Views")]
public partial class T7_1_SuperAdmin_Aggregation_Views : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW dbo.vw_super_admin_inventory_summary
            AS
            SELECT
              t.Id AS CompanyId,
              t.Name AS CompanyName,
              t.TenantCode,
              COUNT(p.Id) AS ProductCount,
              SUM(CASE WHEN p.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveProductCount,
              CAST(ISNULL(SUM(CASE WHEN p.IsActive = 1 THEN p.QuantityOnHand ELSE 0 END), 0) AS bigint) AS TotalQuantityOnHand,
              SUM(CASE WHEN p.IsActive = 1 AND p.QuantityOnHand <= p.MinimumStockLevel THEN 1 ELSE 0 END) AS LowStockProductCount,
              (
                SELECT COUNT(1)
                FROM dbo.Vendors v
                WHERE v.CompanyId = t.Id AND v.IsActive = 1
              ) AS ActiveVendorCount
            FROM dbo.Tenants t
            LEFT JOIN dbo.Products p ON p.CompanyId = t.Id
            WHERE t.IsActive = 1
            GROUP BY t.Id, t.Name, t.TenantCode;
            """);

        migrationBuilder.Sql(
            """
            -- Stub view: zero rows until Sales module exists (M7.2 — do not fake live scans).
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
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.vw_super_admin_sales_by_tenant', N'V') IS NOT NULL
              DROP VIEW dbo.vw_super_admin_sales_by_tenant;
            IF OBJECT_ID(N'dbo.vw_super_admin_inventory_summary', N'V') IS NOT NULL
              DROP VIEW dbo.vw_super_admin_inventory_summary;
            """);
    }
}
