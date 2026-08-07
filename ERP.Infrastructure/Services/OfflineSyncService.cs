using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Fase 1 do Offline-First (ver docs/OFFLINE_FIRST_ARCHITECTURE.md) — banco
/// local SQLite do terminal. Reaproveita as tabelas de cache que já
/// existiam (ProdutosCache/ClientesCache); refatora VendasOffline pra ter
/// status/tentativas/ultimo_erro em vez de um flag binário; adiciona a
/// SyncOutbox genérica (§4/§5 do documento). O Sync Engine (classe
/// separada, SyncEngineService) é quem chama isso e fala com o
/// ISaleService — esta classe só mexe no SQLite, nunca na rede.
/// </summary>
public class OfflineSyncService
{
    private readonly string _dbPath;

    /// <param name="dbPath">Testabilidade — igual ao padrão já usado em
    /// PixPollingService (S15 FIX): default preserva o comportamento de
    /// produção exatamente como era (caminho fixo em LocalApplicationData);
    /// os testes passam um caminho temporário isolado.</param>
    public OfflineSyncService(string? dbPath = null)
    {
        if (dbPath != null)
        {
            _dbPath = dbPath;
        }
        else
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TTSoft.ERP");
            Directory.CreateDirectory(pasta);
            _dbPath = Path.Combine(pasta, "offline.db");
        }
        InicializarBanco();
    }

    // ── Inicialização ─────────────────────────────────────────────────────────

    private void InicializarBanco()
    {
        using var conn = Abrir();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ProdutosCache (
                Id          TEXT PRIMARY KEY,
                Nome        TEXT NOT NULL,
                Barcode     TEXT,
                PrecoVenda  REAL NOT NULL,
                Estoque     REAL NOT NULL,
                Unidade     TEXT,
                DadosJson   TEXT,
                AtualizadoEm TEXT
            );

            CREATE TABLE IF NOT EXISTS ClientesCache (
                Id       TEXT PRIMARY KEY,
                Nome     TEXT NOT NULL,
                Cpf      TEXT,
                DadosJson TEXT
            );

            -- Achado de auditoria (06/08) + Fase 1 Offline-First (08/2026):
            -- Id É o mesmo GUID que vira Sale.Id no Azure (gerado aqui, no
            -- cliente, no momento da venda) — é essa igualdade que faz a
            -- idempotência do §7 funcionar. Status/Tentativas/UltimoErro
            -- substituem o antigo flag binário Sincronizado (0/1).
            CREATE TABLE IF NOT EXISTS VendasOffline (
                Id           TEXT PRIMARY KEY,
                DadosJson    TEXT NOT NULL,
                CriadoEm     TEXT NOT NULL,
                Status       TEXT NOT NULL DEFAULT 'Pendente',
                Tentativas   INTEGER NOT NULL DEFAULT 0,
                UltimoErro   TEXT,
                SincronizadoEm TEXT
            );

            -- Outbox genérica (§4/§5 do documento) — hoje só existe o tipo
            -- de evento SALE_CREATED (§6), mas a tabela já nasce genérica
            -- pra não precisar de migração de esquema quando crescer.
            CREATE TABLE IF NOT EXISTS SyncOutbox (
                Id          TEXT PRIMARY KEY,
                TipoEvento  TEXT NOT NULL,
                EntidadeId  TEXT NOT NULL,
                PayloadJson TEXT NOT NULL,
                CriadoEm    TEXT NOT NULL,
                Tentativas  INTEGER NOT NULL DEFAULT 0,
                UltimoErro  TEXT,
                Status      TEXT NOT NULL DEFAULT 'Pendente',
                SincronizadoEm TEXT
            );

            CREATE TABLE IF NOT EXISTS SyncLog (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Tipo        TEXT,
                Detalhes    TEXT,
                CriadoEm   TEXT
            );";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Abrir()
        => new($"Data Source={_dbPath};Cache=Shared");

    // ── Sincronização de Catálogo (§8 — snapshot, sem risco) ────────────────────

    public async Task SincronizarProdutosAsync(IEnumerable<object> produtos)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx  = conn.BeginTransaction();

        foreach (var produto in produtos)
        {
            var json = JsonSerializer.Serialize(produto);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ProdutosCache (Id, Nome, Barcode, PrecoVenda, Estoque, Unidade, DadosJson, AtualizadoEm)
                VALUES (@id, @nome, @barcode, @preco, @estoque, @unidade, @json, @dt)
                ON CONFLICT(Id) DO UPDATE SET
                    Nome = excluded.Nome, Barcode = excluded.Barcode,
                    PrecoVenda = excluded.PrecoVenda, Estoque = excluded.Estoque,
                    Unidade = excluded.Unidade, DadosJson = excluded.DadosJson,
                    AtualizadoEm = excluded.AtualizadoEm";

            cmd.Parameters.AddWithValue("@id",      root.GetProperty("id").GetString());
            cmd.Parameters.AddWithValue("@nome",    root.GetProperty("name").GetString());
            cmd.Parameters.AddWithValue("@barcode", root.TryGetProperty("barcode", out var b) ? b.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("@preco",   root.GetProperty("salePrice").GetDecimal());
            // Estoque aqui é só uma fotografia pra o operador ter noção do
            // saldo — NUNCA é usado como fonte pra decrementar (§8 do doc).
            cmd.Parameters.AddWithValue("@estoque", root.GetProperty("stock").GetDecimal());
            cmd.Parameters.AddWithValue("@unidade", root.TryGetProperty("unit", out var u) ? u.GetString() ?? "UN" : "UN");
            cmd.Parameters.AddWithValue("@json",    json);
            cmd.Parameters.AddWithValue("@dt",      DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
        await RegistrarLogAsync("SincProdutos", $"{produtos.Count()} produtos sincronizados em {DateTime.Now:dd/MM/yyyy HH:mm}");
    }

    public async Task SincronizarClientesAsync(IEnumerable<object> clientes)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        foreach (var cliente in clientes)
        {
            var json = JsonSerializer.Serialize(cliente);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ClientesCache (Id, Nome, Cpf, DadosJson)
                VALUES (@id, @nome, @cpf, @json)
                ON CONFLICT(Id) DO UPDATE SET
                    Nome = excluded.Nome, Cpf = excluded.Cpf, DadosJson = excluded.DadosJson";

            cmd.Parameters.AddWithValue("@id",   root.GetProperty("id").GetString());
            cmd.Parameters.AddWithValue("@nome", root.GetProperty("name").GetString());
            cmd.Parameters.AddWithValue("@cpf",  root.TryGetProperty("document", out var c) ? c.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("@json", json);
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
        await RegistrarLogAsync("SincClientes", $"{clientes.Count()} clientes sincronizados em {DateTime.Now:dd/MM/yyyy HH:mm}");
    }

    // ── Consulta offline ──────────────────────────────────────────────────────

    public async Task<string?> BuscarProdutoPorBarcodeAsync(string barcode)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DadosJson FROM ProdutosCache WHERE Barcode = @b LIMIT 1";
        cmd.Parameters.AddWithValue("@b", barcode);
        return (await cmd.ExecuteScalarAsync()) as string;
    }

    public async Task<List<string>> BuscarProdutosPorNomeAsync(string termo)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DadosJson FROM ProdutosCache WHERE Nome LIKE @t LIMIT 50";
        cmd.Parameters.AddWithValue("@t", $"%{termo}%");

        var lista = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add(reader.GetString(0));
        return lista;
    }

    // ── Vendas offline (§7 idempotência, §16.5 atomicidade) ─────────────────────

    /// <summary>Grava a venda offline E o evento correspondente na Outbox
    /// numa transação SQLite só (§16.5 do documento — achado do ChatGPT).
    /// Se o computador desligar no meio, nenhuma das duas tabelas fica com
    /// dado parcial: ou as duas existem juntas, ou nenhuma existe.</summary>
    /// <param name="vendaId">Precisa ser o MESMO Guid que vai virar Sale.Id
    /// no Azure quando sincronizar — é essa igualdade que garante
    /// idempotência (§7). Gerado no cliente, ANTES de qualquer tentativa de
    /// rede, com Guid.NewGuid() no momento da venda.</param>
    public async Task SalvarVendaOfflineComOutboxAsync(Guid vendaId, object vendaDto)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var json = JsonSerializer.Serialize(vendaDto);
            var agora = DateTime.Now.ToString("O");

            using (var cmdVenda = conn.CreateCommand())
            {
                cmdVenda.Transaction = tx;
                cmdVenda.CommandText = @"
                    INSERT INTO VendasOffline (Id, DadosJson, CriadoEm, Status, Tentativas)
                    VALUES (@id, @json, @dt, 'Pendente', 0)";
                cmdVenda.Parameters.AddWithValue("@id",   vendaId.ToString());
                cmdVenda.Parameters.AddWithValue("@json", json);
                cmdVenda.Parameters.AddWithValue("@dt",   agora);
                await cmdVenda.ExecuteNonQueryAsync();
            }

            using (var cmdOutbox = conn.CreateCommand())
            {
                cmdOutbox.Transaction = tx;
                cmdOutbox.CommandText = @"
                    INSERT INTO SyncOutbox (Id, TipoEvento, EntidadeId, PayloadJson, CriadoEm, Tentativas, Status)
                    VALUES (@id, 'SALE_CREATED', @entidadeId, @json, @dt, 0, 'Pendente')";
                cmdOutbox.Parameters.AddWithValue("@id",         Guid.NewGuid().ToString());
                cmdOutbox.Parameters.AddWithValue("@entidadeId", vendaId.ToString());
                cmdOutbox.Parameters.AddWithValue("@json",       json);
                cmdOutbox.Parameters.AddWithValue("@dt",         agora);
                await cmdOutbox.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<(string Id, string Json, int Tentativas)>> GetEventosPendentesAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, PayloadJson, Tentativas FROM SyncOutbox WHERE Status = 'Pendente' ORDER BY CriadoEm";

        var lista = new List<(string, string, int)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return lista;
    }

    public async Task MarcarEventoSincronizadoAsync(string outboxId, Guid entidadeId)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        var agora = DateTime.Now.ToString("O");

        using (var cmd1 = conn.CreateCommand())
        {
            cmd1.Transaction = tx;
            cmd1.CommandText = "UPDATE SyncOutbox SET Status = 'Sincronizado', SincronizadoEm = @dt WHERE Id = @id";
            cmd1.Parameters.AddWithValue("@id", outboxId);
            cmd1.Parameters.AddWithValue("@dt", agora);
            await cmd1.ExecuteNonQueryAsync();
        }

        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.Transaction = tx;
            cmd2.CommandText = "UPDATE VendasOffline SET Status = 'Sincronizado', SincronizadoEm = @dt WHERE Id = @id";
            cmd2.Parameters.AddWithValue("@id", entidadeId.ToString());
            cmd2.Parameters.AddWithValue("@dt", agora);
            await cmd2.ExecuteNonQueryAsync();
        }

        tx.Commit();
        await RegistrarLogAsync("SyncSucesso", $"Venda {entidadeId} sincronizada às {DateTime.Now:HH:mm:ss}");
    }

    public async Task RegistrarFalhaEventoAsync(string outboxId, Guid entidadeId, string erro)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        using (var cmd1 = conn.CreateCommand())
        {
            cmd1.Transaction = tx;
            cmd1.CommandText = "UPDATE SyncOutbox SET Tentativas = Tentativas + 1, UltimoErro = @erro WHERE Id = @id";
            cmd1.Parameters.AddWithValue("@id",   outboxId);
            cmd1.Parameters.AddWithValue("@erro", erro);
            await cmd1.ExecuteNonQueryAsync();
        }

        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.Transaction = tx;
            cmd2.CommandText = "UPDATE VendasOffline SET Tentativas = Tentativas + 1, UltimoErro = @erro WHERE Id = @id";
            cmd2.Parameters.AddWithValue("@id",   entidadeId.ToString());
            cmd2.Parameters.AddWithValue("@erro", erro);
            await cmd2.ExecuteNonQueryAsync();
        }

        tx.Commit();
        await RegistrarLogAsync("SyncFalha", $"Venda {entidadeId}: {erro}");
    }

    // ── Status / diagnóstico (§13 — dados; a tela em si é Fase 3) ───────────────

    public async Task<OfflineStatus> GetStatusAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();

        async Task<int> Count(string tabela)
        {
            using var c = conn.CreateCommand();
            c.CommandText = $"SELECT COUNT(*) FROM {tabela}";
            return Convert.ToInt32(await c.ExecuteScalarAsync());
        }

        async Task<int> CountWhere(string tabela, string where)
        {
            using var c = conn.CreateCommand();
            c.CommandText = $"SELECT COUNT(*) FROM {tabela} WHERE {where}";
            return Convert.ToInt32(await c.ExecuteScalarAsync());
        }

        return new OfflineStatus
        {
            TotalProdutos       = await Count("ProdutosCache"),
            TotalClientes       = await Count("ClientesCache"),
            VendasPendentes     = await CountWhere("VendasOffline", "Status = 'Pendente'"),
            VendasComErro       = await CountWhere("VendasOffline", "Tentativas > 0 AND Status = 'Pendente'"),
            TamanhoBanco        = new FileInfo(_dbPath).Length,
            CaminhoBanco        = _dbPath
        };
    }

    private async Task RegistrarLogAsync(string tipo, string detalhes)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO SyncLog (Tipo, Detalhes, CriadoEm) VALUES (@t, @d, @dt)";
        cmd.Parameters.AddWithValue("@t",  tipo);
        cmd.Parameters.AddWithValue("@d",  detalhes);
        cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
}

public class OfflineStatus
{
    public int    TotalProdutos   { get; set; }
    public int    TotalClientes   { get; set; }
    public int    VendasPendentes { get; set; }
    public int    VendasComErro   { get; set; }
    public long   TamanhoBanco    { get; set; }
    public string CaminhoBanco    { get; set; } = string.Empty;
    public string TamanhoBancoFormatado
        => TamanhoBanco < 1024 * 1024
            ? $"{TamanhoBanco / 1024.0:F1} KB"
            : $"{TamanhoBanco / (1024.0 * 1024):F1} MB";
}