using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class CorrigirTotalItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Corrige TotalItem de vendas antigas usando o Sale.Total proporcional
            // Para vendas com 1 item: TotalItem = Sale.Total exato
            // Para vendas com múltiplos itens: proporcional ao peso de cada item
            migrationBuilder.Sql(@"
                UPDATE si
                SET si.TotalItem = ROUND(
                    s.Total * (si.Quantity * si.UnitPrice) / NULLIF(s.Subtotal, 0)
                , 2)
                FROM SaleItems si
                INNER JOIN Sales s ON s.Id = si.SaleId
                WHERE s.Subtotal > 0;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
