using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransporteEPagamentoNotaAvulsa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EspecieVolumes",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InformacoesComplementares",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalidadeFrete",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PesoBrutoKg",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoLiquidoKg",
                table: "NotasFiscais",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeVolumes",
                table: "NotasFiscais",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraDocumento",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraEndereco",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraIe",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraMunicipio",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraNome",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportadoraUf",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VeiculoPlaca",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VeiculoUf",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotaFiscalPagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaFiscalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormaPagamento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaFiscalPagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaFiscalPagamentos_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalPagamentos_NotaFiscalId",
                table: "NotaFiscalPagamentos",
                column: "NotaFiscalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotaFiscalPagamentos");

            migrationBuilder.DropColumn(
                name: "EspecieVolumes",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "InformacoesComplementares",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "ModalidadeFrete",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "PesoBrutoKg",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "PesoLiquidoKg",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "QuantidadeVolumes",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraDocumento",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraEndereco",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraIe",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraMunicipio",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraNome",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TransportadoraUf",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "VeiculoPlaca",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "VeiculoUf",
                table: "NotasFiscais");
        }
    }
}
