using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntidadesHerdamBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ══════════════════════════════════════════════════════════════
            // AuditLog herda BaseEntity
            // TenantId já foi adicionado pelo FixConsolidado (IF NOT EXISTS)
            // Adiciona: CreatedAt, UpdatedAt, IsDeleted
            // ══════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='AuditLogs' AND COLUMN_NAME='TenantId')
                    ALTER TABLE AuditLogs ADD TenantId UNIQUEIDENTIFIER NOT NULL 
                        DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='AuditLogs' AND COLUMN_NAME='CreatedAt')
                    ALTER TABLE AuditLogs ADD CreatedAt DATETIME2 NOT NULL 
                        DEFAULT GETUTCDATE();");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='AuditLogs' AND COLUMN_NAME='UpdatedAt')
                    ALTER TABLE AuditLogs ADD UpdatedAt DATETIME2 NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='AuditLogs' AND COLUMN_NAME='IsDeleted')
                    ALTER TABLE AuditLogs ADD IsDeleted BIT NOT NULL DEFAULT 0;");

            // ══════════════════════════════════════════════════════════════
            // CaixaMovimento herda BaseEntity
            // TenantId já foi adicionado pelo FixConsolidado (IF NOT EXISTS)
            // Adiciona: CreatedAt, UpdatedAt, IsDeleted
            // ══════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='CaixaMovimentos' AND COLUMN_NAME='TenantId')
                    ALTER TABLE CaixaMovimentos ADD TenantId UNIQUEIDENTIFIER NOT NULL 
                        DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='CaixaMovimentos' AND COLUMN_NAME='CreatedAt')
                    ALTER TABLE CaixaMovimentos ADD CreatedAt DATETIME2 NOT NULL 
                        DEFAULT GETUTCDATE();");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='CaixaMovimentos' AND COLUMN_NAME='UpdatedAt')
                    ALTER TABLE CaixaMovimentos ADD UpdatedAt DATETIME2 NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='CaixaMovimentos' AND COLUMN_NAME='IsDeleted')
                    ALTER TABLE CaixaMovimentos ADD IsDeleted BIT NOT NULL DEFAULT 0;");

            // ══════════════════════════════════════════════════════════════
            // MovimentoHaver herda BaseEntity
            // A tabela foi criada no FixConsolidado JÁ COM TenantId/CreatedAt/UpdatedAt/IsDeleted
            // Apenas garante que existem caso o banco seja mais antigo
            // ══════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='MovimentosHaver' AND COLUMN_NAME='TenantId')
                    ALTER TABLE MovimentosHaver ADD TenantId UNIQUEIDENTIFIER NOT NULL 
                        DEFAULT '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='MovimentosHaver' AND COLUMN_NAME='CreatedAt')
                    ALTER TABLE MovimentosHaver ADD CreatedAt DATETIME2 NOT NULL 
                        DEFAULT GETUTCDATE();");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='MovimentosHaver' AND COLUMN_NAME='UpdatedAt')
                    ALTER TABLE MovimentosHaver ADD UpdatedAt DATETIME2 NULL;");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME='MovimentosHaver' AND COLUMN_NAME='IsDeleted')
                    ALTER TABLE MovimentosHaver ADD IsDeleted BIT NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down intencional vazio — colunas adicionadas com IF NOT EXISTS
            // não precisam ser revertidas em rollback para evitar perda de dados
        }
    }
}
