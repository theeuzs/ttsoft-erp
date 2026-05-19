using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Gerencia o cache local SQLite para operação offline do PDV.
/// Sincroniza produtos e clientes do Azure → SQLite quando online.
/// Armazena vendas offline para sincronizar quando reconectar.
/// </summary>
public class OfflineSyncService
{
    private readonly string _dbPath;

    public OfflineSyncService()
    {
        var pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TTSoft.ERP");
        Directory.CreateDirectory(pasta);
        _dbPath = Path.Combine(pasta, "offline.db");
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

            CREATE TABLE IF NOT EXISTS VendasOffline (
                Id          TEXT PRIMARY KEY,
                DadosJson   TEXT NOT NULL,
                CriadoEm    TEXT NOT NULL,
                Sincronizado INTEGER NOT NULL DEFAULT 0
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

    // ── Sincronização de Produtos ─────────────────────────────────────────────

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
            cmd.Parameters.AddWithValue("@estoque", root.GetProperty("stock").GetDecimal());
            cmd.Parameters.AddWithValue("@unidade", root.TryGetProperty("unit", out var u) ? u.GetString() ?? "UN" : "UN");
            cmd.Parameters.AddWithValue("@json",    json);
            cmd.Parameters.AddWithValue("@dt",      DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
        await RegistrarLogAsync("SincProdutos", $"Sincronizados em {DateTime.Now:dd/MM/yyyy HH:mm}");
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

    // ── Vendas offline ────────────────────────────────────────────────────────

    public async Task SalvarVendaOfflineAsync(object venda)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO VendasOffline (Id, DadosJson, CriadoEm, Sincronizado)
            VALUES (@id, @json, @dt, 0)";
        cmd.Parameters.AddWithValue("@id",   Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(venda));
        cmd.Parameters.AddWithValue("@dt",   DateTime.Now.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Id, string Json)>> GetVendasPendentesAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, DadosJson FROM VendasOffline WHERE Sincronizado = 0";

        var lista = new List<(string, string)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add((reader.GetString(0), reader.GetString(1)));
        return lista;
    }

    public async Task MarcarVendaSincronizadaAsync(string id)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE VendasOffline SET Sincronizado = 1 WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Status ────────────────────────────────────────────────────────────────

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
            VendasPendentes     = await CountWhere("VendasOffline", "Sincronizado = 0"),
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
    public long   TamanhoBanco    { get; set; }
    public string CaminhoBanco    { get; set; } = string.Empty;
    public string TamanhoBancoFormatado
        => TamanhoBanco < 1024 * 1024
            ? $"{TamanhoBanco / 1024.0:F1} KB"
            : $"{TamanhoBanco / (1024.0 * 1024):F1} MB";
}
