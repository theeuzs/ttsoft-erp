using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TabelaPedidosCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompraItens_PedidosCompra_PedidoCompraId",
                table: "PedidosCompraItens");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidosCompraItens_Products_ProductId",
                table: "PedidosCompraItens");

            // 👇 ==== COMENTADO PARA NÃO DAR ERRO NO BANCO ==== 👇
            // migrationBuilder.DropIndex(
            //     name: "IX_ContasReceber_TenantId",
            //     table: "ContasReceber");

            // migrationBuilder.DropIndex(
            //     name: "IX_ContasPagar_TenantId",
            //     table: "ContasPagar");
            // 👆 ============================================== 👆

            migrationBuilder.DropPrimaryKey(
                name: "PK_PedidosCompraItens",
                table: "PedidosCompraItens");

            migrationBuilder.RenameTable(
                name: "PedidosCompraItens",
                newName: "PedidoCompraItens");

            migrationBuilder.RenameIndex(
                name: "IX_PedidosCompraItens_ProductId",
                table: "PedidoCompraItens",
                newName: "IX_PedidoCompraItens_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_PedidosCompraItens_PedidoCompraId",
                table: "PedidoCompraItens",
                newName: "IX_PedidoCompraItens_PedidoCompraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PedidoCompraItens",
                table: "PedidoCompraItens",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PedidoCompraItens_PedidosCompra_PedidoCompraId",
                table: "PedidoCompraItens",
                column: "PedidoCompraId",
                principalTable: "PedidosCompra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidoCompraItens_Products_ProductId",
                table: "PedidoCompraItens",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PedidoCompraItens_PedidosCompra_PedidoCompraId",
                table: "PedidoCompraItens");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidoCompraItens_Products_ProductId",
                table: "PedidoCompraItens");

            migrationBuilder.DropTable(
                name: "MovimentosHaver");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PedidoCompraItens",
                table: "PedidoCompraItens");

            migrationBuilder.RenameTable(
                name: "PedidoCompraItens",
                newName: "PedidosCompraItens");

            migrationBuilder.RenameIndex(
                name: "IX_PedidoCompraItens_ProductId",
                table: "PedidosCompraItens",
                newName: "IX_PedidosCompraItens_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_PedidoCompraItens_PedidoCompraId",
                table: "PedidosCompraItens",
                newName: "IX_PedidosCompraItens_PedidoCompraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PedidosCompraItens",
                table: "PedidosCompraItens",
                column: "Id");

            // 👇 ==== COMENTADO PARA MANTER A CONSISTÊNCIA ==== 👇
            // migrationBuilder.CreateIndex(
            //     name: "IX_ContasReceber_TenantId",
            //     table: "ContasReceber",
            //     column: "TenantId");

            // migrationBuilder.CreateIndex(
            //     name: "IX_ContasPagar_TenantId",
            //     table: "ContasPagar",
            //     column: "TenantId");
            // 👆 ============================================== 👆

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompraItens_PedidosCompra_PedidoCompraId",
                table: "PedidosCompraItens",
                column: "PedidoCompraId",
                principalTable: "PedidosCompra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidosCompraItens_Products_ProductId",
                table: "PedidosCompraItens",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}