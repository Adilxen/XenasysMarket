using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916113000_T2_14_Vendor_NtnNo_Optional")]
public partial class T2_14_Vendor_NtnNo_Optional : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_Vendors_CompanyId_NtnNo",
            table: "Vendors");

        migrationBuilder.AlterColumn<string>(
            name: "NtnNo",
            table: "Vendors",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32);

        // Multiple NULL NTNs allowed; non-empty values stay unique per company.
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX UX_Vendors_CompanyId_NtnNo
            ON Vendors (CompanyId, NtnNo)
            WHERE NtnNo IS NOT NULL AND NtnNo <> N'';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS UX_Vendors_CompanyId_NtnNo ON Vendors;");

        migrationBuilder.Sql(
            """
            UPDATE Vendors SET NtnNo = N'' WHERE NtnNo IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "NtnNo",
            table: "Vendors",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "UX_Vendors_CompanyId_NtnNo",
            table: "Vendors",
            columns: new[] { "CompanyId", "NtnNo" },
            unique: true);
    }
}
