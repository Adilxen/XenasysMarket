using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_83_AccountNo_GlobalUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data renumber may already be applied; index swap is idempotent.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_CompanyId_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    DROP INDEX [IX_SubsidiaryAccounts_CompanyId_AccountNo] ON [SubsidiaryAccounts];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    CREATE UNIQUE INDEX [IX_SubsidiaryAccounts_AccountNo] ON [SubsidiaryAccounts] ([AccountNo]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    DROP INDEX [IX_SubsidiaryAccounts_AccountNo] ON [SubsidiaryAccounts];

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_CompanyId_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    CREATE UNIQUE INDEX [IX_SubsidiaryAccounts_CompanyId_AccountNo]
                        ON [SubsidiaryAccounts] ([CompanyId], [AccountNo]);
                """);
        }
    }
}
