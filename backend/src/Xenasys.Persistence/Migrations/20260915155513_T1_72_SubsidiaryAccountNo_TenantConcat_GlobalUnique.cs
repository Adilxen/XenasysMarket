using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_72_SubsidiaryAccountNo_TenantConcat_GlobalUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rewrite legacy ACC-{yyyy}-{seq} → ACC-{TenantCode}-{yyyy}-{seq} before global UX.
            migrationBuilder.Sql(
                """
                UPDATE sa
                SET sa.[AccountNo] = N'ACC-' + UPPER(t.[TenantCode]) + N'-' + SUBSTRING(sa.[AccountNo], 5, 4000)
                FROM [SubsidiaryAccounts] sa
                INNER JOIN [Tenants] t ON t.[Id] = sa.[CompanyId]
                WHERE sa.[AccountNo] LIKE N'ACC-[0-9][0-9][0-9][0-9]-%'
                  AND (LEN(sa.[AccountNo]) - LEN(REPLACE(sa.[AccountNo], N'-', N''))) = 2;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SubsidiaryAccounts_AccountNo",
                table: "SubsidiaryAccounts",
                column: "AccountNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubsidiaryAccounts_AccountNo",
                table: "SubsidiaryAccounts");
        }
    }
}
