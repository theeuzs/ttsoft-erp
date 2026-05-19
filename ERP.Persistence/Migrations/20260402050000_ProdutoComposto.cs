using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class ProdutoComposto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='ParentProductId')
                    ALTER TABLE Products ADD ParentProductId UNIQUEIDENTIFIER NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME='Products' AND COLUMN_NAME='ConversionFactor')
                    ALTER TABLE Products ADD ConversionFactor DECIMAL(18,6) NOT NULL DEFAULT 1;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Products_Products_ParentProductId')
                    ALTER TABLE Products ADD CONSTRAINT FK_Products_Products_ParentProductId
                        FOREIGN KEY (ParentProductId) REFERENCES Products(Id)
                        ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_ParentProductId')
                    CREATE INDEX IX_Products_ParentProductId ON Products (ParentProductId)
                    WHERE ParentProductId IS NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Products_Products_ParentProductId') ALTER TABLE Products DROP CONSTRAINT FK_Products_Products_ParentProductId;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_ParentProductId') DROP INDEX IX_Products_ParentProductId ON Products;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='ConversionFactor') ALTER TABLE Products DROP COLUMN ConversionFactor;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='ParentProductId') ALTER TABLE Products DROP COLUMN ParentProductId;");
        }
    }
}
