using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_13_DropTenantShortNameCompanyNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyNo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyNo",
                table: "Tenants",
                type: "char(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Tenants",
                type: "char(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "");
        }
    }
}
