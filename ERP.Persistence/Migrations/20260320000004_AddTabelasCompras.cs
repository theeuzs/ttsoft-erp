using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class AddTabelasCompras : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidosCompra",
                columns: table => new
                {
                    Id              = table.Column<Guid>(nullable: false),
                    Numero          = table.Column<string>(maxLength: 20, nullable: false),
                    SupplierId      = table.Column<Guid>(nullable: true),
                    FornecedorNome  = table.Column<string>(maxLength: 200, nullable: false),
                    DataPedido      = table.Column<DateTime>(nullable: false),
                    DataPrevista    = table.Column<DateTime>(nullable: true),
                    DataRecebimento = table.Column<DateTime>(nullable: true),
                    Status          = table.Column<int>(nullable: false, defaultValue: 0),
                    Observacoes     = table.Column<string>(nullable: true),
                    CriadoPor       = table.Column<string>(nullable: true),
                    TenantId        = table.Column<Guid>(nullable: false),
                    CreatedAt       = table.Column<DateTime>(nullable: false),
                    UpdatedAt       = table.Column<DateTime>(nullable: true),
                    IsDeleted       = table.Column<bool>(nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosCompra_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PedidosCompraItens",
                columns: table => new
                {
                    Id             = table.Column<Guid>(nullable: false),
                    PedidoCompraId = table.Column<Guid>(nullable: false),
                    ProductId      = table.Column<Guid>(nullable: false),
                    ProductName    = table.Column<string>(nullable: false),
                    Quantidade     = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    PrecoUnitario  = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId       = table.Column<Guid>(nullable: false),
                    CreatedAt      = table.Column<DateTime>(nullable: false),
                    UpdatedAt      = table.Column<DateTime>(nullable: true),
                    IsDeleted      = table.Column<bool>(nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosCompraItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosCompraItens_PedidosCompra_PedidoCompraId",
                        column: x => x.PedidoCompraId,
                        principalTable: "PedidosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidosCompraItens_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_SupplierId",
                table: "PedidosCompra",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompraItens_PedidoCompraId",
                table: "PedidosCompraItens",
                column: "PedidoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompraItens_ProductId",
                table: "PedidosCompraItens",
                column: "ProductId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PedidosCompraItens");
            migrationBuilder.DropTable(name: "PedidosCompra");
        }
    }
}