-- ============================================================
--  TTSoft ERP — Script de Setup Multi-Tenancy no Azure SQL
--  Execute este script UMA VEZ no banco ERPMateriais do Azure.
--  É seguro rodar múltiplas vezes (verifica antes de alterar).
-- ============================================================

PRINT '=== TTSoft Multi-Tenancy Setup ===';
PRINT 'Iniciando em: ' + CONVERT(VARCHAR, GETDATE(), 120);

-- ─── 1. ADICIONA COLUNA TenantId EM TODAS AS TABELAS ──────────────────

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Products' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Products ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Products';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Customers' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Customers ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Customers';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Sales' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Sales ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Sales';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Users' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Users ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Users';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Roles' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Roles ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Roles';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Caixas' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Caixas ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Caixas';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='Orcamentos' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE Orcamentos ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em Orcamentos';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='ContasReceber' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE ContasReceber ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em ContasReceber';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='ContasPagar' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE ContasPagar ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em ContasPagar';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='PedidosCompra' AND COLUMN_NAME='TenantId')
BEGIN
    ALTER TABLE PedidosCompra ADD TenantId UNIQUEIDENTIFIER NOT NULL
        DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'TenantId adicionado em PedidosCompra';
END

-- ─── 2. ÍNDICES DE PERFORMANCE ─────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_TenantId')
    CREATE INDEX IX_Products_TenantId   ON Products(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Customers_TenantId')
    CREATE INDEX IX_Customers_TenantId  ON Customers(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sales_TenantId')
    CREATE INDEX IX_Sales_TenantId      ON Sales(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Users_TenantId')
    CREATE INDEX IX_Users_TenantId      ON Users(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Caixas_TenantId')
    CREATE INDEX IX_Caixas_TenantId     ON Caixas(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Orcamentos_TenantId')
    CREATE INDEX IX_Orcamentos_TenantId ON Orcamentos(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ContasReceber_TenantId')
    CREATE INDEX IX_ContasReceber_TenantId ON ContasReceber(TenantId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ContasPagar_TenantId')
    CREATE INDEX IX_ContasPagar_TenantId   ON ContasPagar(TenantId);

PRINT 'Índices criados.';

-- ─── 3. PREENCHER TenantId DA VILA VERDE ───────────────────────────────
--
-- INSTRUÇÃO:
-- Execute o console abaixo em C# (ex: LinqPad ou projeto Console) para
-- descobrir o TenantId da Vila Verde a partir do CNPJ 12.820.608/0001-41:
--
--   using System.Security.Cryptography;
--   using System.Text;
--   var cnpj = "12820608000141";
--   var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(cnpj));
--   var guidBytes = new byte[16];
--   Array.Copy(hash, guidBytes, 16);
--   Console.WriteLine(new Guid(guidBytes));
--
-- Cole o resultado no DECLARE abaixo e execute este bloco separadamente:
--
-- DECLARE @TenantVilaVerde UNIQUEIDENTIFIER = 'COLE-O-GUID-AQUI';
-- UPDATE Products      SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Customers     SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Sales         SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Users         SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Roles         SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Caixas        SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE Orcamentos    SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE ContasReceber SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE ContasPagar   SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- UPDATE PedidosCompra SET TenantId = @TenantVilaVerde WHERE TenantId = '00000000-0000-0000-0000-000000000000';
-- PRINT 'Dados da Vila Verde migrados!';

PRINT '=== Setup concluído com sucesso! ===';
