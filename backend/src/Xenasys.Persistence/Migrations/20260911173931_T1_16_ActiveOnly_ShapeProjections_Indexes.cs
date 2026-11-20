using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_16_ActiveOnly_ShapeProjections_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive_Name",
                table: "Tenants",
                columns: new[] { "IsActive", "Name" })
                .Annotation("SqlServer:Include", new[] { "TenantCode", "ContactEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_IsActive_Id",
                table: "Products",
                columns: new[] { "CompanyId", "IsActive", "Id" })
                .Annotation("SqlServer:Include", new[] { "ProductCode", "ProductName", "RetailPrice", "MinimumStockLevel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsActive_Name",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId_IsActive_Id",
                table: "Products");
        }
    }
}
