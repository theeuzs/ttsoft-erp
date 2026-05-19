using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class SaleItemTotalItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='SaleItems' AND COLUMN_NAME='TotalItem')
                    ALTER TABLE SaleItems ADD TotalItem DECIMAL(18,2) NOT NULL DEFAULT 0;");

            // Preenche registros antigos com o valor calculado
            migrationBuilder.Sql(@"
                UPDATE SaleItems SET TotalItem = ROUND(Quantity * UnitPrice * (1 - DiscountPercent / 100), 2)
                WHERE TotalItem = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
