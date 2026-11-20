using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations;

    /// <inheritdoc />
    public partial class T1_28_Roles_RestoreCompanyId_ColumnOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        // Separate batches: SQL Server compiles whole batch against current schema
        // (CompanyId indexes cannot be in the same batch as the rebuild CREATE).
        migrationBuilder.Sql(
            """
            ALTER TABLE [RolePermissions] DROP CONSTRAINT [FK_RolePermissions_Roles_RoleId];
            ALTER TABLE [UserTenantMemberships] DROP CONSTRAINT [FK_UserTenantMemberships_Roles_RoleId];

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Roles_IsActive_Name' AND object_id = OBJECT_ID(N'dbo.Roles'))
                DROP INDEX [IX_Roles_IsActive_Name] ON [Roles];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Roles_RoleCode' AND object_id = OBJECT_ID(N'dbo.Roles'))
                DROP INDEX [UX_Roles_RoleCode] ON [Roles];
            """);

        migrationBuilder.Sql(
            """
            DECLARE @masterCompanyId int = (SELECT TOP (1) [Id] FROM [Tenants] ORDER BY [Id]);

            CREATE TABLE [Roles_Rebuild] (
                [Id] int NOT NULL IDENTITY(1,1),
                [CompanyId] int NULL,
                [Name] nvarchar(100) NOT NULL,
                [RoleCode] nvarchar(50) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_Roles_Rebuild_IsActive] DEFAULT CAST(1 AS bit),
                [InsertDate] datetime2 NOT NULL,
                [InsertUserId] int NOT NULL,
                [UpdateDate] datetime2 NULL,
                [UpdateUserId] int NULL,
                CONSTRAINT [PK_Roles_Rebuild] PRIMARY KEY ([Id])
            );

            SET IDENTITY_INSERT [Roles_Rebuild] ON;
            INSERT INTO [Roles_Rebuild] (
                [Id], [CompanyId], [Name], [RoleCode], [Description], [IsActive],
                [InsertDate], [InsertUserId], [UpdateDate], [UpdateUserId])
            SELECT
                [Id],
                CASE
                    WHEN [RoleCode] = N'SUPER_ADMIN' THEN NULL
                    ELSE @masterCompanyId
                END,
                [Name],
                [RoleCode],
                [Description],
                [IsActive],
                [InsertDate],
                [InsertUserId],
                [UpdateDate],
                [UpdateUserId]
            FROM [Roles];
            SET IDENTITY_INSERT [Roles_Rebuild] OFF;

            DROP TABLE [Roles];
            EXEC sp_rename N'Roles_Rebuild', N'Roles';
            EXEC sp_rename N'PK_Roles_Rebuild', N'PK_Roles', N'OBJECT';
            EXEC sp_rename N'DF_Roles_Rebuild_IsActive', N'DF_Roles_IsActive', N'OBJECT';
            """);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX [UX_Roles_RoleCode_Platform]
                ON [Roles] ([RoleCode])
                WHERE [CompanyId] IS NULL;

            CREATE UNIQUE INDEX [UX_Roles_CompanyId_RoleCode]
                ON [Roles] ([CompanyId], [RoleCode])
                WHERE [CompanyId] IS NOT NULL;

            CREATE INDEX [IX_Roles_CompanyId_IsActive_Name]
                ON [Roles] ([CompanyId], [IsActive], [Name])
                INCLUDE ([RoleCode], [Description]);

            DECLARE @maxId int = (SELECT ISNULL(MAX([Id]), 0) FROM [Roles]);
            DBCC CHECKIDENT (N'Roles', RESEED, @maxId);

            ALTER TABLE [Roles] WITH CHECK ADD CONSTRAINT [FK_Roles_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;

            ALTER TABLE [RolePermissions] WITH CHECK ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId]
                FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE;

            ALTER TABLE [UserTenantMemberships] WITH CHECK ADD CONSTRAINT [FK_UserTenantMemberships_Roles_RoleId]
                FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION;

            IF EXISTS (
                SELECT 1 FROM sys.extended_properties
                WHERE major_id = OBJECT_ID(N'dbo.Roles') AND name = N'MS_Description' AND minor_id = 0)
                EXEC sp_updateextendedproperty
                    @name = N'MS_Description',
                    @value = N'Module: CompanyId, Name, RoleCode, Description, IsActive + audit (T1.28 BRD company-scoped)',
                    @level0type = N'SCHEMA', @level0name = N'dbo',
                    @level1type = N'TABLE',  @level1name = N'Roles';
            ELSE
                EXEC sp_addextendedproperty
                    @name = N'MS_Description',
                    @value = N'Module: CompanyId, Name, RoleCode, Description, IsActive + audit (T1.28 BRD company-scoped)',
                    @level0type = N'SCHEMA', @level0name = N'dbo',
                    @level1type = N'TABLE',  @level1name = N'Roles';
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        // Irreversible rebuild (CompanyId restored + column order).
    }
}
