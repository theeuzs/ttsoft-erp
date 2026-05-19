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
    // BACKUP DATABASE não suporta parâmetros (@p0) no SQL Server.
    // Sanitizamos permitindo apenas caracteres seguros.
    private static string SanitizarParaBackup(string valor, string descricao)
    {
        // Permite: letras, números, espaços, hífens, underscores, pontos, barras e dois-pontos (para path)
        if (!Regex.IsMatch(valor, @"^[\w\s\-\.:\\\/]+$"))
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
            string dbNameSeguro    = SanitizarParaBackup(dbName, "nome do banco");
            string caminhoSeguro   = SanitizarParaBackup(caminhoAbsoluto, "caminho do backup");

            await dbContext.Database.ExecuteSqlRawAsync(
                $"BACKUP DATABASE [{dbNameSeguro}] TO DISK = N'{caminhoSeguro}' WITH INIT");

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
