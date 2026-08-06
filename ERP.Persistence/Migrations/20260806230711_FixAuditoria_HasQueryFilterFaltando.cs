using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditoria_HasQueryFilterFaltando : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "OrcamentoItens",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OrcamentoItens",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // TenantId NÃO entra aqui — já existe na tabela de verdade (foi
            // adicionado via SQL bruto numa migration antiga, FixConsolidado,
            // sem passar pelo snapshot do EF Core).

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrcamentoItens",
                type: "datetime2",
                nullable: true);

            // Achado mais profundo (06/08/2026): a tabela NfePendentes NUNCA
            // existiu de verdade — numa migration antiga (ImplementacaoRBAC),
            // o CreateTable dela foi comentado manualmente, sem ninguém criar
            // por outro caminho. A feature de contingência fiscal nunca foi
            // exercida na prática (Focus NFe ainda não estava ativo), então
            // isso nunca deu erro até agora. Cria a tabela inteira do zero,
            // já com as colunas de BaseEntity incluídas.
            migrationBuilder.CreateTable(
                name: "NfePendentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoNota = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataFalha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    UltimaMensagemErro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfePendentes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "OrcamentoItens");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OrcamentoItens");

            // TenantId não foi adicionado por essa migration em OrcamentoItens
            // (já existia) — não deve ser removido no Down() dela também.

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "OrcamentoItens");

            migrationBuilder.DropTable(
                name: "NfePendentes");
        }
    }
}