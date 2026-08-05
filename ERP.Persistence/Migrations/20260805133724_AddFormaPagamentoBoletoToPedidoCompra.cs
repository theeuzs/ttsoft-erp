using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormaPagamentoBoletoToPedidoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormaPagamento",
                table: "PedidosCompra",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroBoleto",
                table: "PedidosCompra",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VencimentoBoleto",
                table: "PedidosCompra",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormaPagamento",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "NumeroBoleto",
                table: "PedidosCompra");

            migrationBuilder.DropColumn(
                name: "VencimentoBoleto",
                table: "PedidosCompra");
        }
    }
}
