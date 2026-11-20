using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenasys.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T1_34_FormActions_RoleFormActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    ActionId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormActions_Actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormActions_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Module: Form×Action catalog (T1.34) — which actions apply to a form");

            migrationBuilder.CreateTable(
                name: "RoleFormActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    ActionId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    InsertDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsertUserId = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleFormActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleFormActions_Actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleFormActions_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleFormActions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Module: Role×Form×Action grants (T1.34) — company-scoped via Roles.CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FormActions_ActionId",
                table: "FormActions",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormActions_IsActive_FormId",
                table: "FormActions",
                columns: new[] { "IsActive", "FormId" });

            migrationBuilder.CreateIndex(
                name: "UX_FormActions_Form_Action_Active",
                table: "FormActions",
                columns: new[] { "FormId", "ActionId" },
                unique: true,
                filter: "[IsActive] = CAST(1 AS bit)");

            migrationBuilder.CreateIndex(
                name: "IX_RoleFormActions_ActionId",
                table: "RoleFormActions",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleFormActions_FormId",
                table: "RoleFormActions",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleFormActions_IsActive_RoleId",
                table: "RoleFormActions",
                columns: new[] { "IsActive", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "UX_RoleFormActions_Role_Form_Action_Active",
                table: "RoleFormActions",
                columns: new[] { "RoleId", "FormId", "ActionId" },
                unique: true,
                filter: "[IsActive] = CAST(1 AS bit)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormActions");

            migrationBuilder.DropTable(
                name: "RoleFormActions");
        }
    }
}
