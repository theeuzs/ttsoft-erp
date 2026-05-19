using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixConsolidado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── TenantId em todas as tabelas principais ───────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Products ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Customers' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Customers ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Sales' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Sales ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Users' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Users ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Roles' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Roles ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Permissions' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Permissions ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Caixas' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Caixas ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Orcamentos' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Orcamentos ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ContasReceber' AND COLUMN_NAME='TenantId')
                    ALTER TABLE ContasReceber ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ContasPagar' AND COLUMN_NAME='TenantId')
                    ALTER TABLE ContasPagar ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PedidosCompra' AND COLUMN_NAME='TenantId')
                    ALTER TABLE PedidosCompra ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SaleItems' AND COLUMN_NAME='TenantId')
                    ALTER TABLE SaleItems ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SalePayment' AND COLUMN_NAME='TenantId')
                    ALTER TABLE SalePayment ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CaixaMovimentos' AND COLUMN_NAME='TenantId')
                    ALTER TABLE CaixaMovimentos ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='OrcamentoItens' AND COLUMN_NAME='TenantId')
                    ALTER TABLE OrcamentoItens ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PedidoCompraItens' AND COLUMN_NAME='TenantId')
                    ALTER TABLE PedidoCompraItens ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='AuditLogs' AND COLUMN_NAME='TenantId')
                    ALTER TABLE AuditLogs ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Categories' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Categories ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Brands' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Brands ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Suppliers' AND COLUMN_NAME='TenantId')
                    ALTER TABLE Suppliers ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            // ── Colunas de atacado em Products ────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='WholesaleMinQuantity')
                    ALTER TABLE Products ADD WholesaleMinQuantity DECIMAL(18,4) NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Products' AND COLUMN_NAME='WholesalePrice')
                    ALTER TABLE Products ADD WholesalePrice DECIMAL(18,4) NULL;");

            // ── SaleId em ContasReceber e MovimentosHaver ─────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ContasReceber' AND COLUMN_NAME='SaleId')
                    ALTER TABLE ContasReceber ADD SaleId UNIQUEIDENTIFIER NULL;");

            // ── UsuarioId em Orcamentos ───────────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Orcamentos' AND COLUMN_NAME='UsuarioId')
                    ALTER TABLE Orcamentos ADD UsuarioId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            // ── Tabela MovimentosHaver ────────────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MovimentosHaver')
                BEGIN
                    CREATE TABLE MovimentosHaver (
                        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
                        TenantId        UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                        CustomerId      UNIQUEIDENTIFIER NOT NULL,
                        SaleId          UNIQUEIDENTIFIER NULL,
                        Valor           DECIMAL(18,2)    NOT NULL,
                        Tipo            NVARCHAR(50)     NOT NULL,
                        Descricao       NVARCHAR(500)    NOT NULL,
                        DataMovimento   DATETIME2        NOT NULL,
                        OperadorNome    NVARCHAR(200)    NOT NULL,
                        CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedAt       DATETIME2        NULL,
                        IsDeleted       BIT              NOT NULL DEFAULT 0,
                        CONSTRAINT FK_MovimentosHaver_Customers
                            FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_MovimentosHaver_CustomerId ON MovimentosHaver(CustomerId);
                    CREATE INDEX IX_MovimentosHaver_TenantId   ON MovimentosHaver(TenantId);
                END");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='MovimentosHaver')
                AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MovimentosHaver' AND COLUMN_NAME='SaleId')
                    ALTER TABLE MovimentosHaver ADD SaleId UNIQUEIDENTIFIER NULL;");

            // ── Índices de performance ────────────────────────────────────────

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_TenantId')
                    CREATE INDEX IX_Products_TenantId  ON Products(TenantId);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Customers_TenantId')
                    CREATE INDEX IX_Customers_TenantId ON Customers(TenantId);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_TenantId')
                    CREATE INDEX IX_Sales_TenantId     ON Sales(TenantId);");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Users_TenantId')
                    CREATE INDEX IX_Users_TenantId     ON Users(TenantId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down intencional vazio — colunas adicionadas com IF NOT EXISTS
            // são seguras para manter em rollback
        }
    }
}