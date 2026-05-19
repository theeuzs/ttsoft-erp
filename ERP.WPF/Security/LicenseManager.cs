using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace ERP.WPF.Security;

public static class LicenseManager
{
    private static readonly string ApiUrl = "https://ttsoft-api-brasil-f8c2d0f5g0d9hvd8.brazilsouth-01.azurewebsites.net";
    private static readonly string CacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenca.cache");
    private const int GracePeriodDias = 3;

    public static DateTime DataVencimento { get; private set; }
    public static string   StatusAtual    { get; private set; } = "Desconhecido";
    public static int      DiasRestantes  { get; private set; } = 0;

    public static async Task<(bool IsValid, DateTime DataVencimento)> VerificarLicencaAsync(string cnpj)
    {
        string machineId = MachineFingerprint.GetMachineId();

        try
        {
            // ← Timeout aumentado de 10s para 30s (Azure App Service cold start pode demorar)
            using var client   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string    url      = $"{ApiUrl}/api/licenca/verificar?cnpj={cnpj}&machineId={machineId}";
            var       response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                StatusAtual    = root.GetProperty("status").GetString() ?? "Ativo";
                DataVencimento = root.GetProperty("vencimento").GetDateTime();
                DiasRestantes  = root.TryGetProperty("diasRestantes", out var dr) ? dr.GetInt32() : 999;
                bool alerta    = root.TryGetProperty("alertaVencimento", out var av) && av.GetBoolean();

                SalvarCache(cnpj, machineId, DataVencimento);

                if (alerta)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"⚠️ Sua licença vence em {DiasRestantes} dia(s)!\n\nRenove para evitar interrupção do sistema.\nContato: TTSoft (41) 99627-2846",
                            "Licença Próxima do Vencimento",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }

                return (true, DataVencimento);
            }
            else
            {
                string motivo = "Licença inválida.";
                try
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("motivo", out var m))
                        motivo = m.GetString() ?? motivo;
                }
                catch { }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"🚫 SISTEMA NÃO AUTORIZADO!\n\n{motivo}\n\nContato: TTSoft (41) 99627-2846",
                        "Licença Inválida",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });

                return (false, DateTime.MinValue);
            }
        }
        catch (Exception ex)
        {
            // ← Loga o motivo real em vez de engolir silenciosamente
            Log.Warning(ex, "Falha ao verificar licença online. CNPJ={Cnpj} MachineId={MachineId} Erro={Erro}",
                cnpj, machineId, ex.Message);

            return VerificarCacheOffline(cnpj, machineId);
        }
    }

    // ── Cache offline ────────────────────────────────────────────────
    private static void SalvarCache(string cnpj, string machineId, DateTime vencimento)
    {
        try
        {
            var dados = new
            {
                cnpj,
                machineId,
                vencimento = vencimento.ToString("O"),
                salvoEm    = DateTime.Now.ToString("O")
            };
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(dados));
        }
        catch { }
    }

    private static (bool IsValid, DateTime DataVencimento) VerificarCacheOffline(string cnpj, string machineId)
    {
        try
        {
            if (!File.Exists(CacheFile))
            {
                MostrarErraSemInternet(semCache: true);
                return (false, DateTime.MinValue);
            }

            string json = File.ReadAllText(CacheFile);
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            string cnpjCache    = root.GetProperty("cnpj").GetString() ?? "";
            string machineCache = root.GetProperty("machineId").GetString() ?? "";
            DateTime vencimento = DateTime.Parse(root.GetProperty("vencimento").GetString() ?? "");
            DateTime salvoEm    = DateTime.Parse(root.GetProperty("salvoEm").GetString() ?? "");

            if (cnpjCache != cnpj || machineCache != machineId)
            {
                MostrarErraSemInternet(semCache: true);
                return (false, DateTime.MinValue);
            }

            int diasSemInternet = (DateTime.Now - salvoEm).Days;
            if (diasSemInternet > GracePeriodDias)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"🚫 Sem conexão com o servidor de licenças há {diasSemInternet} dias.\n\nO sistema só permite {GracePeriodDias} dias offline.\n\nVerifique sua internet e tente novamente.\nContato: TTSoft (41) 99627-2846",
                        "Limite Offline Excedido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
                return (false, DateTime.MinValue);
            }

            if (vencimento.Date < DateTime.Now.Date)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"🚫 Sua licença venceu em {vencimento:dd/MM/yyyy}.\n\nRenove para continuar usando o sistema.\nContato: TTSoft (41) 99627-2846",
                        "Licença Vencida",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
                return (false, DateTime.MinValue);
            }

            DataVencimento = vencimento;
            StatusAtual    = "Ativo (Offline)";
            DiasRestantes  = (vencimento.Date - DateTime.Now.Date).Days;

            // ← Sem MessageBox no modo offline — só registra no log
            Log.Warning("Licença verificada em modo OFFLINE. Dias sem internet: {Dias}/{Max}. Vencimento: {Vencimento}",
                diasSemInternet, GracePeriodDias, vencimento.ToString("dd/MM/yyyy"));

            return (true, vencimento);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao ler cache de licença offline");
            MostrarErraSemInternet(semCache: true);
            return (false, DateTime.MinValue);
        }
    }

    private static void MostrarErraSemInternet(bool semCache)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                "🚫 Não foi possível verificar a licença.\n\nSem conexão com a internet e sem cache local válido.\n\nContato: TTSoft (41) 99627-2846",
                "Erro de Licença",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }
}