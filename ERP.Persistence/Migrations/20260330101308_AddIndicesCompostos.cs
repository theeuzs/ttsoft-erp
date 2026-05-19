using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicesCompostos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TenantId_SaleDate",
                table: "Sales",
                columns: new[] { "TenantId", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TenantId_SaleDate_Status",
                table: "Sales",
                columns: new[] { "TenantId", "SaleDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductId" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_TenantId_SaleDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TenantId_SaleDate_Status",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_SaleId_ProductId",
                table: "SaleItems");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");
        }
    }
}
