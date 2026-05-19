using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class AddProdutosAgregados : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProdutosAgregados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoRelacionadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutosAgregados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutosAgregados_Products_ProdutoPrincipalId",
                        column: x => x.ProdutoPrincipalId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProdutosAgregados_Products_ProdutoRelacionadoId",
                        column: x => x.ProdutoRelacionadoId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosAgregados_ProdutoPrincipalId_ProdutoRelacionadoId",
                table: "ProdutosAgregados",
                columns: new[] { "ProdutoPrincipalId", "ProdutoRelacionadoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosAgregados_ProdutoRelacionadoId",
                table: "ProdutosAgregados",
                column: "ProdutoRelacionadoId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProdutosAgregados");
        }
    }
}