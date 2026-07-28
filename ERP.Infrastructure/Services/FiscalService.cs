// ── ERP.Application/Services/FiscalService.cs ───────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System.Globalization;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Emissão fiscal extraída do FinalizarVendaViewModel (WPF) — a mesma lógica,
/// os mesmos payloads, os mesmos fallbacks, só que reconstruindo tudo a
/// partir da Venda já persistida em vez do carrinho em memória. Isso é o
/// que permite chamar isso tanto do PDV quanto do processamento de pedido
/// de marketplace, com resultado idêntico.
///
/// ETAPA 1 da refatoração fiscal: só estrutura, nenhuma regra de negócio
/// mudou (mesmo payload, mesma validação, mesmo tratamento de contingência).
/// </summary>
public class FiscalService : IFiscalService
{
    private readonly Persistence.Context.AppDbContext _ctx;
    private readonly IFiscalConfigurationProvider _configProvider;
    private readonly INfceEmissionService _nfceService;
    private readonly INfeEmissionService _nfeService;
    private readonly INfeContingencyService _contingencyService;
    private readonly ISaleService _saleService;

    public FiscalService(
        Persistence.Context.AppDbContext ctx,
        IFiscalConfigurationProvider configProvider,
        INfceEmissionService nfceService,
        INfeEmissionService nfeService,
        INfeContingencyService contingencyService,
        ISaleService saleService)
    {
        _ctx                 = ctx;
        _configProvider      = configProvider;
        _nfceService         = nfceService;
        _nfeService          = nfeService;
        _contingencyService  = contingencyService;
        _saleService         = saleService;
    }

