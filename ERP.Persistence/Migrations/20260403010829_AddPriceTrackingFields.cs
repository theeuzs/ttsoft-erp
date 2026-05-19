using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 👇 Mantemos apenas as 4 colunas novas de preços na tabela Products 👇
            
            migrationBuilder.AddColumn<DateTime>(
                name: "CostPriceChangedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostPriceChangedBy",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SalePriceChangedAt",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalePriceChangedBy",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 👇 Mantemos a capacidade de reverter apenas as 4 colunas 👇
            
            migrationBuilder.DropColumn(
                name: "CostPriceChangedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CostPriceChangedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalePriceChangedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalePriceChangedBy",
                table: "Products");
        }
    }
}