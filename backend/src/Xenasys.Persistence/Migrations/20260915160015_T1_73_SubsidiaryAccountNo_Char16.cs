using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_73_SubsidiaryAccountNo_Char16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ACC-{TenantCode}-{yyyy}-{seq} → TenantCode + yyyy + seq6
            migrationBuilder.Sql(
                """
                UPDATE [SubsidiaryAccounts]
                SET [AccountNo] =
                    UPPER(SUBSTRING([AccountNo], 5, 6))
                    + SUBSTRING([AccountNo], 12, 4)
                    + RIGHT(REPLICATE(N'0', 6) + SUBSTRING([AccountNo], 17, 10), 6)
                WHERE [AccountNo] LIKE N'ACC-[A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]-[0-9][0-9][0-9][0-9]-%';
                """);

            // ACC-{yyyy}-{seq} → TenantCode + yyyy + seq6 (join company)
            migrationBuilder.Sql(
                """
                UPDATE sa
                SET sa.[AccountNo] =
                    UPPER(t.[TenantCode])
                    + SUBSTRING(sa.[AccountNo], 5, 4)
                    + RIGHT(REPLICATE(N'0', 6) + SUBSTRING(sa.[AccountNo], 10, 10), 6)
                FROM [SubsidiaryAccounts] sa
                INNER JOIN [Tenants] t ON t.[Id] = sa.[CompanyId]
                WHERE sa.[AccountNo] LIKE N'ACC-[0-9][0-9][0-9][0-9]-%'
                  AND (LEN(sa.[AccountNo]) - LEN(REPLACE(sa.[AccountNo], N'-', N''))) = 2;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "char(16)",
                fixedLength: true,
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(16)",
                oldFixedLength: true,
                oldMaxLength: 16);
        }
    }
}
