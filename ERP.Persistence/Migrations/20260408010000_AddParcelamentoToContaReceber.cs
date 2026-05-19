using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class AddParcelamentoToContaReceber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroParcela",
                table: "ContasReceber",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TotalParcelas",
                table: "ContasReceber",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ParcelamentoId",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaPagamento",
                table: "ContasReceber",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Índice para busca de parcelas por grupo
            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_ParcelamentoId",
                table: "ContasReceber",
                column: "ParcelamentoId",
                filter: "[ParcelamentoId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContasReceber_ParcelamentoId",
                table: "ContasReceber");

            migrationBuilder.DropColumn(name: "NumeroParcela",   table: "ContasReceber");
            migrationBuilder.DropColumn(name: "TotalParcelas",   table: "ContasReceber");
            migrationBuilder.DropColumn(name: "ParcelamentoId",  table: "ContasReceber");
            migrationBuilder.DropColumn(name: "FormaPagamento",  table: "ContasReceber");
        }
    }
}
