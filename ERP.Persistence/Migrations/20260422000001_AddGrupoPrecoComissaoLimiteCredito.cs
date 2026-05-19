using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class AddGrupoPrecoComissaoLimiteCredito : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Sprint C: Preços por grupo no produto ──────────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "PrecoBRevendedor",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoCAtacadista",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // ── Sprint C + D: Grupo de preço e crediário no cliente ───────────
            migrationBuilder.AddColumn<int>(
                name: "GrupoPreco",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0); // 0 = GrupoPreco.A (Varejo)

            migrationBuilder.AddColumn<decimal>(
                name: "LimiteCredito",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoDevedor",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // ── Sprint E: Percentual de comissão no cargo ─────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "PercentualComissao",
                table: "Roles",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            // Índice para buscas por grupo de preço (ex: listar todos do Grupo B)
            migrationBuilder.CreateIndex(
                name: "IX_Customers_GrupoPreco",
                table: "Customers",
                column: "GrupoPreco");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_Customers_GrupoPreco", "Customers");

            migrationBuilder.DropColumn("PrecoBRevendedor",    "Products");
            migrationBuilder.DropColumn("PrecoCAtacadista",    "Products");
            migrationBuilder.DropColumn("GrupoPreco",          "Customers");
            migrationBuilder.DropColumn("LimiteCredito",       "Customers");
            migrationBuilder.DropColumn("SaldoDevedor",        "Customers");
            migrationBuilder.DropColumn("PercentualComissao",  "Roles");
        }
    }
}
