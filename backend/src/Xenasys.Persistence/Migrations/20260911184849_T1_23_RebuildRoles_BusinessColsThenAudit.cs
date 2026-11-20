using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations;

/// <inheritdoc />
public partial class T1_23_RebuildRoles_BusinessColsThenAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQL Server cannot reorder columns in-place — rebuild Roles (Company/T1.14 pattern).
        // Order: Id, CompanyId, Code, Name, Description, IsActive, IsSystem, then audit.
        migrationBuilder.Sql(
            """
            ALTER TABLE [RolePermissions] DROP CONSTRAINT [FK_RolePermissions_Roles_RoleId];
            ALTER TABLE [UserTenantMemberships] DROP CONSTRAINT [FK_UserTenantMemberships_Roles_RoleId];
            ALTER TABLE [Roles] DROP CONSTRAINT [FK_Roles_Tenants_CompanyId];

            CREATE TABLE [Roles_Rebuild] (
                [Id] int NOT NULL IDENTITY(1,1),
                [CompanyId] int NULL,
                [Code] nvarchar(50) NOT NULL,
                [Name] nvarchar(100) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_Roles_Rebuild_IsActive] DEFAULT CAST(1 AS bit),
                [IsSystem] bit NOT NULL,
                [InsertDate] datetime2 NOT NULL,
                [InsertUserId] int NOT NULL,
                [UpdateDate] datetime2 NULL,
                [UpdateUserId] int NULL,
                CONSTRAINT [PK_Roles_Rebuild] PRIMARY KEY ([Id])
            );

            SET IDENTITY_INSERT [Roles_Rebuild] ON;
            INSERT INTO [Roles_Rebuild] (
                [Id], [CompanyId], [Code], [Name], [Description], [IsActive], [IsSystem],
                [InsertDate], [InsertUserId], [UpdateDate], [UpdateUserId])
            SELECT
                [Id],
                [CompanyId],
                [Code],
                [Name],
                COALESCE(
                    NULLIF(LTRIM(RTRIM([Description])), N''),
                    CASE [Code]
                        WHEN N'SUPER_ADMIN' THEN N'Platform super administrator'
                        WHEN N'TENANT_ADMIN' THEN N'Company administrator with full tenant RBAC'
                        WHEN N'TENANT_STAFF' THEN N'Operational staff with limited claims'
                        ELSE NULL
                    END),
                ISNULL([IsActive], CAST(1 AS bit)),
                [IsSystem],
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

            CREATE UNIQUE INDEX [IX_Roles_Code]
                ON [Roles] ([Code])
                WHERE [CompanyId] IS NULL;

            CREATE UNIQUE INDEX [IX_Roles_CompanyId_Code]
                ON [Roles] ([CompanyId], [Code])
                WHERE [CompanyId] IS NOT NULL;

            CREATE INDEX [IX_Roles_CompanyId_IsActive_Name]
                ON [Roles] ([CompanyId], [IsActive], [Name])
                INCLUDE ([Code], [Description], [IsSystem]);

            DECLARE @maxId int = (SELECT ISNULL(MAX([Id]), 0) FROM [Roles]);
            DBCC CHECKIDENT (N'Roles', RESEED, @maxId);

            ALTER TABLE [Roles] WITH CHECK ADD CONSTRAINT [FK_Roles_Tenants_CompanyId]
                FOREIGN KEY ([CompanyId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION;

            ALTER TABLE [RolePermissions] WITH CHECK ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId]
                FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE;

            ALTER TABLE [UserTenantMemberships] WITH CHECK ADD CONSTRAINT [FK_UserTenantMemberships_Roles_RoleId]
                FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION;

            EXEC sp_addextendedproperty
                @name = N'MS_Description',
                @value = N'RBAC roles — business cols first; audit last (T1.23)',
                @level0type = N'SCHEMA', @level0name = N'dbo',
                @level1type = N'TABLE',  @level1name = N'Roles';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1 FROM sys.extended_properties
                WHERE major_id = OBJECT_ID(N'dbo.Roles') AND name = N'MS_Description' AND minor_id = 0)
            BEGIN
                EXEC sp_dropextendedproperty
                    @name = N'MS_Description',
                    @level0type = N'SCHEMA', @level0name = N'dbo',
                    @level1type = N'TABLE',  @level1name = N'Roles';
            END
            """);
    }
}
