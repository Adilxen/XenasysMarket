using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeColumnNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_control_accounts_master_accounts_MasterAccountId",
                table: "control_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_product_code_sequences_tenants_CompanyId",
                table: "product_code_sequences");

            migrationBuilder.DropForeignKey(
                name: "FK_products_subsidiary_accounts_SubsidiaryAccountId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tenants_CompanyId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_role_permissions_roles_RoleId",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_roles_tenants_CompanyId",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_subsidiary_accounts_control_accounts_ControlAccountId",
                table: "subsidiary_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_subsidiary_accounts_tenants_CompanyId",
                table: "subsidiary_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tenant_memberships_roles_RoleId",
                table: "user_tenant_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tenant_memberships_tenants_CompanyId",
                table: "user_tenant_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_user_tenant_memberships_users_UserId",
                table: "user_tenant_memberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_roles",
                table: "roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_products",
                table: "products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_permissions",
                table: "permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_tenant_memberships",
                table: "user_tenant_memberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subsidiary_accounts",
                table: "subsidiary_accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_role_permissions",
                table: "role_permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_code_sequences",
                table: "product_code_sequences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_master_accounts",
                table: "master_accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_control_accounts",
                table: "control_accounts");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "tenants",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "roles",
                newName: "Roles");

            migrationBuilder.RenameTable(
                name: "products",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "permissions",
                newName: "Permissions");

            migrationBuilder.RenameTable(
                name: "user_tenant_memberships",
                newName: "UserTenantMemberships");

            migrationBuilder.RenameTable(
                name: "subsidiary_accounts",
                newName: "SubsidiaryAccounts");

            migrationBuilder.RenameTable(
                name: "role_permissions",
                newName: "RolePermissions");

            migrationBuilder.RenameTable(
                name: "product_code_sequences",
                newName: "ProductCodeSequences");

            migrationBuilder.RenameTable(
                name: "master_accounts",
                newName: "MasterAccounts");

            migrationBuilder.RenameTable(
                name: "control_accounts",
                newName: "ControlAccounts");

            migrationBuilder.RenameIndex(
                name: "IX_users_UserName",
                table: "Users",
                newName: "IX_Users_UserName");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameColumn(
                name: "tenant_code",
                table: "Tenants",
                newName: "TenantCode");

            migrationBuilder.RenameColumn(
                name: "is_frozen",
                table: "Tenants",
                newName: "IsFrozen");

            migrationBuilder.RenameColumn(
                name: "CompanyNn",
                table: "Tenants",
                newName: "CompanyNo");

            migrationBuilder.RenameIndex(
                name: "ux_tenants_tenant_code",
                table: "Tenants",
                newName: "UX_Tenants_TenantCode");

            migrationBuilder.RenameIndex(
                name: "IX_roles_CompanyId_Code",
                table: "Roles",
                newName: "IX_Roles_CompanyId_Code");

            migrationBuilder.RenameIndex(
                name: "IX_roles_Code",
                table: "Roles",
                newName: "IX_Roles_Code");

            migrationBuilder.RenameIndex(
                name: "IX_products_SubsidiaryAccountId",
                table: "Products",
                newName: "IX_Products_SubsidiaryAccountId");

            migrationBuilder.RenameIndex(
                name: "ux_products_company_product_code_covering",
                table: "Products",
                newName: "UX_Products_CompanyId_ProductCode");

            migrationBuilder.RenameIndex(
                name: "ix_products_company_id",
                table: "Products",
                newName: "IX_Products_CompanyId_Id");

            migrationBuilder.RenameIndex(
                name: "ux_permissions_code",
                table: "Permissions",
                newName: "UX_Permissions_Code");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenant_memberships_UserId_CompanyId",
                table: "UserTenantMemberships",
                newName: "IX_UserTenantMemberships_UserId_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenant_memberships_UserId",
                table: "UserTenantMemberships",
                newName: "IX_UserTenantMemberships_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenant_memberships_RoleId",
                table: "UserTenantMemberships",
                newName: "IX_UserTenantMemberships_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenant_memberships_CompanyId_UserId",
                table: "UserTenantMemberships",
                newName: "IX_UserTenantMemberships_CompanyId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_user_tenant_memberships_CompanyId_RoleId",
                table: "UserTenantMemberships",
                newName: "IX_UserTenantMemberships_CompanyId_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_subsidiary_accounts_ControlAccountId",
                table: "SubsidiaryAccounts",
                newName: "IX_SubsidiaryAccounts_ControlAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_subsidiary_accounts_CompanyId_SubsidiaryAccountCode",
                table: "SubsidiaryAccounts",
                newName: "IX_SubsidiaryAccounts_CompanyId_SubsidiaryAccountCode");

            migrationBuilder.RenameIndex(
                name: "IX_subsidiary_accounts_CompanyId_Id",
                table: "SubsidiaryAccounts",
                newName: "IX_SubsidiaryAccounts_CompanyId_Id");

            migrationBuilder.RenameIndex(
                name: "IX_subsidiary_accounts_CompanyId_AccountNo",
                table: "SubsidiaryAccounts",
                newName: "IX_SubsidiaryAccounts_CompanyId_AccountNo");

            migrationBuilder.RenameIndex(
                name: "IX_role_permissions_PermissionId",
                table: "RolePermissions",
                newName: "IX_RolePermissions_PermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_master_accounts_Code",
                table: "MasterAccounts",
                newName: "IX_MasterAccounts_Code");

            migrationBuilder.RenameIndex(
                name: "IX_control_accounts_MasterAccountId",
                table: "ControlAccounts",
                newName: "IX_ControlAccounts_MasterAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_control_accounts_ControlKind",
                table: "ControlAccounts",
                newName: "IX_ControlAccounts_ControlKind");

            migrationBuilder.RenameIndex(
                name: "IX_control_accounts_Code",
                table: "ControlAccounts",
                newName: "IX_ControlAccounts_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Permissions",
                table: "Permissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserTenantMemberships",
                table: "UserTenantMemberships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubsidiaryAccounts",
                table: "SubsidiaryAccounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductCodeSequences",
                table: "ProductCodeSequences",
                column: "CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MasterAccounts",
                table: "MasterAccounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ControlAccounts",
                table: "ControlAccounts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ControlAccounts_MasterAccounts_MasterAccountId",
                table: "ControlAccounts",
                column: "MasterAccountId",
                principalTable: "MasterAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCodeSequences_Tenants_CompanyId",
                table: "ProductCodeSequences",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_SubsidiaryAccounts_SubsidiaryAccountId",
                table: "Products",
                column: "SubsidiaryAccountId",
                principalTable: "SubsidiaryAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Tenants_CompanyId",
                table: "Products",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Tenants_CompanyId",
                table: "Roles",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubsidiaryAccounts_ControlAccounts_ControlAccountId",
                table: "SubsidiaryAccounts",
                column: "ControlAccountId",
                principalTable: "ControlAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubsidiaryAccounts_Tenants_CompanyId",
                table: "SubsidiaryAccounts",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantMemberships_Roles_RoleId",
                table: "UserTenantMemberships",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantMemberships_Tenants_CompanyId",
                table: "UserTenantMemberships",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantMemberships_Users_UserId",
                table: "UserTenantMemberships",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ControlAccounts_MasterAccounts_MasterAccountId",
                table: "ControlAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCodeSequences_Tenants_CompanyId",
                table: "ProductCodeSequences");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_SubsidiaryAccounts_SubsidiaryAccountId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Tenants_CompanyId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Tenants_CompanyId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_SubsidiaryAccounts_ControlAccounts_ControlAccountId",
                table: "SubsidiaryAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_SubsidiaryAccounts_Tenants_CompanyId",
                table: "SubsidiaryAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantMemberships_Roles_RoleId",
                table: "UserTenantMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantMemberships_Tenants_CompanyId",
                table: "UserTenantMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantMemberships_Users_UserId",
                table: "UserTenantMemberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Permissions",
                table: "Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserTenantMemberships",
                table: "UserTenantMemberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubsidiaryAccounts",
                table: "SubsidiaryAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductCodeSequences",
                table: "ProductCodeSequences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MasterAccounts",
                table: "MasterAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ControlAccounts",
                table: "ControlAccounts");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "tenants");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "roles");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "products");

            migrationBuilder.RenameTable(
                name: "Permissions",
                newName: "permissions");

            migrationBuilder.RenameTable(
                name: "UserTenantMemberships",
                newName: "user_tenant_memberships");

            migrationBuilder.RenameTable(
                name: "SubsidiaryAccounts",
                newName: "subsidiary_accounts");

            migrationBuilder.RenameTable(
                name: "RolePermissions",
                newName: "role_permissions");

            migrationBuilder.RenameTable(
                name: "ProductCodeSequences",
                newName: "product_code_sequences");

            migrationBuilder.RenameTable(
                name: "MasterAccounts",
                newName: "master_accounts");

            migrationBuilder.RenameTable(
                name: "ControlAccounts",
                newName: "control_accounts");

            migrationBuilder.RenameIndex(
                name: "IX_Users_UserName",
                table: "users",
                newName: "IX_users_UserName");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "users",
                newName: "IX_users_Email");

            migrationBuilder.RenameColumn(
                name: "TenantCode",
                table: "tenants",
                newName: "tenant_code");

            migrationBuilder.RenameColumn(
                name: "IsFrozen",
                table: "tenants",
                newName: "is_frozen");

            migrationBuilder.RenameColumn(
                name: "CompanyNo",
                table: "tenants",
                newName: "CompanyNn");

            migrationBuilder.RenameIndex(
                name: "UX_Tenants_TenantCode",
                table: "tenants",
                newName: "ux_tenants_tenant_code");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_CompanyId_Code",
                table: "roles",
                newName: "IX_roles_CompanyId_Code");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_Code",
                table: "roles",
                newName: "IX_roles_Code");

            migrationBuilder.RenameIndex(
                name: "IX_Products_SubsidiaryAccountId",
                table: "products",
                newName: "IX_products_SubsidiaryAccountId");

            migrationBuilder.RenameIndex(
                name: "UX_Products_CompanyId_ProductCode",
                table: "products",
                newName: "ux_products_company_product_code_covering");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CompanyId_Id",
                table: "products",
                newName: "ix_products_company_id");

            migrationBuilder.RenameIndex(
                name: "UX_Permissions_Code",
                table: "permissions",
                newName: "ux_permissions_code");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantMemberships_UserId_CompanyId",
                table: "user_tenant_memberships",
                newName: "IX_user_tenant_memberships_UserId_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantMemberships_UserId",
                table: "user_tenant_memberships",
                newName: "IX_user_tenant_memberships_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantMemberships_RoleId",
                table: "user_tenant_memberships",
                newName: "IX_user_tenant_memberships_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantMemberships_CompanyId_UserId",
                table: "user_tenant_memberships",
                newName: "IX_user_tenant_memberships_CompanyId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantMemberships_CompanyId_RoleId",
                table: "user_tenant_memberships",
                newName: "IX_user_tenant_memberships_CompanyId_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_SubsidiaryAccounts_ControlAccountId",
                table: "subsidiary_accounts",
                newName: "IX_subsidiary_accounts_ControlAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_SubsidiaryAccounts_CompanyId_SubsidiaryAccountCode",
                table: "subsidiary_accounts",
                newName: "IX_subsidiary_accounts_CompanyId_SubsidiaryAccountCode");

            migrationBuilder.RenameIndex(
                name: "IX_SubsidiaryAccounts_CompanyId_Id",
                table: "subsidiary_accounts",
                newName: "IX_subsidiary_accounts_CompanyId_Id");

            migrationBuilder.RenameIndex(
                name: "IX_SubsidiaryAccounts_CompanyId_AccountNo",
                table: "subsidiary_accounts",
                newName: "IX_subsidiary_accounts_CompanyId_AccountNo");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "role_permissions",
                newName: "IX_role_permissions_PermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_MasterAccounts_Code",
                table: "master_accounts",
                newName: "IX_master_accounts_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ControlAccounts_MasterAccountId",
                table: "control_accounts",
                newName: "IX_control_accounts_MasterAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ControlAccounts_ControlKind",
                table: "control_accounts",
                newName: "IX_control_accounts_ControlKind");

            migrationBuilder.RenameIndex(
                name: "IX_ControlAccounts_Code",
                table: "control_accounts",
                newName: "IX_control_accounts_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                table: "tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_roles",
                table: "roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_products",
                table: "products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_permissions",
                table: "permissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_tenant_memberships",
                table: "user_tenant_memberships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subsidiary_accounts",
                table: "subsidiary_accounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_role_permissions",
                table: "role_permissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_code_sequences",
                table: "product_code_sequences",
                column: "CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_master_accounts",
                table: "master_accounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_control_accounts",
                table: "control_accounts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_control_accounts_master_accounts_MasterAccountId",
                table: "control_accounts",
                column: "MasterAccountId",
                principalTable: "master_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_code_sequences_tenants_CompanyId",
                table: "product_code_sequences",
                column: "CompanyId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_subsidiary_accounts_SubsidiaryAccountId",
                table: "products",
                column: "SubsidiaryAccountId",
                principalTable: "subsidiary_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tenants_CompanyId",
                table: "products",
                column: "CompanyId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions",
                column: "PermissionId",
                principalTable: "permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_role_permissions_roles_RoleId",
                table: "role_permissions",
                column: "RoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_tenants_CompanyId",
                table: "roles",
                column: "CompanyId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subsidiary_accounts_control_accounts_ControlAccountId",
                table: "subsidiary_accounts",
                column: "ControlAccountId",
                principalTable: "control_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subsidiary_accounts_tenants_CompanyId",
                table: "subsidiary_accounts",
                column: "CompanyId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenant_memberships_roles_RoleId",
                table: "user_tenant_memberships",
                column: "RoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenant_memberships_tenants_CompanyId",
                table: "user_tenant_memberships",
                column: "CompanyId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_tenant_memberships_users_UserId",
                table: "user_tenant_memberships",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
