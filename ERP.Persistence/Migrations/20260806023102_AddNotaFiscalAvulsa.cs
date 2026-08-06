using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaFiscalAvulsa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinatarioBairro",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioCep",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioIe",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioLogradouro",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioMunicipio",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioNumero",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinatarioUf",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturezaOperacao",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoOperacaoEntradaSaida",
                table: "NotasFiscais",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "NotaFiscalItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaFiscalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cfop = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaFiscalItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaFiscalItens_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotaFiscalItens_NotaFiscalId",
                table: "NotaFiscalItens",
                column: "NotaFiscalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotaFiscalItens");

            migrationBuilder.DropColumn(
                name: "DestinatarioBairro",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioCep",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioIe",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioLogradouro",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioMunicipio",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioNumero",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "DestinatarioUf",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "NaturezaOperacao",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "TipoOperacaoEntradaSaida",
                table: "NotasFiscais");
        }
    }
}
