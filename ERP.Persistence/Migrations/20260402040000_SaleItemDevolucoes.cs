using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class SaleItemDevolucoes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SaleItemDevolucoes')
                CREATE TABLE SaleItemDevolucoes (
                    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    SaleId              UNIQUEIDENTIFIER NOT NULL,
                    ProductId           UNIQUEIDENTIFIER NOT NULL,
                    ProductName         NVARCHAR(300)    NOT NULL DEFAULT '',
                    QuantidadeDevolvida DECIMAL(18,4)    NOT NULL,
                    ValorDevolvido      DECIMAL(18,2)    NOT NULL,
                    Motivo              NVARCHAR(500)    NULL,
                    OperadorNome        NVARCHAR(200)    NULL,
                    DataDevolucao       DATETIME2        NOT NULL DEFAULT GETDATE(),
                    CreatedAt           DATETIME2        NOT NULL DEFAULT GETDATE(),
                    UpdatedAt           DATETIME2        NOT NULL DEFAULT GETDATE(),
                    IsDeleted           BIT              NOT NULL DEFAULT 0
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SaleItemDevolucoes_SaleId_ProductId')
                    CREATE INDEX IX_SaleItemDevolucoes_SaleId_ProductId
                        ON SaleItemDevolucoes (SaleId, ProductId);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS SaleItemDevolucoes;");
        }
    }
}
