using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ERP.WPF.Helpers;

public static class BackupService
{
    private const string PastaBackupLocal = @"C:\TTSoft_Backups";
    private const int    MaxBackupsLocais = 30;
    private const int    MaxBackupsNuvem  = 30;

    // ── Timeout estendido para BACKUP DATABASE ────────────────────────────────
    // O comando BACKUP DATABASE pode demorar vários minutos conforme o volume de dados
    // cresce. O timeout padrão do SqlClient é 30s — suficiente no início, mas começa
    // a falhar conforme o banco cresce (erro: "Tempo Limite de Execução Expirado").
    // 300s (5 min) é seguro para bancos de até ~50GB em rede local; ajuste se necessário.
    private const int BackupCommandTimeoutSeconds = 300;

    // ── Detecta se está conectado no Azure SQL ────────────────────────
    private static bool IsAzureSql()
    {
        try
        {
            var dbContext = ERP.WPF.App.Services
                .GetRequiredService<ERP.Persistence.Context.AppDbContext>();
            string dataSource = dbContext.Database.GetDbConnection().DataSource ?? "";
            return dataSource.Contains("database.windows.net",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ── Sanitiza string para uso seguro em comandos SQL de backup ─────
    // BACKUP DATABASE não suporta parâmetros (@p0) no SQL Server — sanitização
    // por allow-list é a proteção correta e usual para esse comando específico.
    // Nenhum dos dois valores chamadores (nome do banco: vem da connection string,
    // não de input do usuário; caminho: gerado por código a partir de constante +
    // timestamp) é controlável por usuário final — mas a validação fica aqui como
    // defesa em profundidade, não como a única barreira.
    private static string SanitizarParaBackup(string valor, string descricao)
    {
        // Permite: letras, números, espaços, hífen simples, underscores, pontos,
        // barras e dois-pontos (para path). Bloqueia explicitamente aspas, ponto e
        // vírgula, colchetes — e "--" (comentário SQL), que hífen simples sozinho
        // não cobria.
        if (!Regex.IsMatch(valor, @"^[\w\s\-\.:\\\/]+$") || valor.Contains("--"))
            throw new InvalidOperationException(
                $"Valor inválido para {descricao}: caracteres não permitidos detectados.");
        return valor;
    }

    // ── Backup automático (local + OneDrive) ──────────────────────────
    public static async Task RealizarBackupAutomaticoAsync(bool silencioso = true)
    {
        if (IsAzureSql())
        {
            if (!silencioso)
                MessageBox.Show(
                    "ℹ️ Banco de dados na nuvem (Azure SQL).\n\n" +
                    "O backup é gerenciado automaticamente pela Microsoft Azure " +
                    "com retenção de 7 dias. Nenhuma ação necessária.",
                    "Backup Azure", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string? caminhoArquivo = null;
        try
        {
            if (!Directory.Exists(PastaBackupLocal))
                Directory.CreateDirectory(PastaBackupLocal);

            string nomeArquivo = $"TTSoft_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            caminhoArquivo     = Path.Combine(PastaBackupLocal, nomeArquivo);

            // Valida que o caminho não saiu da pasta esperada (path traversal)
            string caminhoAbsoluto  = Path.GetFullPath(caminhoArquivo);
            string pastaAbsoluta    = Path.GetFullPath(PastaBackupLocal);
            if (!caminhoAbsoluto.StartsWith(pastaAbsoluta, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Caminho de backup inválido.");

            var dbContext = ERP.WPF.App.Services
                .GetRequiredService<ERP.Persistence.Context.AppDbContext>();
            string dbName = dbContext.Database.GetDbConnection().Database;

            // Sanitiza dbName e caminho antes de usar na string SQL
            // BACKUP DATABASE não aceita parâmetros @p0 — sanitização é a proteção correta aqui
            string dbNameSeguro  = SanitizarParaBackup(dbName, "nome do banco");
            string caminhoSeguro = SanitizarParaBackup(caminhoAbsoluto, "caminho do backup");

            // Estende o timeout SOMENTE para o BACKUP DATABASE, sem afetar o resto do sistema.
            // O timeout padrão (30s) é insuficiente conforme o banco cresce — causa o erro
            // "Tempo Limite de Execução Expirado" nas madrugadas. Restauramos após o backup.
            var timeoutOriginal = dbContext.Database.GetCommandTimeout();
            dbContext.Database.SetCommandTimeout(BackupCommandTimeoutSeconds);
            try
            {
                // EF1002 suprimido intencionalmente: BACKUP DATABASE não aceita parâmetros
                // no SQL Server. dbNameSeguro e caminhoSeguro já passaram por
                // SanitizarParaBackup() (allow-list + bloqueio de "--") logo acima, e
                // nenhum dos dois vem de input do usuário final (ver comentário do método).
#pragma warning disable EF1002
                await dbContext.Database.ExecuteSqlRawAsync(
                    $"BACKUP DATABASE [{dbNameSeguro}] TO DISK = N'{caminhoSeguro}' WITH INIT");
#pragma warning restore EF1002
            }
            finally
            {
                dbContext.Database.SetCommandTimeout(timeoutOriginal);
            }

            LimparBackupsAntigos(PastaBackupLocal, MaxBackupsLocais);

            bool nuvemOk = await CopiarParaOneDriveAsync(caminhoArquivo, nomeArquivo);

            if (!silencioso)
            {
                string msg = nuvemOk
                    ? $"✅ Backup concluído!\n\nLocal: {caminhoArquivo}\nNuvem: OneDrive ✓"
                    : $"⚠️ Backup local OK, mas falhou no OneDrive.\n\nLocal: {caminhoArquivo}";

                MessageBox.Show(msg, "Backup TTSoft", MessageBoxButton.OK,
                    nuvemOk ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ Falha ao realizar backup!\n\nErro: {ex.Message}",
                "ALERTA DE SEGURANÇA", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static async Task RealizarBackupManualAsync()
        => await RealizarBackupAutomaticoAsync(silencioso: false);

    private static async Task<bool> CopiarParaOneDriveAsync(
        string caminhoArquivo, string nomeArquivo)
    {
        try
        {
            string oneDrive =
                Environment.GetEnvironmentVariable("OneDrive") ??
                Environment.GetEnvironmentVariable("OneDriveConsumer") ??
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile), "OneDrive");

            if (!Directory.Exists(oneDrive))
                throw new Exception("Pasta do OneDrive não encontrada.");

            string pastaDestino = Path.Combine(oneDrive, "ERP_Backups_VilaVerde");
            if (!Directory.Exists(pastaDestino))
                Directory.CreateDirectory(pastaDestino);

            // Valida path traversal no destino também
            string destinoAbsoluto = Path.GetFullPath(Path.Combine(pastaDestino, nomeArquivo));
            if (!destinoAbsoluto.StartsWith(Path.GetFullPath(pastaDestino), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Caminho de destino inválido.");

            await Task.Run(() => File.Copy(caminhoArquivo, destinoAbsoluto, overwrite: true));

            LimparBackupsAntigos(pastaDestino, MaxBackupsNuvem);
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Logs", "backup_nuvem.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                await File.AppendAllTextAsync(logPath,
                    $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] ERRO OneDrive: {ex.Message}\n");
            }
            catch { }
            return false;
        }
    }

    private static void LimparBackupsAntigos(string pasta, int manter)
    {
        try
        {
            var arquivos = Directory.GetFiles(pasta, "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(manter);

            foreach (var arquivo in arquivos)
                arquivo.Delete();
        }
        catch { }
    }

    public static string ObterStatusUltimoBackup()
    {
        try
        {
            if (IsAzureSql())
                return "Azure SQL — backup automático gerenciado pela Microsoft";

            if (!Directory.Exists(PastaBackupLocal)) return "Nenhum backup realizado";

            var ultimo = Directory.GetFiles(PastaBackupLocal, "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .FirstOrDefault();

            if (ultimo == null) return "Nenhum backup realizado";

            var tamanho = ultimo.Length / (1024.0 * 1024.0);
            return $"Último: {ultimo.CreationTime:dd/MM/yyyy HH:mm} ({tamanho:N1} MB)";
        }
        catch { return "Erro ao verificar backup"; }
    }
}