using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Todas as alterações desta migration já existem no banco de produção
            // (ChatMessages, PontosFidelidade, MustChangePassword, LimiteCredito, etc.
            // foram aplicadas manualmente ou por migrations anteriores não rastreadas).
            // O ID foi registrado em __EFMigrationsHistory via INSERT direto.
            // Up() vazio evita erro de "column already exists" ao rodar database update.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormulasTintometricas_Products_ProductId",
                table: "FormulasTintometricas");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "PontosFidelidade");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PercentualComissao",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PrecoBRevendedor",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrecoCAtacadista",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GrupoPreco",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SaldoDevedor",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AsaasPaymentId",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "AsaasStatus",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "BoletoBarCode",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "BoletoUrl",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "InvoiceUrl",
                table: "ContasReceber");

            migrationBuilder.AlterColumn<decimal>(
                name: "RendimentoM2PorLitro",
                table: "FormulasTintometricas",
                type: "decimal(8,2)",
                nullable: false,
                defaultValue: 10m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Observacoes",
                table: "FormulasTintometricas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NomeCor",
                table: "FormulasTintometricas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "FormulasTintometricas",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Fabricante",
                table: "FormulasTintometricas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "DemaosRecomendadas",
                table: "FormulasTintometricas",
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "CodigoFabricante",
                table: "FormulasTintometricas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Base",
                table: "FormulasTintometricas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_FormulasTintometricas_TenantId",
                table: "FormulasTintometricas",
                column: "TenantId");
        }
    }
}