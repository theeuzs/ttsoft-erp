using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
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

            // Dispara o Updater passando: [novo_exe] [destino_exe] [pid_do_erp]
            string exeAtual = Process.GetCurrentProcess().MainModule!.FileName;
            string updater  = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

            if (!File.Exists(updater))
                throw new FileNotFoundException(
                    "Updater.exe não encontrado na pasta do sistema.", updater);

            Process.Start(new ProcessStartInfo
            {
                FileName        = updater,
                Arguments       = $"\"{tempExe}\" \"{exeAtual}\" {Process.GetCurrentProcess().Id}",
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
}
