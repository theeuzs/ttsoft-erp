using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerAndShippingToExternalOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuyerNickname",
                table: "ExternalOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingId",
                table: "ExternalOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMode",
                table: "ExternalOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingStatus",
                table: "ExternalOrders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyerNickname",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "ShippingId",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "ShippingMode",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "ShippingStatus",
                table: "ExternalOrders");
        }
    }
}
