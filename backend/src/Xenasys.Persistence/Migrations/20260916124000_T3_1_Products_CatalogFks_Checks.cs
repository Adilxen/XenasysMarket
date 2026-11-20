using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916124000_T3_1_Products_CatalogFks_Checks")]
public partial class T3_1_Products_CatalogFks_Checks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM Products;");

        migrationBuilder.Sql(
            """
            IF COL_LENGTH('Products', 'BrandId') IS NULL
              ALTER TABLE Products ADD BrandId int NOT NULL CONSTRAINT DF_Products_BrandId DEFAULT 0;
            IF COL_LENGTH('Products', 'UnitId') IS NULL
              ALTER TABLE Products ADD UnitId int NOT NULL CONSTRAINT DF_Products_UnitId DEFAULT 0;
            """);

        migrationBuilder.Sql(
            """
            -- Make Category/SubCategory required (table empty after DELETE).
            ALTER TABLE Products ALTER COLUMN CategoryId int NOT NULL;
            ALTER TABLE Products ALTER COLUMN SubCategoryId int NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Categories_CategoryId')
              ALTER TABLE Products ADD CONSTRAINT FK_Products_Categories_CategoryId
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_SubCategories_SubCategoryId')
              ALTER TABLE Products ADD CONSTRAINT FK_Products_SubCategories_SubCategoryId
                FOREIGN KEY (SubCategoryId) REFERENCES SubCategories(Id);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Brands_BrandId')
              ALTER TABLE Products ADD CONSTRAINT FK_Products_Brands_BrandId
                FOREIGN KEY (BrandId) REFERENCES Brands(Id);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Units_UnitId')
              ALTER TABLE Products ADD CONSTRAINT FK_Products_Units_UnitId
                FOREIGN KEY (UnitId) REFERENCES Units(Id);
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_CompanyId_CategoryId')
              CREATE INDEX IX_Products_CompanyId_CategoryId ON Products(CompanyId, CategoryId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_CompanyId_BrandId')
              CREATE INDEX IX_Products_CompanyId_BrandId ON Products(CompanyId, BrandId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_CategoryId')
              CREATE INDEX IX_Products_CategoryId ON Products(CategoryId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_SubCategoryId')
              CREATE INDEX IX_Products_SubCategoryId ON Products(SubCategoryId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_BrandId')
              CREATE INDEX IX_Products_BrandId ON Products(BrandId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_UnitId')
              CREATE INDEX IX_Products_UnitId ON Products(UnitId);
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_InvoicePrice_Positive')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_InvoicePrice_Positive CHECK ([InvoicePrice] > 0);
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_RetailPrice_Positive')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_RetailPrice_Positive CHECK ([RetailPrice] > 0);
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_Retail_Gte_Invoice')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_Retail_Gte_Invoice CHECK ([RetailPrice] >= [InvoicePrice]);
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_MinStock_NonNegative')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_MinStock_NonNegative CHECK ([MinimumStockLevel] >= 0);
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'DF_Products_BrandId', N'D') IS NOT NULL ALTER TABLE Products DROP CONSTRAINT DF_Products_BrandId;
            IF OBJECT_ID(N'DF_Products_UnitId', N'D') IS NOT NULL ALTER TABLE Products DROP CONSTRAINT DF_Products_UnitId;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE Products DROP CONSTRAINT CK_Products_InvoicePrice_Positive;
            ALTER TABLE Products DROP CONSTRAINT CK_Products_RetailPrice_Positive;
            ALTER TABLE Products DROP CONSTRAINT CK_Products_Retail_Gte_Invoice;
            ALTER TABLE Products DROP CONSTRAINT CK_Products_MinStock_NonNegative;
            ALTER TABLE Products DROP CONSTRAINT FK_Products_Categories_CategoryId;
            ALTER TABLE Products DROP CONSTRAINT FK_Products_SubCategories_SubCategoryId;
            ALTER TABLE Products DROP CONSTRAINT FK_Products_Brands_BrandId;
            ALTER TABLE Products DROP CONSTRAINT FK_Products_Units_UnitId;
            DROP INDEX IF EXISTS IX_Products_CompanyId_CategoryId ON Products;
            DROP INDEX IF EXISTS IX_Products_CompanyId_BrandId ON Products;
            DROP INDEX IF EXISTS IX_Products_CategoryId ON Products;
            DROP INDEX IF EXISTS IX_Products_SubCategoryId ON Products;
            DROP INDEX IF EXISTS IX_Products_BrandId ON Products;
            DROP INDEX IF EXISTS IX_Products_UnitId ON Products;
            ALTER TABLE Products DROP COLUMN BrandId;
            ALTER TABLE Products DROP COLUMN UnitId;
            ALTER TABLE Products ALTER COLUMN CategoryId int NULL;
            ALTER TABLE Products ALTER COLUMN SubCategoryId int NULL;
            """);
    }
}
