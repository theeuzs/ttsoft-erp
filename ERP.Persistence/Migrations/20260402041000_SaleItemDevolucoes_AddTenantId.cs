using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class SaleItemDevolucoes_AddTenantId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='SaleItemDevolucoes' AND COLUMN_NAME='TenantId')
                    ALTER TABLE SaleItemDevolucoes ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
