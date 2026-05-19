using Serilog;
using System.IO.Ports;
using System.Text;

namespace ERP.Infrastructure.Services;

public record LeituraBalanca(bool Sucesso, decimal Peso, string Unidade, string? Erro = null);

/// <summary>
/// Leitura de balança via porta serial RS-232.
/// Suporta protocolo Toledo (padrão de materiais de construção) e Filizola.
/// Configurar COM port e baudrate nas configurações do sistema.
/// </summary>
public class BalancaService : IDisposable
{
    private SerialPort? _porta;
    private bool        _disposed;

    public bool IsConectada => _porta?.IsOpen ?? false;

    public bool Conectar(string comPort = "COM1", int baudRate = 9600)
    {
        try
        {
            if (_porta?.IsOpen == true) _porta.Close();

            _porta = new SerialPort(comPort, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout  = 2000,
                WriteTimeout = 2000,
                Encoding     = Encoding.ASCII
            };

            _porta.Open();
            Log.Information("Balança conectada em {Port} @ {Baud}", comPort, baudRate);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao conectar balança em {Port}", comPort);
            return false;
        }
    }

    public void Desconectar()
    {
        _porta?.Close();
        Log.Information("Balança desconectada");
    }

    // ── Protocolo Toledo (mais comum em materiais de construção) ──────────────

    public LeituraBalanca LerPesoToledo()
    {
        if (_porta?.IsOpen != true)
            return new LeituraBalanca(false, 0, "", "Balança não conectada.");

        try
        {
            // Toledo: envia ENQ (0x05), recebe STX + dados + ETX
            _porta.Write(new byte[] { 0x05 }, 0, 1); // ENQ

            // Lê resposta até ETX (0x03) ou timeout
            var sb = new StringBuilder();
            int lido;
            while ((lido = _porta.ReadByte()) != 0x03 && lido != -1)
            {
                if (lido != 0x02) // Ignora STX
                    sb.Append((char)lido);
            }

            return ParsearRespostaToledo(sb.ToString().Trim());
        }
        catch (TimeoutException)
        {
            return new LeituraBalanca(false, 0, "", "Timeout — balança não respondeu.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao ler balança Toledo");
            return new LeituraBalanca(false, 0, "", ex.Message);
        }
    }

    private static LeituraBalanca ParsearRespostaToledo(string dados)
    {
        // Formato Toledo: "P  001.500 kg" ou "P  001500 g"
        // Ou formato simplificado: "001.500"
        if (string.IsNullOrWhiteSpace(dados))
            return new LeituraBalanca(false, 0, "", "Resposta vazia da balança.");

        var unidade = "kg";
        var valorStr = dados;

        if (dados.Contains("kg", StringComparison.OrdinalIgnoreCase))
        {
            unidade  = "kg";
            valorStr = dados.Replace("kg", "", StringComparison.OrdinalIgnoreCase);
        }
        else if (dados.Contains(" g", StringComparison.OrdinalIgnoreCase))
        {
            unidade  = "g";
            valorStr = dados.Replace(" g", "", StringComparison.OrdinalIgnoreCase);
        }

        // Remove indicador "P" do Toledo e espaços
        valorStr = valorStr.Replace("P", "").Replace("S", "").Trim();

        if (decimal.TryParse(valorStr,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var peso))
        {
            // Converte gramas para kg se necessário
            if (unidade == "g") { peso /= 1000m; unidade = "kg"; }
            return new LeituraBalanca(true, Math.Round(peso, 3), unidade);
        }

        return new LeituraBalanca(false, 0, "", $"Formato inválido: '{dados}'");
    }

    // ── Protocolo Filizola ────────────────────────────────────────────────────

    public LeituraBalanca LerPesoFilizola()
    {
        if (_porta?.IsOpen != true)
            return new LeituraBalanca(false, 0, "", "Balança não conectada.");

        try
        {
            // Filizola: envia 0x05 (ENQ), aguarda resposta de 6 bytes
            _porta.Write(new byte[] { 0x05 }, 0, 1);

            var buffer = new byte[16];
            int total  = 0;
            while (total < 6)
            {
                int lido = _porta.Read(buffer, total, buffer.Length - total);
                if (lido == 0) break;
                total += lido;
            }

            if (total < 6)
                return new LeituraBalanca(false, 0, "", "Resposta incompleta da balança.");

            // Formato Filizola: byte[0]=STX, bytes[1-5]=peso em gramas BCD, byte[5]=ETX
            var pesoBruto = (buffer[1] - '0') * 10000 +
                            (buffer[2] - '0') * 1000 +
                            (buffer[3] - '0') * 100 +
                            (buffer[4] - '0') * 10 +
                            (buffer[5] - '0');

            return new LeituraBalanca(true, Math.Round(pesoBruto / 1000m, 3), "kg");
        }
        catch (TimeoutException)
        {
            return new LeituraBalanca(false, 0, "", "Timeout — balança Filizola não respondeu.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao ler balança Filizola");
            return new LeituraBalanca(false, 0, "", ex.Message);
        }
    }

    // ── Lista portas disponíveis ──────────────────────────────────────────────

    public static string[] ListarPortas() => SerialPort.GetPortNames();

    public void Dispose()
    {
        if (!_disposed)
        {
            _porta?.Close();
            _porta?.Dispose();
            _disposed = true;
        }
    }
}
