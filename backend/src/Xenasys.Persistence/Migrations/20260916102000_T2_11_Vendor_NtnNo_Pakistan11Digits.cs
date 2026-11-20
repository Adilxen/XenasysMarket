using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916102000_T2_11_Vendor_NtnNo_Pakistan11Digits")]
public partial class T2_11_Vendor_NtnNo_Pakistan11Digits : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Widen first so 11-digit Pakistan NTNs (and future international IDs) fit.
        migrationBuilder.AlterColumn<string>(
            name: "NtnNo",
            table: "Vendors",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(9)",
            oldMaxLength: 9);

        // Normalize existing seed/legacy NTNs to unique 11-digit values.
        migrationBuilder.Sql(
            """
            UPDATE v
            SET NtnNo = RIGHT(REPLICATE('0', 11) + CAST(v.Id AS varchar(11)), 11)
            FROM Vendors v;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE v
            SET NtnNo = LEFT(v.NtnNo, 9)
            FROM Vendors v;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "NtnNo",
            table: "Vendors",
            type: "nvarchar(9)",
            maxLength: 9,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32);
    }
}
