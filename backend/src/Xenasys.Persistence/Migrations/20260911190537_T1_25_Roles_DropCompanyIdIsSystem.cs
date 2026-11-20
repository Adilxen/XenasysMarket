using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_25_Roles_DropCompanyIdIsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Tenants_CompanyId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CompanyId_IsActive_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_CompanyId_RoleCode",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_RoleCode",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Roles");

            migrationBuilder.AlterTable(
                name: "Roles",
                comment: "Module: Name, RoleCode, Description, IsActive + audit (T1.25)",
                oldComment: "Module: Name, RoleCode, Description, IsActive + audit (T1.24)");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsActive_Name",
                table: "Roles",
                columns: new[] { "IsActive", "Name" })
                .Annotation("SqlServer:Include", new[] { "RoleCode", "Description" });

            migrationBuilder.CreateIndex(
                name: "UX_Roles_RoleCode",
                table: "Roles",
                column: "RoleCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_IsActive_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UX_Roles_RoleCode",
                table: "Roles");

            migrationBuilder.AlterTable(
                name: "Roles",
                comment: "Module: Name, RoleCode, Description, IsActive + audit (T1.24)",
                oldComment: "Module: Name, RoleCode, Description, IsActive + audit (T1.25)");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CompanyId_IsActive_Name",
                table: "Roles",
                columns: new[] { "CompanyId", "IsActive", "Name" })
                .Annotation("SqlServer:Include", new[] { "RoleCode", "Description", "IsSystem" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CompanyId_RoleCode",
                table: "Roles",
                columns: new[] { "CompanyId", "RoleCode" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleCode",
                table: "Roles",
                column: "RoleCode",
                unique: true,
                filter: "[CompanyId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Tenants_CompanyId",
                table: "Roles",
                column: "CompanyId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
