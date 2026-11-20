using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916143000_T5_1_Stock_Quantity_Movements_Outbox")]
public partial class T5_1_Stock_Quantity_Movements_Outbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('Products', 'QuantityOnHand') IS NULL
            BEGIN
              ALTER TABLE Products ADD QuantityOnHand int NOT NULL
                CONSTRAINT DF_Products_QuantityOnHand DEFAULT 0;
            END
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_QtyOnHand_NonNegative')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_QtyOnHand_NonNegative
                CHECK ([QuantityOnHand] >= 0);
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
            BEGIN
              CREATE TABLE dbo.StockMovements
              (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY,
                CompanyId int NOT NULL,
                ProductId int NOT NULL,
                QuantityDelta int NOT NULL,
                QuantityAfter int NOT NULL,
                Reason nvarchar(250) NULL,
                OccurredAt datetime2 NOT NULL,
                InsertDate datetime2 NOT NULL,
                InsertUserId int NOT NULL,
                UpdateDate datetime2 NULL,
                UpdateUserId int NULL,
                CONSTRAINT FK_StockMovements_Tenants_CompanyId
                  FOREIGN KEY (CompanyId) REFERENCES dbo.Tenants(Id),
                CONSTRAINT FK_StockMovements_Products_ProductId
                  FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
              );
              CREATE INDEX IX_StockMovements_CompanyId_ProductId_OccurredAt
                ON dbo.StockMovements(CompanyId, ProductId, OccurredAt);
              CREATE INDEX IX_StockMovements_CompanyId_Id
                ON dbo.StockMovements(CompanyId, Id);
            END
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.NotificationOutbox', N'U') IS NULL
            BEGIN
              CREATE TABLE dbo.NotificationOutbox
              (
                Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationOutbox PRIMARY KEY,
                CompanyId int NOT NULL,
                ProductId int NULL,
                EventType nvarchar(50) NOT NULL,
                PayloadJson nvarchar(4000) NOT NULL,
                Status nvarchar(20) NOT NULL,
                ProcessedAt datetime2 NULL,
                ErrorMessage nvarchar(1000) NULL,
                InsertDate datetime2 NOT NULL,
                InsertUserId int NOT NULL,
                UpdateDate datetime2 NULL,
                UpdateUserId int NULL,
                CONSTRAINT FK_NotificationOutbox_Tenants_CompanyId
                  FOREIGN KEY (CompanyId) REFERENCES dbo.Tenants(Id),
                CONSTRAINT FK_NotificationOutbox_Products_ProductId
                  FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
              );
              CREATE INDEX IX_NotificationOutbox_CompanyId_Status_Id
                ON dbo.NotificationOutbox(CompanyId, Status, Id);
              CREATE INDEX IX_NotificationOutbox_Company_Product_Event_Status
                ON dbo.NotificationOutbox(CompanyId, ProductId, EventType, Status);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.NotificationOutbox', N'U') IS NOT NULL DROP TABLE dbo.NotificationOutbox;
            IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NOT NULL DROP TABLE dbo.StockMovements;
            IF COL_LENGTH('Products', 'QuantityOnHand') IS NOT NULL
            BEGIN
              IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Products_QtyOnHand_NonNegative')
                ALTER TABLE Products DROP CONSTRAINT CK_Products_QtyOnHand_NonNegative;
              IF OBJECT_ID(N'DF_Products_QuantityOnHand', N'D') IS NOT NULL
                ALTER TABLE Products DROP CONSTRAINT DF_Products_QuantityOnHand;
              ALTER TABLE Products DROP COLUMN QuantityOnHand;
            END
            """);
    }
}
