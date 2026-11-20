using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xenasys.Persistence.Data;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260916121500_T1_85_MasterPrefixUnique_CashBankProtect")]
public partial class T1_85_MasterPrefixUnique_CashBankProtect : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_MasterAccounts_Prefix",
            table: "MasterAccounts",
            column: "Prefix",
            unique: true);

        // Baseline Cash/Bank leaves must stay system-locked (#D7).
        migrationBuilder.Sql(
            """
            UPDATE SubsidiaryAccounts
            SET IsSystemGenerated = 1
            WHERE IsSystemGenerated = 0
              AND (
                SubsidiaryAccountCode LIKE N'%-CASH'
                OR SubsidiaryAccountCode LIKE N'%-BANK'
                OR SubsidiaryAccountCode IN (N'OTH-CASH', N'OTH-BANK')
                OR (SubsidiaryAccountTitle IN (N'Cash', N'Bank') AND SourceEntityType IS NULL AND SourceEntityId IS NULL)
              );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MasterAccounts_Prefix",
            table: "MasterAccounts");
    }
}
