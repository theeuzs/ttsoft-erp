using Serilog;
using System.Net.Sockets;
using System.Text;

namespace ERP.Infrastructure.Services;

public enum TipoTransacaoTEF { Debito, Credito, CreditoParcelado, Pix, Cancelamento }
public enum StatusTEF { Pendente, Aprovado, Negado, Cancelado, Erro, TimeoutSemResposta }

public class TEFResultado
{
    public StatusTEF Status          { get; set; }
    public string    NSU             { get; set; } = string.Empty;
    public string    Autorizacao     { get; set; } = string.Empty;
    public string    Bandeira        { get; set; } = string.Empty;
    public string    MensagemDisplay { get; set; } = string.Empty;
    public string?   ViaCliente      { get; set; }
    public string?   ViaEstabelecimento { get; set; }
    public decimal   ValorAprovado   { get; set; }
}

/// <summary>
/// Integração TEF via protocolo PayGo Web (socket TCP local).
/// O PayGo Client deve estar instalado na máquina do caixa.
/// Porta padrão: 8085 (configurável).
/// </summary>
public class TEFService
{
    private readonly string _host;
    private readonly int    _porta;
    private readonly int    _timeoutMs;

    public TEFService(string host = "127.0.0.1", int porta = 8085, int timeoutMs = 90000)
    {
        _host      = host;
        _porta     = porta;
        _timeoutMs = timeoutMs;
    }

    public async Task<TEFResultado> IniciarTransacaoAsync(
        TipoTransacaoTEF tipo, decimal valor, int parcelas = 1, string? numeroCupom = null)
    {
        Log.Information("TEF: iniciando transação {Tipo} R$ {Valor}", tipo, valor);

        try
        {
            // Monta requisição no formato PayGo
            var req = MontarRequisicao(tipo, valor, parcelas, numeroCupom);
            var resposta = await EnviarComandoAsync(req);
            return ParsearResposta(resposta);
        }
        catch (SocketException ex)
        {
            Log.Warning(ex, "TEF: PayGo Client não encontrado na porta {Porta}", _porta);
            return new TEFResultado
            {
                Status          = StatusTEF.Erro,
                MensagemDisplay = "TEF não disponível. Verifique se o PayGo Client está aberto."
            };
        }
        catch (TimeoutException)
        {
            return new TEFResultado
            {
                Status          = StatusTEF.TimeoutSemResposta,
                MensagemDisplay = "Tempo limite de resposta do TEF esgotado."
            };
        }
    }

    public async Task<TEFResultado> CancelarTransacaoAsync(string nsu, decimal valor)
    {
        var req = $"CRT|{nsu}|{(int)(valor * 100):D12}|";
        var resposta = await EnviarComandoAsync(req);
        return ParsearResposta(resposta);
    }

    private string MontarRequisicao(TipoTransacaoTEF tipo, decimal valor, int parcelas, string? cupom)
    {
        var valorCentavos = (int)(valor * 100);
        var modalidade = tipo switch
        {
            TipoTransacaoTEF.Debito           => "2",  // Débito
            TipoTransacaoTEF.Credito          => "3",  // Crédito à vista
            TipoTransacaoTEF.CreditoParcelado => "4",  // Crédito parcelado loja
            TipoTransacaoTEF.Pix              => "9",  // PIX via TEF
            _                                 => "3"
        };

        // Formato PayGo: CMD|VALOR|MODALIDADE|PARCELAS|CUPOM|
        return $"CNF|{valorCentavos:D12}|{modalidade}|{parcelas:D2}|{cupom ?? ""}|";
    }

    private async Task<string> EnviarComandoAsync(string comando)
    {
        using var client = new TcpClient();
        var cts = new CancellationTokenSource(_timeoutMs);

        await client.ConnectAsync(_host, _porta, cts.Token);

        var stream = client.GetStream();
        var bytes  = Encoding.ASCII.GetBytes(comando + "\x1C"); // ETX terminator
        await stream.WriteAsync(bytes, cts.Token);

        // Lê resposta
        var buffer   = new byte[4096];
        var sb       = new StringBuilder();
        int read;
        while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
        {
            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (sb.ToString().Contains('\x1C')) break; // ETX = fim da resposta
        }

        return sb.ToString().TrimEnd('\x1C');
    }

    private TEFResultado ParsearResposta(string resposta)
    {
        // Formato de resposta PayGo: COD|NSU|AUTORIZACAO|BANDEIRA|MENSAGEM|VIA_CLI|VIA_EST|VALOR|
        var partes = resposta.Split('|');
        if (partes.Length < 2)
            return new TEFResultado { Status = StatusTEF.Erro, MensagemDisplay = "Resposta inválida do TEF" };

        var codigo = partes[0];
        var status = codigo == "000" ? StatusTEF.Aprovado
                   : codigo == "001" ? StatusTEF.Negado
                   : codigo == "003" ? StatusTEF.Cancelado
                   : StatusTEF.Erro;

        return new TEFResultado
        {
            Status             = status,
            NSU                = partes.ElementAtOrDefault(1) ?? "",
            Autorizacao        = partes.ElementAtOrDefault(2) ?? "",
            Bandeira           = partes.ElementAtOrDefault(3) ?? "",
            MensagemDisplay    = partes.ElementAtOrDefault(4) ?? "",
            ViaCliente         = partes.ElementAtOrDefault(5),
            ViaEstabelecimento = partes.ElementAtOrDefault(6),
            ValorAprovado      = decimal.TryParse(partes.ElementAtOrDefault(7), out var v) ? v / 100m : 0
        };
    }
}
