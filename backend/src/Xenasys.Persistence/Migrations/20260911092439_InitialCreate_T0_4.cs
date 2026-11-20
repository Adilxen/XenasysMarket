using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_T0_4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShortName = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false),
                    CompanyNn = table.Column<string>(type: "char(2)", fixedLength: true, maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserNn = table.Column<string>(type: "char(2)", fixedLength: true, maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSuperAdmin = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "control_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MasterAccountId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ControlKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_control_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_control_accounts_master_accounts_MasterAccountId",
                        column: x => x.MasterAccountId,
                        principalTable: "master_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_code_sequences",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_code_sequences", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_product_code_sequences_tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roles_tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subsidiary_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ControlAccountId = table.Column<int>(type: "int", nullable: false),
                    SubsidiaryAccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubsidiaryAccountTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AccountNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProductCode = table.Column<string>(type: "char(16)", fixedLength: true, maxLength: 16, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemGenerated = table.Column<bool>(type: "bit", nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceEntityId = table.Column<int>(type: "int", nullable: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subsidiary_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subsidiary_accounts_control_accounts_ControlAccountId",
                        column: x => x.ControlAccountId,
                        principalTable: "control_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subsidiary_accounts_tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_tenant_memberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tenant_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_tenant_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    SubCategoryId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Packaging = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitOfMeasurement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductCode = table.Column<string>(type: "char(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MinimumStockLevel = table.Column<int>(type: "int", nullable: false),
                    InvoicePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RetailPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubsidiaryAccountId = table.Column<int>(type: "int", nullable: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_products_subsidiary_accounts_SubsidiaryAccountId",
                        column: x => x.SubsidiaryAccountId,
                        principalTable: "subsidiary_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_control_accounts_Code",
                table: "control_accounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_control_accounts_ControlKind",
                table: "control_accounts",
                column: "ControlKind",
                unique: true,
                filter: "[ControlKind] IN (N'INVENTORY', N'ACCOUNTS_PAYABLE')");

            migrationBuilder.CreateIndex(
                name: "IX_control_accounts_MasterAccountId",
                table: "control_accounts",
                column: "MasterAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_master_accounts_Code",
                table: "master_accounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_company_id",
                table: "products",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_SubsidiaryAccountId",
                table: "products",
                column: "SubsidiaryAccountId");

            migrationBuilder.CreateIndex(
                name: "ux_products_company_product_code_covering",
                table: "products",
                columns: new[] { "CompanyId", "ProductCode" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "ProductName", "RetailPrice", "MinimumStockLevel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_roles_Code",
                table: "roles",
                column: "Code",
                unique: true,
                filter: "[CompanyId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_roles_CompanyId_Code",
                table: "roles",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_subsidiary_accounts_CompanyId_AccountNo",
                table: "subsidiary_accounts",
                columns: new[] { "CompanyId", "AccountNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subsidiary_accounts_CompanyId_Id",
                table: "subsidiary_accounts",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_subsidiary_accounts_CompanyId_SubsidiaryAccountCode",
                table: "subsidiary_accounts",
                columns: new[] { "CompanyId", "SubsidiaryAccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subsidiary_accounts_ControlAccountId",
                table: "subsidiary_accounts",
                column: "ControlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Code",
                table: "tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_CompanyId_RoleId",
                table: "user_tenant_memberships",
                columns: new[] { "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_CompanyId_UserId",
                table: "user_tenant_memberships",
                columns: new[] { "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_RoleId",
                table: "user_tenant_memberships",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_UserId",
                table: "user_tenant_memberships",
                column: "UserId",
                unique: true,
                filter: "[CompanyId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_tenant_memberships_UserId_CompanyId",
                table: "user_tenant_memberships",
                columns: new[] { "UserId", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_UserName",
                table: "users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_code_sequences");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "user_tenant_memberships");

            migrationBuilder.DropTable(
                name: "subsidiary_accounts");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "control_accounts");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "master_accounts");
        }
    }
}
