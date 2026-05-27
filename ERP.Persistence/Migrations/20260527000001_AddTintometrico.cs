using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTintometrico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormulasTintometricas",
                columns: table => new
                {
                    Id               = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId        = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fabricante       = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CodigoFabricante = table.Column<string>(type: "nvarchar(50)",  maxLength: 50,  nullable: false),
                    NomeCor          = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Base             = table.Column<string>(type: "nvarchar(50)",  maxLength: 50,  nullable: false),
                    RendimentoM2PorLitro    = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 10m),
                    DemaosRecomendadas      = table.Column<int>(type: "int",          nullable: false, defaultValue: 2),
                    CorantesJson     = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observacoes      = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId         = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt        = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt        = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted        = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormulasTintometricas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormulasTintometricas_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormulasTintometricas_ProductId",
                table: "FormulasTintometricas",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_FormulasTintometricas_TenantId",
                table: "FormulasTintometricas",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FormulasTintometricas");
        }
    }
}
