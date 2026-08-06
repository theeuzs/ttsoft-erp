using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMdeNfeRecebida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "TenantFiscalConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NfesRecebidas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CnpjEmitente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomeEmitente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Versao = table.Column<long>(type: "bigint", nullable: false),
                    StatusManifestacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Importada = table.Column<bool>(type: "bit", nullable: false),
                    DescobertaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfesRecebidas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NfesRecebidas_Chave",
                table: "NfesRecebidas",
                column: "Chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NfesRecebidas");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "TenantFiscalConfigurations");
        }
    }
}