    public async Task<FiscalEmissionResult> EmitirNotaAsync(Guid vendaId, string tipoDocumento)
    {
        var sale = await _ctx.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Payments)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == vendaId)
            ?? throw new KeyNotFoundException($"Venda {vendaId} não encontrada.");

        var config = await _configProvider.ObterConfiguracaoAsync();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        var request = tipoDocumento == "NFE"
            ? MontarRequestNfeA4(sale)
            : MontarRequestNfce(sale);

        var (sucesso, mensagem, urlDanfe) = tipoDocumento == "NFE"
            ? await _nfeService.EmitirNfeA4Async(vendaId.ToString(), request, config.TokenFocusNfe, config.UsarAmbienteProducao)
            : await _nfceService.EmitirNfceAsync(vendaId.ToString(), request, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            try { await _saleService.AtualizarDadosNfceAsync(vendaId, urlDanfe, "Autorizada", ambienteSefaz, vendaId.ToString()); }
            catch (Exception exAtualizar)
            {
                Log.Warning(exAtualizar, "Falha ao salvar dados locais da nota autorizada para a venda {VendaId} (nota em si já foi autorizada na SEFAZ)", vendaId);
            }

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = mensagem, Status = "Autorizada",
                UrlDanfe = urlDanfe, Ambiente = ambienteSefaz
            };
        }

        // Falha de comunicação (não erro de validação/schema) → modo contingência,
        // mesmo critério exato usado hoje no WPF.
        bool ehFalhaComunicacao = mensagem.Contains("Erro de Comunicação")
            && !mensagem.Contains("UnprocessableEntity")
            && !mensagem.Contains("erro_validacao_schema");

        if (ehFalhaComunicacao)
        {
            try
            {
                string jsonPayload = JsonConvert.SerializeObject(request);
                await _contingencyService.RegistrarNotaPendenteAsync(vendaId, tipoDocumento, jsonPayload);
                await _saleService.AtualizarDadosNfceAsync(vendaId, "", "Contingência", ambienteSefaz, vendaId.ToString());

                return new FiscalEmissionResult
                {
                    Sucesso = true, Mensagem = "Venda salva em modo contingência — a nota será transmitida automaticamente quando a conexão voltar.",
                    Status = "Contingência", Ambiente = ambienteSefaz, EmContingencia = true
                };
            }
            catch (Exception exContingencia)
            {
                Log.Error(exContingencia, "Falha ao registrar nota em contingência para a venda {VendaId} — venda SEM documento fiscal nenhum.", vendaId);
                return new FiscalEmissionResult
                {
                    Sucesso = false,
                    Mensagem = $"Falha ao salvar a nota em contingência: {exContingencia.Message}. A venda foi concluída, mas SEM nota fiscal registrada.",
                    Status = "Falha", Ambiente = ambienteSefaz
                };
            }
        }

        return new FiscalEmissionResult { Sucesso = false, Mensagem = mensagem, Status = "Falha", Ambiente = ambienteSefaz };
    }

    private static string TraduzirFormaPagamentoParaSefaz(PaymentMethod metodo) => metodo switch
    {
        PaymentMethod.Dinheiro      => "01",
        PaymentMethod.CartaoCredito => "03",
        PaymentMethod.CartaoDebito  => "04",
        PaymentMethod.APrazo        => "05",
        PaymentMethod.Pix           => "17",
        _                           => "99"
    };

    private static List<FocusItemRequest> MontarItens(Domain.Entities.Sale sale) =>
        sale.Items.Select((item, index) =>
        {
            var ncm    = item.Product?.NCM;
            var csosn  = item.Product?.CSOSN;
            var cfop   = item.Product?.CFOPPadrao;
            return new FocusItemRequest
            {
                NumeroItem             = (index + 1).ToString(),
                CodigoProduto          = item.ProductId.ToString().Substring(0, 6),
                Descricao              = item.ProductName,
                QuantidadeComercial    = item.Quantity.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto             = item.TotalPrice.ToString("F2", CultureInfo.InvariantCulture),
                CodigoNcm              = string.IsNullOrWhiteSpace(ncm) ? "00000000" : ncm!.Replace(".", "").Replace("-", "").Trim(),
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(csosn) ? "102" : csosn!.Split('-')[0].Trim(),
                IcmsOrigem             = "0",
                Cfop                   = string.IsNullOrWhiteSpace(cfop) ? "5102" : cfop!.Replace(".", "").Trim(),
                PisSituacaoTributaria     = "99",
                CofinsSituacaoTributaria  = "99"
            };
        }).ToList();

    private static List<FocusPagamentoRequest> MontarPagamentos(Domain.Entities.Sale sale) =>
        sale.Payments.Select(p => new FocusPagamentoRequest
        {
            FormaPagamento = TraduzirFormaPagamentoParaSefaz(p.PaymentMethod),
            // SalePayment.Amount já é o valor efetivamente aplicado à venda
            // (pós-troco) — diferente do carrinho em memória do WPF, que
            // precisava subtrair o Troco na hora, aqui o dado já vem líquido.
            ValorPagamento = p.Amount.ToString("F2", CultureInfo.InvariantCulture)
        }).ToList();

    private static FocusNfceRequest MontarRequestNfce(Domain.Entities.Sale sale)
    {
        string? cpfCnpjLimpo = null;
        if (!string.IsNullOrWhiteSpace(sale.Customer?.Document))
            cpfCnpjLimpo = new string(sale.Customer.Document.Where(char.IsDigit).ToArray());

        return new FocusNfceRequest
        {
            DataEmissao = sale.SaleDate.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            CpfCnpj     = cpfCnpjLimpo,
            Nome        = sale.Customer?.Name,
            Itens       = MontarItens(sale),
            Pagamentos  = MontarPagamentos(sale)
        };
    }

    private static FocusNfceRequest MontarRequestNfeA4(Domain.Entities.Sale sale)
    {
        var customer = sale.Customer;
        string? cpfCnpjLimpo = null;
        if (!string.IsNullOrWhiteSpace(customer?.Document))
            cpfCnpjLimpo = new string(customer.Document.Where(char.IsDigit).ToArray());
        string? cepLimpo = null;
        if (!string.IsNullOrWhiteSpace(customer?.ZipCode))
            cepLimpo = new string(customer.ZipCode.Where(char.IsDigit).ToArray());
        string? ieLimpa = null;
        if (!string.IsNullOrWhiteSpace(customer?.StateRegistration))
            ieLimpa = new string(customer.StateRegistration.Where(char.IsDigit).ToArray());
        else if (cpfCnpjLimpo?.Length > 11) ieLimpa = "ISENTO";

        return new FocusNfceRequest
        {
            DataEmissao            = sale.SaleDate.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            TipoDocumento          = "1",
            CpfCnpj                = cpfCnpjLimpo,
            Nome                   = customer?.Name,
            LogradouroDestinatario = string.IsNullOrWhiteSpace(customer?.Street) ? "Nao Informado" : customer.Street,
            NumeroDestinatario     = string.IsNullOrWhiteSpace(customer?.Number) ? "S/N" : customer.Number,
            BairroDestinatario     = string.IsNullOrWhiteSpace(customer?.Neighborhood) ? "Centro" : customer.Neighborhood,
            MunicipioDestinatario  = string.IsNullOrWhiteSpace(customer?.City) ? "Curitiba" : customer.City,
            UfDestinatario         = string.IsNullOrWhiteSpace(customer?.State) ? "PR" : customer.State,
            CepDestinatario        = string.IsNullOrWhiteSpace(cepLimpo) ? "00000000" : cepLimpo,
            IeDestinatario         = ieLimpa,
            Itens                  = MontarItens(sale),
            Pagamentos             = MontarPagamentos(sale)
        };
    }
}
