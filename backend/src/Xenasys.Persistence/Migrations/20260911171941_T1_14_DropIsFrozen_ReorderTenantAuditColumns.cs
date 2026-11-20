using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations;

    /// <inheritdoc />
    public partial class T1_14_DropIsFrozen_ReorderTenantAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        // SQL Server cannot reorder columns in-place — rebuild Tenants with audit cols last.
        migrationBuilder.Sql(
            """
            ALTER TABLE [ProductCodeSequences] DROP CONSTRAINT [FK_ProductCodeSequences_Tenants_CompanyId];
            ALTER TABLE [Products] DROP CONSTRAINT [FK_Products_Tenants_CompanyId];
            ALTER TABLE [Roles] DROP CONSTRAINT [FK_Roles_Tenants_CompanyId];
            ALTER TABLE [SubsidiaryAccounts] DROP CONSTRAINT [FK_SubsidiaryAccounts_Tenants_CompanyId];
            ALTER TABLE [UserTenantMemberships] DROP CONSTRAINT [FK_UserTenantMemberships_Tenants_CompanyId];

            CREATE TABLE [Tenants_Rebuild] (
                [Id] int NOT NULL IDENTITY(1,1),
                [Name] nvarchar(200) NOT NULL,
                [TenantCode] char(6) NOT NULL,
                [IsActive] bit NOT NULL,
                [ContactEmail] nvarchar(256) NULL,
                [ContactPhone] nvarchar(30) NULL,
                [Address] nvarchar(500) NULL,
                [InsertDate] datetime2 NOT NULL,
                [InsertUserId] int NOT NULL,
                [UpdateDate] datetime2 NULL,
                [UpdateUserId] int NULL,
                CONSTRAINT [PK_Tenants_Rebuild] PRIMARY KEY ([Id])
            );

            SET IDENTITY_INSERT [Tenants_Rebuild] ON;
            INSERT INTO [Tenants_Rebuild] (
                [Id], [Name], [TenantCode], [IsActive],
                [ContactEmail], [ContactPhone], [Address],
                [InsertDate], [InsertUserId], [UpdateDate], [UpdateUserId])
            SELECT
                [Id], [Name], [TenantCode], [IsActive],
                [ContactEmail], [ContactPhone], [Address],
                [InsertDate], [InsertUserId], [UpdateDate], [UpdateUserId]
            FROM [Tenants];
            SET IDENTITY_INSERT [Tenants_Rebuild] OFF;

            DROP TABLE [Tenants];
            EXEC sp_rename N'Tenants_Rebuild', N'Tenants';
            EXEC sp_rename N'PK_Tenants_Rebuild', N'PK_Tenants', N'OBJECT';

            CREATE UNIQUE INDEX [UX_Tenants_TenantCode] ON [Tenants] ([TenantCode]);

            DECLARE @maxId int = (SELECT ISNULL(MAX([Id]), 0) FROM [Tenants]);
            DBCC CHECKIDENT (N'Tenants', RESEED, @maxId);

            ALTER TABLE [ProductCodeSequences] WITH CHECK ADD CONSTRAINT [FK_ProductCodeSequences_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
            ALTER TABLE [Products] WITH CHECK ADD CONSTRAINT [FK_Products_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
            ALTER TABLE [Roles] WITH CHECK ADD CONSTRAINT [FK_Roles_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
            ALTER TABLE [SubsidiaryAccounts] WITH CHECK ADD CONSTRAINT [FK_SubsidiaryAccounts_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
            ALTER TABLE [UserTenantMemberships] WITH CHECK ADD CONSTRAINT [FK_UserTenantMemberships_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);
    }
}
