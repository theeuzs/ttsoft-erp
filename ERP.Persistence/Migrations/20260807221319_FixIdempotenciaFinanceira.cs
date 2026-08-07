using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixIdempotenciaFinanceira : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalePaymentId",
                table: "RecebiveisOperadora",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalePaymentId",
                table: "MovimentosContaBancaria",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalePaymentId",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalePaymentId",
                table: "CaixaMovimentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendaId",
                table: "CaixaMovimentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecebiveisOperadora_SalePaymentId",
                table: "RecebiveisOperadora",
                column: "SalePaymentId",
                unique: true,
                filter: "[SalePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosContaBancaria_SalePaymentId_Tipo",
                table: "MovimentosContaBancaria",
                columns: new[] { "SalePaymentId", "Tipo" },
                unique: true,
                filter: "[SalePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_SalePaymentId",
                table: "ContasReceber",
                column: "SalePaymentId",
                unique: true,
                filter: "[SalePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaixaMovimentos_SalePaymentId",
                table: "CaixaMovimentos",
                column: "SalePaymentId",
                unique: true,
                filter: "[SalePaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecebiveisOperadora_SalePaymentId",
                table: "RecebiveisOperadora");

            migrationBuilder.DropIndex(
                name: "IX_MovimentosContaBancaria_SalePaymentId_Tipo",
                table: "MovimentosContaBancaria");

            migrationBuilder.DropIndex(
                name: "IX_ContasReceber_SalePaymentId",
                table: "ContasReceber");

            migrationBuilder.DropIndex(
                name: "IX_CaixaMovimentos_SalePaymentId",
                table: "CaixaMovimentos");

            migrationBuilder.DropColumn(
                name: "SalePaymentId",
                table: "RecebiveisOperadora");

            migrationBuilder.DropColumn(
                name: "SalePaymentId",
                table: "MovimentosContaBancaria");

            migrationBuilder.DropColumn(
                name: "SalePaymentId",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "SalePaymentId",
                table: "CaixaMovimentos");

            migrationBuilder.DropColumn(
                name: "VendaId",
                table: "CaixaMovimentos");
        }
    }
}
