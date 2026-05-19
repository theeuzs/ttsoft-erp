using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class MetroLinearConversaoUnidade : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='UnidadeEstoque')
                    ALTER TABLE Products ADD UnidadeEstoque NVARCHAR(10) NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='UnidadeVenda')
                    ALTER TABLE Products ADD UnidadeVenda NVARCHAR(10) NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='FatorConversao')
                    ALTER TABLE Products ADD FatorConversao DECIMAL(18,4) NOT NULL DEFAULT 1;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='LabelUnidadeVenda')
                    ALTER TABLE Products ADD LabelUnidadeVenda NVARCHAR(30) NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
