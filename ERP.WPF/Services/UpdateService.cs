using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Services;

public class VersaoInfo
{
    public string VersaoAtual    { get; set; } = "1.0.0";
    public bool   Obrigatoria    { get; set; } = false;
    public string UrlDownload    { get; set; } = string.Empty;
    public string Notas          { get; set; } = string.Empty;
    public string DataLancamento { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) do .exe publicado em UrlDownload. Ausente/vazio
    /// bloqueia a atualização — nunca é tratado como "sem verificação, segue
    /// mesmo assim". Ver S17 FIX.</summary>
    public string? Sha256        { get; set; }
}

public static class UpdateService
{
    private const string UrlVersao =
        "https://ttsoftupdates.blob.core.windows.net/releases/versao.json";

    /// <summary>
    /// Verifica se há versão mais nova no Blob.
    /// Retorna null se estiver atualizado ou sem internet — nunca lança exceção.
    /// </summary>
    public static async Task<VersaoInfo?> VerificarAtualizacaoAsync()
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{UrlVersao}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            { NoCache = true, NoStore = true };

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        string json = await response.Content.ReadAsStringAsync();
        var info = JsonSerializer.Deserialize<VersaoInfo>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (info == null) return null;

        if (!Version.TryParse(info.VersaoAtual, out var remota)) return null;

        var localVersion = Assembly.GetExecutingAssembly().GetName().Version
                           ?? new Version(1, 0, 0, 0);

        var remotaTrim = new Version(remota.Major, remota.Minor, Math.Max(remota.Build, 0));
        var localTrim  = new Version(localVersion.Major, localVersion.Minor, localVersion.Build);

        return remotaTrim > localTrim ? info : null;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Erro no update check: {ex.Message}", "Debug");
        return null;
    }
}

    /// <summary>
    /// Baixa o novo .exe e dispara o Updater.exe para substituir e reiniciar.
    /// Retorna true se o processo foi iniciado com sucesso (app vai fechar).
    /// </summary>
    public static async Task<bool> BaixarEAplicarAsync(
        VersaoInfo info,
        Action<int>? onProgress = null)
    {
        try
        {
            // S17 FIX: manifesto sem SHA-256 bloqueia a atualização. Nunca
            // "se tiver hash, valido" — é "só existe atualização com hash
            // válido", pra ninguém reativar o comportamento inseguro sem
            // perceber publicando um versao.json incompleto.
            if (string.IsNullOrWhiteSpace(info.Sha256))
            {
                MessageBox.Show(
                    "Manifesto de atualização sem SHA-256. Atualização bloqueada por segurança.",
                    "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // S17 FIX: nada impede hoje que UrlDownload aponte pra http://
            // — exigir https explicitamente antes de baixar qualquer coisa.
            if (!UrlEhHttps(info.UrlDownload))
            {
                MessageBox.Show(
                    "URL de download da atualização não é HTTPS. Atualização bloqueada por segurança.",
                    "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string tempExe = Path.Combine(Path.GetTempPath(), "ERP.WPF.new.exe");

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                info.UrlDownload,
                HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file   = File.Create(tempExe);

            byte[] buffer = new byte[81920];
            long   baixado = 0;
            int    lido;

            while ((lido = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, lido));
                baixado += lido;
                if (total > 0)
                    onProgress?.Invoke((int)(baixado * 100 / total));
            }

            file.Close();

            // S17 FIX: valida integridade do que foi baixado antes de fechar
            // o ERP e disparar o Updater — falha aqui não derruba a sessão
            // do usuário. Essa é a camada de UX; a validação que de fato
            // decide se o executável é substituído é a segunda, dentro do
            // próprio ERP.Updater (não confia que quem o chamou já validou).
            string hashCalculado = CalcularSha256Arquivo(tempExe);
            if (!HashConfere(info.Sha256, hashCalculado))
            {
                try { File.Delete(tempExe); } catch { /* limpeza best-effort */ }

                MessageBox.Show(
                    "O arquivo baixado não confere com o SHA-256 esperado. Atualização bloqueada por segurança.",
                    "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Dispara o Updater passando: [novo_exe] [destino_exe] [pid_do_erp] [sha256_esperado]
            string exeAtual = Process.GetCurrentProcess().MainModule!.FileName;
            string updater  = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

            if (!File.Exists(updater))
                throw new FileNotFoundException(
                    "Updater.exe não encontrado na pasta do sistema.", updater);

            Process.Start(new ProcessStartInfo
            {
                FileName        = updater,
                Arguments       = $"\"{tempExe}\" \"{exeAtual}\" {Process.GetCurrentProcess().Id} {info.Sha256!.Trim()}",
                UseShellExecute = true
            });

            // Fecha o ERP para liberar o .exe para substituição
            System.Windows.Application.Current.Shutdown();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erro ao baixar atualização:\n{ex.Message}",
                "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>S17 FIX: true só se a URL for absoluta e o esquema for
    /// exatamente https — nada aqui aceita http:// nem URL relativa.</summary>
    public static bool UrlEhHttps(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    /// <summary>S17 FIX: SHA-256 do arquivo em hex, sem separadores.
    /// Assume que ninguém mais tem o arquivo aberto para escrita.</summary>
    public static string CalcularSha256Arquivo(string caminhoArquivo)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(caminhoArquivo);
        return Convert.ToHexString(sha256.ComputeHash(stream));
    }

    /// <summary>S17 FIX: comparação fail-closed — esperado ausente/vazio
    /// NUNCA confere, mesmo que o calculado também esteja vazio. Ignora
    /// maiúsculas/minúsculas e espaços nas pontas (hex pode vir dos dois
    /// jeitos dependendo de quem gerou o manifesto).</summary>
    public static bool HashConfere(string? esperado, string? calculado)
        => !string.IsNullOrWhiteSpace(esperado)
           && !string.IsNullOrWhiteSpace(calculado)
           && string.Equals(esperado.Trim(), calculado.Trim(), StringComparison.OrdinalIgnoreCase);
}