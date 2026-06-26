using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlargaNumeroPedidoCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterColumn<string>(
        name:      "Numero",
        table:     "PedidosCompra",
        maxLength: 50,
        nullable:  false,
        oldMaxLength: 20,
        oldNullable: false);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterColumn<string>(
        name:      "Numero",
        table:     "PedidosCompra",
        maxLength: 20,
        nullable:  false,
        oldMaxLength: 50,
        oldNullable: false);
}
    }
}
