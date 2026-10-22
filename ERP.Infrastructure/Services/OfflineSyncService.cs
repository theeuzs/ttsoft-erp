using Microsoft.Data.Sqlite;
using System.Text.Json;
using ERP.Application.DTOs;

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

    // Achado (15/09) — Serialize(produto)/Serialize(cliente) rodavam sem
    // opções, então saíam em PascalCase ("Id", "SalePrice" — o padrão do C#),
    // mas o código logo abaixo lia de volta em camelCase ("id", "salePrice").
    // GetProperty("id") contra uma chave "Id" no JSON gerado —
    // KeyNotFoundException toda vez, silenciosamente derrubando a
    // sincronização inteira daquele catálogo (produto sempre falhava;
    // cliente também falharia, só nunca chegava a rodar por causa do bug
    // separado do DbContext concorrente, corrigido junto).
    private static readonly JsonSerializerOptions _jsonCamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        var idsSnapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var produto in produtos)
        {
            var json = JsonSerializer.Serialize(produto, _jsonCamelCase);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            idsSnapshot.Add(root.GetProperty("id").GetString()!);

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

        var (removidos, motivoSemLimpeza) = await PurgarProdutosAusentesDoSnapshotAsync(conn, tx, idsSnapshot);

        tx.Commit();
        // Log só depois do commit: RegistrarLogAsync abre outra conexão e
        // disputaria o lock de escrita com a transação ainda aberta.
        if (motivoSemLimpeza != null)
            await RegistrarLogAsync("SincProdutosSemLimpeza", motivoSemLimpeza);
        await RegistrarLogAsync("SincProdutos", $"{idsSnapshot.Count} produtos sincronizados, {removidos} removido(s) do cache em {ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil():dd/MM/yyyy HH:mm}");
    }

    // Tamanho mínimo do cache para valer o teste de "snapshot parcial", e
    // fração mínima do cache anterior que o snapshot precisa cobrir.
    private const int CacheMinimoParaTesteDeCobertura = 20;
    private const double CoberturaMinimaDoSnapshot = 0.5;

    /// <summary>Remove do ProdutosCache o que o servidor não devolve mais
    /// (excluído) — senão o PDV offline continua vendendo produto inexistente.
    /// Conservador de propósito; NÃO purga se:
    ///  • o snapshot veio vazio (resposta vazia nunca prova catálogo vazio);
    ///  • o snapshot cobre menos da metade do cache atual (resposta parcial);
    /// e NUNCA remove produto usado por venda ainda pendente ou em quarentena.
    /// (Chegar aqui já implica HTTP 200 e JSON íntegro; sem total oficial no
    /// endpoint, esses são os sinais de completude disponíveis.)</summary>
    private async Task<(int Removidos, string? MotivoSemLimpeza)> PurgarProdutosAusentesDoSnapshotAsync(
        SqliteConnection conn, SqliteTransaction tx, HashSet<string> idsSnapshot)
    {
        if (idsSnapshot.Count == 0)
            return (0, "Snapshot vazio — cache preservado.");

        var idsCache = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Id FROM ProdutosCache";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) idsCache.Add(r.GetString(0));
        }

        if (idsCache.Count >= CacheMinimoParaTesteDeCobertura
            && idsSnapshot.Count < idsCache.Count * CoberturaMinimaDoSnapshot)
        {
            return (0, $"Snapshot parcial ({idsSnapshot.Count} de {idsCache.Count} no cache) — cache preservado.");
        }

        var obsoletos = idsCache.Where(id => !idsSnapshot.Contains(id)).ToList();
        if (obsoletos.Count == 0) return (0, null);

        var emUso = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT DadosJson FROM VendasOffline WHERE Status <> 'Sincronizado'";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                ColetarProductIds(r.GetString(0), emUso);
        }

        int removidos = 0;
        foreach (var id in obsoletos.Where(id => !emUso.Contains(id)))
        {
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM ProdutosCache WHERE Id = @id";
            del.Parameters.AddWithValue("@id", id);
            removidos += await del.ExecuteNonQueryAsync();
        }
        return (removidos, null);
    }

    private static void ColetarProductIds(string vendaJson, HashSet<string> destino)
    {
        try
        {
            using var doc = JsonDocument.Parse(vendaJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.NameEquals("Items") && !prop.Name.Equals("items", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in prop.Value.EnumerateArray())
                    foreach (var p in item.EnumerateObject())
                        if (p.Name.Equals("ProductId", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                            destino.Add(p.Value.GetString()!);
            }
        }
        catch (JsonException) { /* payload ilegível: não dá pra proteger nada dele */ }
    }

    // Achado (16/09) — a metade que faltava da arquitetura offline-first
    // (§8/§16.5 do documento): o catálogo local já é escrito sozinho a cada
    // minuto (SincronizarProdutosAsync acima), mas nunca existia um jeito de
    // LER esse cache de volta. Sem isso, quando a internet cai, a busca do
    // PDV simplesmente falha e fica vazia — apesar do catálogo estar salvo
    // localmente, atualizado, esperando.
    //
    // Reconstrói o ProductDto a partir do DadosJson (o produto inteiro, não
    // só as colunas resumidas da tabela) — assim o resultado do fallback
    // tem os mesmos campos que a busca online devolveria (preço de atacado,
    // categoria, etc.), não uma versão capada.

    /// <summary>Busca por nome (LIKE, mesmo comportamento de sempre) OU por
    /// código de barras/SKU (prefixo) no catálogo local — espelha
    /// IProductService.SearchAsync, só que lendo do SQLite em vez do Azure.</summary>
    public async Task<List<ProductDto>> BuscarProdutosCacheAsync(string termo, int limite = 30)
    {
        var resultado = new List<ProductDto>();
        using var conn = Abrir();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DadosJson FROM ProdutosCache
            WHERE Nome LIKE @termo OR Barcode LIKE @prefixo
            ORDER BY Nome
            LIMIT @limite";
        cmd.Parameters.AddWithValue("@termo",   $"%{termo}%");
        cmd.Parameters.AddWithValue("@prefixo", $"{termo}%");
        cmd.Parameters.AddWithValue("@limite",  limite);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var produto = DesserializarProdutoCache(reader.GetString(0));
            if (produto != null) resultado.Add(produto);
        }
        return resultado;
    }

    /// <summary>Espelha IProductService.GetByBarcodeAsync — busca EXATA por
    /// código de barras (bipagem), não prefixo.</summary>
    public async Task<ProductDto?> BuscarProdutoPorCodigoBarrasCacheAsync(string barcode)
    {
        using var conn = Abrir();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DadosJson FROM ProdutosCache WHERE Barcode = @barcode LIMIT 1";
        cmd.Parameters.AddWithValue("@barcode", barcode);

        var dadosJson = await cmd.ExecuteScalarAsync() as string;
        return dadosJson == null ? null : DesserializarProdutoCache(dadosJson);
    }

    private static ProductDto? DesserializarProdutoCache(string dadosJson)
    {
        try { return JsonSerializer.Deserialize<ProductDto>(dadosJson); }
        catch (Exception ex)
        {
            // Não devolve exceção pro chamador — um produto com cache
            // corrompido não pode derrubar a busca inteira dos outros.
            System.Diagnostics.Debug.WriteLine($"OfflineSyncService: falha ao ler produto do cache — {ex.Message}");
            return null;
        }
    }

    public async Task SincronizarClientesAsync(IEnumerable<object> clientes)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        foreach (var cliente in clientes)
        {
            var json = JsonSerializer.Serialize(cliente, _jsonCamelCase);
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
        await RegistrarLogAsync("SincClientes", $"{clientes.Count()} clientes sincronizados em {ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil():dd/MM/yyyy HH:mm}");
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
            var agora = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil().ToString("O");

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

    public async Task<List<(string Id, string Json, int Tentativas, string CriadoEm)>> GetEventosPendentesAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, PayloadJson, Tentativas, CriadoEm FROM SyncOutbox WHERE Status = 'Pendente' ORDER BY CriadoEm";

        var lista = new List<(string, string, int, string)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3)));
        return lista;
    }

    // ── Quarentena de falhas de NEGÓCIO ─────────────────────────────────────────
    // Erro de negócio (produto inválido, estoque insuficiente, caixa fechado...)
    // não melhora sozinho com retry. Em vez de tentar para sempre, o evento sai
    // do ciclo automático com Status 'FalhaNegocio' — payload e motivo
    // preservados, nada descartado. As DUAS tabelas mudam juntas, senão a
    // reconciliação de órfãs recriaria o evento na Outbox.
    public const string StatusFalhaNegocio = "FalhaNegocio";

    public async Task MarcarEventoFalhaNegocioAsync(string outboxId, Guid entidadeId, string erro)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        using (var cmd1 = conn.CreateCommand())
        {
            cmd1.Transaction = tx;
            cmd1.CommandText = "UPDATE SyncOutbox SET Status = @st, Tentativas = Tentativas + 1, UltimoErro = @erro WHERE Id = @id";
            cmd1.Parameters.AddWithValue("@st",   StatusFalhaNegocio);
            cmd1.Parameters.AddWithValue("@id",   outboxId);
            cmd1.Parameters.AddWithValue("@erro", erro);
            await cmd1.ExecuteNonQueryAsync();
        }
        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.Transaction = tx;
            cmd2.CommandText = "UPDATE VendasOffline SET Status = @st, Tentativas = Tentativas + 1, UltimoErro = @erro WHERE Id = @id";
            cmd2.Parameters.AddWithValue("@st",   StatusFalhaNegocio);
            cmd2.Parameters.AddWithValue("@id",   entidadeId.ToString());
            cmd2.Parameters.AddWithValue("@erro", erro);
            await cmd2.ExecuteNonQueryAsync();
        }
        tx.Commit();
        await RegistrarLogAsync("SyncFalhaNegocio", $"Venda {entidadeId} em quarentena: {erro}");
    }

    /// <summary>Vendas em quarentena (Id, motivo, quando, payload) — para
    /// diagnóstico e exportação. Somente leitura.</summary>
    public async Task<List<(string Id, string UltimoErro, string CriadoEm, string Json)>> GetVendasEmQuarentenaAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, COALESCE(UltimoErro, ''), CriadoEm, DadosJson FROM VendasOffline WHERE Status = @st ORDER BY CriadoEm";
        cmd.Parameters.AddWithValue("@st", StatusFalhaNegocio);

        var lista = new List<(string, string, string, string)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lista.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return lista;
    }

    /// <summary>Devolve TODAS as vendas em quarentena para a fila (Pendente),
    /// zerando tentativas. Ação manual do operador depois de resolver a causa
    /// (abrir caixa, corrigir estoque...). Idempotente no servidor.</summary>
    public async Task<int> ReenfileirarQuarentenaAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        int n;
        using (var cmd1 = conn.CreateCommand())
        {
            cmd1.Transaction = tx;
            cmd1.CommandText = "UPDATE SyncOutbox SET Status = 'Pendente', Tentativas = 0 WHERE Status = @st";
            cmd1.Parameters.AddWithValue("@st", StatusFalhaNegocio);
            n = await cmd1.ExecuteNonQueryAsync();
        }
        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.Transaction = tx;
            cmd2.CommandText = "UPDATE VendasOffline SET Status = 'Pendente', Tentativas = 0 WHERE Status = @st";
            cmd2.Parameters.AddWithValue("@st", StatusFalhaNegocio);
            await cmd2.ExecuteNonQueryAsync();
        }
        tx.Commit();
        if (n > 0) await RegistrarLogAsync("ReenfileirarQuarentena", $"{n} venda(s) devolvida(s) à fila.");
        return n;
    }

    // Achado (17/09) — venda pendente vista no diagnóstico ("Vendas
    // pendentes: 1"), mas "Processar Outbox Agora" sempre achava 0 eventos.
    // VendasOffline e SyncOutbox são gravadas juntas, na mesma transação,
    // desde que SalvarVendaOfflineComOutboxAsync existe — mas essa venda
    // específica é mais antiga que essa gravação dupla (de antes da
    // SyncOutbox genérica existir, ou de um caminho de código anterior que
    // só gravava em VendasOffline). Fica pendente pra sempre em VendasOffline,
    // órfã, sem chance de ser pega por ProcessarOutboxAsync, que só lê
    // SyncOutbox. Chamado no início de ProcessarOutboxAsync — encontra
    // qualquer venda pendente em VendasOffline sem par correspondente em
    // SyncOutbox e cria o par que falta, pra ela voltar a ser processável.
    // Autocura pra esse caso específico, não só um remendo manual único.
    public async Task<int> ReconciliarVendasOfflineOrfasAsync()
    {
        using var conn = Abrir();
        await conn.OpenAsync();

        var orfas = new List<(string Id, string Json)>();
        using (var cmdBusca = conn.CreateCommand())
        {
            cmdBusca.CommandText = @"
                SELECT vo.Id, vo.DadosJson FROM VendasOffline vo
                WHERE vo.Status = 'Pendente'
                AND NOT EXISTS (SELECT 1 FROM SyncOutbox so WHERE so.EntidadeId = vo.Id)";
            using var reader = await cmdBusca.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                orfas.Add((reader.GetString(0), reader.GetString(1)));
        }

        if (orfas.Count == 0) return 0;

        var agora = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil().ToString("O");
        using var tx = conn.BeginTransaction();
        foreach (var (id, json) in orfas)
        {
            using var cmdInsert = conn.CreateCommand();
            cmdInsert.Transaction = tx;
            cmdInsert.CommandText = @"
                INSERT INTO SyncOutbox (Id, TipoEvento, EntidadeId, PayloadJson, CriadoEm, Tentativas, Status)
                VALUES (@id, 'SALE_CREATED', @entidadeId, @json, @dt, 0, 'Pendente')";
            cmdInsert.Parameters.AddWithValue("@id",         Guid.NewGuid().ToString());
            cmdInsert.Parameters.AddWithValue("@entidadeId", id);
            cmdInsert.Parameters.AddWithValue("@json",       json);
            cmdInsert.Parameters.AddWithValue("@dt",         agora);
            await cmdInsert.ExecuteNonQueryAsync();
        }
        tx.Commit();

        await RegistrarLogAsync("ReconciliacaoOutbox", $"{orfas.Count} venda(s) órfã(s) reincluída(s) na fila de sincronização.");
        return orfas.Count;
    }

    public async Task MarcarEventoSincronizadoAsync(string outboxId, Guid entidadeId)
    {
        using var conn = Abrir();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        var agora = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil().ToString("O");

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
        await RegistrarLogAsync("SyncSucesso", $"Venda {entidadeId} sincronizada às {ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil():HH:mm:ss}");
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
            VendasEmQuarentena  = await CountWhere("VendasOffline", $"Status = '{StatusFalhaNegocio}'"),
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
        cmd.Parameters.AddWithValue("@dt", ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil().ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
}

public class OfflineStatus
{
    public int    TotalProdutos   { get; set; }
    public int    TotalClientes   { get; set; }
    public int    VendasPendentes { get; set; }
    /// <summary>Falha de negócio: fora do ciclo automático, payload preservado.</summary>
    public int    VendasEmQuarentena { get; set; }
    /// <summary>O que o indicador do PDV deve mostrar: quarentena NÃO pode
    /// sumir do aviso só porque saiu do ciclo automático.</summary>
    public int    VendasNaoSincronizadas => VendasPendentes + VendasEmQuarentena;
    public int    VendasComErro   { get; set; }
    public long   TamanhoBanco    { get; set; }
    public string CaminhoBanco    { get; set; } = string.Empty;
    public string TamanhoBancoFormatado
        => TamanhoBanco < 1024 * 1024
            ? $"{TamanhoBanco / 1024.0:F1} KB"
            : $"{TamanhoBanco / (1024.0 * 1024):F1} MB";
}