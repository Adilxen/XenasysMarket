using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_26_ActionsModule_NameIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                },
                comment: "Module: Name, IsActive + audit (T1.26)");

            migrationBuilder.CreateIndex(
                name: "IX_Actions_IsActive_Name",
                table: "Actions",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_Actions_Name_Active",
                table: "Actions",
                column: "Name",
                unique: true,
                filter: "[IsActive] = CAST(1 AS bit)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");
        }
    }
}
