using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecebivelOperadora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OperadoraPadrao",
                table: "OperadorasRecebimento",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RecebiveisOperadora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperadoraRecebimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FormaPagamento = table.Column<int>(type: "int", nullable: false),
                    ValorBruto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTaxa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataVenda = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPrevistaLiquidacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataLiquidacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MovimentoContaBancariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebiveisOperadora", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecebiveisOperadora_OperadorasRecebimento_OperadoraRecebimentoId",
                        column: x => x.OperadoraRecebimentoId,
                        principalTable: "OperadorasRecebimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecebiveisOperadora_OperadoraRecebimentoId",
                table: "RecebiveisOperadora",
                column: "OperadoraRecebimentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecebiveisOperadora");

            migrationBuilder.DropColumn(
                name: "OperadoraPadrao",
                table: "OperadorasRecebimento");
        }
    }
}
