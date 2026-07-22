using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxasOperadoraRecebimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxaCreditoParceladoPercentual",
                table: "OperadorasRecebimento",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaCreditoVistaPercentual",
                table: "OperadorasRecebimento",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaDebitoPercentual",
                table: "OperadorasRecebimento",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxaCreditoParceladoPercentual",
                table: "OperadorasRecebimento");

            migrationBuilder.DropColumn(
                name: "TaxaCreditoVistaPercentual",
                table: "OperadorasRecebimento");

            migrationBuilder.DropColumn(
                name: "TaxaDebitoPercentual",
                table: "OperadorasRecebimento");
        }
    }
}
