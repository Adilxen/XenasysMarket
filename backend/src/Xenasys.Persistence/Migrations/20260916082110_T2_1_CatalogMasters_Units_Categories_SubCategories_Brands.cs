using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T2_1_CatalogMasters_Units_Categories_SubCategories_Brands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SequenceKey",
                table: "PlatformSequences",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameNormalized = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Brands_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameNormalized = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameNormalized = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubCategories_Tenants_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_CompanyId_Id",
                table: "Brands",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_CompanyId_IsActive_Id",
                table: "Brands",
                columns: new[] { "CompanyId", "IsActive", "Id" })
                .Annotation("SqlServer:Include", new[] { "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_Brands_CompanyId_NameNormalized",
                table: "Brands",
                columns: new[] { "CompanyId", "NameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId_Id",
                table: "Categories",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId_IsActive_Id",
                table: "Categories",
                columns: new[] { "CompanyId", "IsActive", "Id" })
                .Annotation("SqlServer:Include", new[] { "Name", "Code" });

            migrationBuilder.CreateIndex(
                name: "UX_Categories_CompanyId_Code",
                table: "Categories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Categories_CompanyId_NameNormalized",
                table: "Categories",
                columns: new[] { "CompanyId", "NameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CompanyId_CategoryId_IsActive_Id",
                table: "SubCategories",
                columns: new[] { "CompanyId", "CategoryId", "IsActive", "Id" })
                .Annotation("SqlServer:Include", new[] { "Name", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_CompanyId_Id",
                table: "SubCategories",
                columns: new[] { "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_SubCategories_CategoryId_NameNormalized",
                table: "SubCategories",
                columns: new[] { "CategoryId", "NameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SubCategories_CompanyId_Code",
                table: "SubCategories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_IsActive_Id",
                table: "Units",
                columns: new[] { "IsActive", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Units_Code",
                table: "Units",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "SubCategories");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.AlterColumn<string>(
                name: "SequenceKey",
                table: "PlatformSequences",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
