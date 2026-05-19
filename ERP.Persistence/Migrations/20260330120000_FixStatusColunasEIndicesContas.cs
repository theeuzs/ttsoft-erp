using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixStatusColunasEIndicesContas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Corrige colunas que ficaram como nvarchar(max) no Azure ──────
            // O snapshot já tem nvarchar(20), mas o banco Azure nunca recebeu
            // o ALTER porque a migration AddEFConfigurations só declarou
            // HasMaxLength depois que as colunas já existiam sem tamanho.

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ContasReceber",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ContasPagar",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // ── Cria os índices compostos (agora que Status tem tamanho fixo) ─
            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_TenantId_Vencimento_Status",
                table: "ContasReceber",
                columns: new[] { "TenantId", "DataVencimento", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_TenantId_Vencimento_Status",
                table: "ContasPagar",
                columns: new[] { "TenantId", "DataVencimento", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContasReceber_TenantId_Vencimento_Status",
                table: "ContasReceber");

            migrationBuilder.DropIndex(
                name: "IX_ContasPagar_TenantId_Vencimento_Status",
                table: "ContasPagar");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ContasReceber",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ContasPagar",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
