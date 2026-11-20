using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_74_SubsidiaryAccountNo_Char12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CHAR(16) TenantCode+yyyy+seq6 → CHAR(12) TenantCode+seq6
            migrationBuilder.Sql(
                """
                UPDATE [SubsidiaryAccounts]
                SET [AccountNo] = LEFT(RTRIM([AccountNo]), 6) + RIGHT(RTRIM([AccountNo]), 6)
                WHERE LEN(RTRIM([AccountNo])) = 16
                  AND CHARINDEX(N'-', [AccountNo]) = 0;
                """);

            // Hyphenated leftovers → TenantCode + seq6
            migrationBuilder.Sql(
                """
                UPDATE sa
                SET sa.[AccountNo] =
                    UPPER(t.[TenantCode])
                    + RIGHT(REPLICATE(N'0', 6) + RIGHT(sa.[AccountNo], CHARINDEX(N'-', REVERSE(sa.[AccountNo])) - 1), 6)
                FROM [SubsidiaryAccounts] sa
                INNER JOIN [Tenants] t ON t.[Id] = sa.[CompanyId]
                WHERE CHARINDEX(N'-', sa.[AccountNo]) > 0;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "char(12)",
                fixedLength: true,
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(16)",
                oldFixedLength: true,
                oldMaxLength: 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "char(16)",
                fixedLength: true,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(12)",
                oldFixedLength: true,
                oldMaxLength: 12);
        }
    }
}
