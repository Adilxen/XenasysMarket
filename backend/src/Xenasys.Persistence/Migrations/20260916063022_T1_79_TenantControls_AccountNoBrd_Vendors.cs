using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_79_TenantControls_AccountNoBrd_Vendors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop only the single-column unique index. AlterColumn below rebuilds
            // IX_SubsidiaryAccounts_CompanyId_AccountNo itself; dropping it here makes that DROP fail.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    DROP INDEX [IX_SubsidiaryAccounts_AccountNo] ON [SubsidiaryAccounts];
                """);

            migrationBuilder.DropIndex(
                name: "IX_ControlAccounts_Code",
                table: "ControlAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ControlAccounts_ControlKind",
                table: "ControlAccounts");

            // Widen before rewrite — seeder converts legacy CHAR(12) → ACC-yyyy-seq5.
            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(12)",
                oldFixedLength: true,
                oldMaxLength: 12);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubsidiaryAccounts_CompanyId_AccountNo' AND object_id = OBJECT_ID(N'SubsidiaryAccounts'))
                    CREATE UNIQUE INDEX [IX_SubsidiaryAccounts_CompanyId_AccountNo]
                        ON [SubsidiaryAccounts] ([CompanyId], [AccountNo]);
                """);

            // Nullable first — platform rows get CompanyId via clone SQL below.
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ControlAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                -- Snapshot platform controls, clone per tenant, remap subsidiaries (avoid self-insert lock).
                IF OBJECT_ID('tempdb..#PlatformControls') IS NOT NULL DROP TABLE #PlatformControls;

                SELECT
                    c.[Id] AS [OldId],
                    c.[MasterAccountId],
                    c.[Code],
                    c.[Title],
                    c.[ControlKind],
                    c.[IsActive]
                INTO #PlatformControls
                FROM [ControlAccounts] c
                WHERE c.[CompanyId] IS NULL;

                INSERT INTO [ControlAccounts] (
                    [CompanyId], [MasterAccountId], [Code], [Title], [ControlKind],
                    [IsActive], [InsertDate], [InsertUserId], [UpdateDate], [UpdateUserId])
                SELECT
                    t.[Id],
                    p.[MasterAccountId],
                    p.[Code],
                    p.[Title],
                    p.[ControlKind],
                    p.[IsActive],
                    SYSUTCDATETIME(),
                    1,
                    NULL,
                    NULL
                FROM #PlatformControls p
                CROSS JOIN [Tenants] t
                WHERE NOT EXISTS (
                    SELECT 1 FROM [ControlAccounts] x
                    WHERE x.[CompanyId] = t.[Id] AND x.[Code] = p.[Code]);

                UPDATE sa
                SET sa.[ControlAccountId] = x.[Id]
                FROM [SubsidiaryAccounts] sa
                INNER JOIN #PlatformControls p ON p.[OldId] = sa.[ControlAccountId]
                INNER JOIN [ControlAccounts] x
                    ON x.[CompanyId] = sa.[CompanyId]
                   AND x.[Code] = p.[Code];

                DELETE c
                FROM [ControlAccounts] c
                INNER JOIN #PlatformControls p ON p.[OldId] = c.[Id];

                DROP TABLE #PlatformControls;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "ControlAccounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NtnNo = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    VendorPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RepresentativeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RepresentativeNo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubsidiaryAccountId = table.Column<int>(type: "int", nullable: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vendors_SubsidiaryAccounts_SubsidiaryAccountId",
                        column: x => x.SubsidiaryAccountId,
                        principalTable: "SubsidiaryAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vendors_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlAccounts_CompanyId_Code",
                table: "ControlAccounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlAccounts_CompanyId_ControlKind",
                table: "ControlAccounts",
                columns: new[] { "CompanyId", "ControlKind" },
                unique: true,
                filter: "[ControlKind] IN (N'INVENTORY', N'ACCOUNTS_PAYABLE')");

            migrationBuilder.CreateIndex(
                name: "IX_ControlAccounts_CompanyId_Id",
                table: "ControlAccounts",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_Id",
                table: "Vendors",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId_IsActive_Id",
                table: "Vendors",
                columns: new[] { "CompanyId", "IsActive", "Id" })
                .Annotation("SqlServer:Include", new[] { "VendorName", "NtnNo", "VendorPhone", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_SubsidiaryAccountId",
                table: "Vendors",
                column: "SubsidiaryAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_Vendors_CompanyId_NtnNo",
                table: "Vendors",
                columns: new[] { "CompanyId", "NtnNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ControlAccounts_Tenants_CompanyId",
                table: "ControlAccounts",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ControlAccounts_Tenants_CompanyId",
                table: "ControlAccounts");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_ControlAccounts_CompanyId_Code",
                table: "ControlAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ControlAccounts_CompanyId_ControlKind",
                table: "ControlAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ControlAccounts_CompanyId_Id",
                table: "ControlAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ControlAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                table: "SubsidiaryAccounts",
                type: "char(12)",
                fixedLength: true,
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_SubsidiaryAccounts_AccountNo",
                table: "SubsidiaryAccounts",
                column: "AccountNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlAccounts_Code",
                table: "ControlAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlAccounts_ControlKind",
                table: "ControlAccounts",
                column: "ControlKind",
                unique: true,
                filter: "[ControlKind] IN (N'INVENTORY', N'ACCOUNTS_PAYABLE')");
        }
    }
}
