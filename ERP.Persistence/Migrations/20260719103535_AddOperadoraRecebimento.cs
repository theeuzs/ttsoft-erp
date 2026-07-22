using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperadoraRecebimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperadorasRecebimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrazoDebitoDias = table.Column<int>(type: "int", nullable: false),
                    PrazoCreditoVistaDias = table.Column<int>(type: "int", nullable: false),
                    PrazoCreditoParceladoDias = table.Column<int>(type: "int", nullable: false),
                    AntecipacaoAutomatica = table.Column<bool>(type: "bit", nullable: false),
                    ContaDestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAtiva = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperadorasRecebimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperadorasRecebimento_ContasBancarias_ContaDestinoId",
                        column: x => x.ContaDestinoId,
                        principalTable: "ContasBancarias",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperadorasRecebimento_ContaDestinoId",
                table: "OperadorasRecebimento",
                column: "ContaDestinoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperadorasRecebimento");
        }
    }
}
