using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916220000_T3_9_Products_Barcode_TenantUnique")]
public partial class T3_9_Products_Barcode_TenantUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Whole-string uniqueness (multi-token lists still validated in ProductBarcodeRules).
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
              SELECT 1 FROM sys.indexes
              WHERE name = N'UX_Products_CompanyId_Barcode'
                AND object_id = OBJECT_ID(N'dbo.Products'))
            BEGIN
              CREATE UNIQUE INDEX UX_Products_CompanyId_Barcode
                ON dbo.Products(CompanyId, Barcode)
                WHERE Barcode IS NOT NULL AND Barcode <> N'';
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
              SELECT 1 FROM sys.indexes
              WHERE name = N'UX_Products_CompanyId_Barcode'
                AND object_id = OBJECT_ID(N'dbo.Products'))
              DROP INDEX UX_Products_CompanyId_Barcode ON dbo.Products;
            """);
    }
}
