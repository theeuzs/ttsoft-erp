// ── ERP.Infrastructure/Services/FiscalService.cs ───────────────────────────
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

        var (sucesso, mensagem, urlDanfe, urlXml) = tipoDocumento == "NFE"
            ? await _nfeService.EmitirNfeA4Async(vendaId.ToString(), request, config.TokenFocusNfe, config.UsarAmbienteProducao)
            : await _nfceService.EmitirNfceAsync(vendaId.ToString(), request, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            try { await _saleService.AtualizarDadosNfceAsync(vendaId, urlDanfe, "Autorizada", ambienteSefaz, vendaId.ToString()); }
            catch (Exception exAtualizar)
            {
                Log.Warning(exAtualizar, "Falha ao salvar dados locais da nota autorizada para a venda {VendaId} (nota em si já foi autorizada na SEFAZ)", vendaId);
            }

            await RegistrarNotaFiscalAsync(vendaId, sale, tipoDocumento, "Autorizada", urlDanfe, ambienteSefaz, urlXml);

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = mensagem, Status = "Autorizada",
                UrlDanfe = urlDanfe, Ambiente = ambienteSefaz
            };
        }

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
                await RegistrarNotaFiscalAsync(vendaId, sale, tipoDocumento, "Contingência", null, ambienteSefaz, null);

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

    public async Task<FiscalEmissionResult> EmitirNotaDevolucaoAsync(
        Guid vendaId, List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)> itensDevolvidos, string motivo)
    {
        var sale = await _ctx.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == vendaId)
            ?? throw new KeyNotFoundException($"Venda {vendaId} não encontrada.");

        if (string.IsNullOrWhiteSpace(sale.NfceChave))
        {
            // Best-effort de propósito: sem a chave da nota original não dá
            // pra montar "notas_referenciadas" corretamente, e a SEFAZ exige
            // isso pra devolução. A devolução operacional (estoque + Haver)
            // já aconteceu antes de chegar aqui — isso só avisa, não desfaz nada.
            return new FiscalEmissionResult
            {
                Sucesso = false,
                Mensagem = "Essa venda não tem nota fiscal original (NF-e) registrada — não é possível emitir NF-e de devolução sem a chave da nota original.",
                Status = "Não Aplicável"
            };
        }

        var config = await _configProvider.ObterConfiguracaoAsync();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        var request = MontarRequestNfeDevolucao(sale, itensDevolvidos, motivo);
        var referenciaDevolucao = $"devolucao-{vendaId}-{DateTime.Now:yyyyMMddHHmmss}";

        var (sucesso, mensagem, urlDanfe, urlXml) = await _nfeService.EmitirNfeA4Async(
            referenciaDevolucao, request, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            _ctx.NotasFiscais.Add(new Domain.Entities.NotaFiscal
            {
                Tipo                  = "NFE",
                VendaId               = vendaId,
                Status                = "Autorizada",
                Finalidade            = "4",
                RefNFe                = sale.NfceChave,
                UrlDanfe              = urlDanfe,
                XmlUrl                = string.IsNullOrWhiteSpace(urlXml) ? null : urlXml,
                Ambiente              = ambienteSefaz,
                DataEmissao           = DateTime.Now,
                DestinatarioNome      = sale.Customer?.Name,
                DestinatarioDocumento = sale.Customer?.Document,
                MotivoCancelamento    = null,
            });
            await _ctx.SaveChangesAsync();

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = "NF-e de devolução emitida com sucesso!",
                Status = "Autorizada", UrlDanfe = urlDanfe, Ambiente = ambienteSefaz
            };
        }

        Log.Warning("Falha ao emitir NF-e de devolução pra venda {VendaId}: {Mensagem}", vendaId, mensagem);
        return new FiscalEmissionResult { Sucesso = false, Mensagem = mensagem, Status = "Falha", Ambiente = ambienteSefaz };
    }

    private static FocusNfceRequest MontarRequestNfeDevolucao(
        Domain.Entities.Sale sale, List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)> itens, string motivo)
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

        var itensRequest = itens.Select((item, index) => new FocusItemRequest
        {
            NumeroItem             = (index + 1).ToString(),
            CodigoProduto          = item.ProductId.ToString().Substring(0, 6),
            Descricao              = item.ProductName,
            QuantidadeComercial    = item.Quantidade.ToString("F2", CultureInfo.InvariantCulture),
            ValorUnitarioComercial = item.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
            ValorBruto             = (item.Quantidade * item.ValorUnitario).ToString("F2", CultureInfo.InvariantCulture),
            // Devolução de venda dentro do estado — CFOP de entrada (1xxx),
            // não o de saída (5xxx) usado na venda original.
            Cfop                   = "1202",
            CodigoNcm              = "00000000",
            IcmsSituacaoTributaria = "102",
            IcmsOrigem             = "0",
            PisSituacaoTributaria     = "99",
            CofinsSituacaoTributaria  = "99",
        }).ToList();

        return new FocusNfceRequest
        {
            DataEmissao            = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            TipoDocumento          = "1",
            NaturezaOperacao       = "DEVOLUCAO DE VENDA",
            FinalidadeEmissao      = "4",
            CpfCnpj                = cpfCnpjLimpo,
            Nome                   = customer?.Name,
            LogradouroDestinatario = string.IsNullOrWhiteSpace(customer?.Street) ? "Nao Informado" : customer.Street,
            NumeroDestinatario     = string.IsNullOrWhiteSpace(customer?.Number) ? "S/N" : customer.Number,
            BairroDestinatario     = string.IsNullOrWhiteSpace(customer?.Neighborhood) ? "Centro" : customer.Neighborhood,
            MunicipioDestinatario  = string.IsNullOrWhiteSpace(customer?.City) ? "Curitiba" : customer.City,
            UfDestinatario         = string.IsNullOrWhiteSpace(customer?.State) ? "PR" : customer.State,
            CepDestinatario        = string.IsNullOrWhiteSpace(cepLimpo) ? "00000000" : cepLimpo,
            IeDestinatario         = ieLimpa,
            Itens                  = itensRequest,
            Pagamentos             = new List<FocusPagamentoRequest>(),
            NotasReferenciadas     = new List<NotaReferenciadaRequest> { new() { ChaveNfe = sale.NfceChave! } },
        };
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

    /// <summary>Fundação do módulo fiscal (entidade NotaFiscal própria) — grava
    /// em paralelo às colunas da Sale, que continuam existindo por
    /// compatibilidade com o resto do sistema (F10, cancelamento, status).
    /// Upsert por (VendaId, Tipo): reemissão atualiza o mesmo registro, não
    /// acumula duplicata.</summary>
    private async Task RegistrarNotaFiscalAsync(
        Guid vendaId, Domain.Entities.Sale sale, string tipoDocumento, string status, string? urlDanfe, string ambiente, string? urlXml)
    {
        var existente = await _ctx.NotasFiscais
            .FirstOrDefaultAsync(n => n.VendaId == vendaId && n.Tipo == tipoDocumento);

        if (existente is null)
        {
            _ctx.NotasFiscais.Add(new Domain.Entities.NotaFiscal
            {
                Tipo                  = tipoDocumento,
                VendaId               = vendaId,
                Status                = status,
                Finalidade            = "1",
                UrlDanfe              = urlDanfe,
                XmlUrl                = string.IsNullOrWhiteSpace(urlXml) ? null : urlXml,
                Ambiente              = ambiente,
                DataEmissao           = DateTime.Now,
                DestinatarioNome      = sale.Customer?.Name,
                DestinatarioDocumento = sale.Customer?.Document,
            });
        }
        else
        {
            existente.Status      = status;
            existente.UrlDanfe    = urlDanfe ?? existente.UrlDanfe;
            existente.XmlUrl      = string.IsNullOrWhiteSpace(urlXml) ? existente.XmlUrl : urlXml;
            existente.Ambiente    = ambiente;
        }

        await _ctx.SaveChangesAsync();
    }

    // Item de auditoria (06/08/2026): o ICMSSTCalculator (fórmula correta,
    // Convênio 142/2018) existia mas não alimentava a emissão de verdade —
    // ficava só em FiscalController/testes. UF de origem hardcoded em "PR"
    // (a mesma simplificação já usada nos fallbacks de endereço do
    // destinatário em toda essa classe) — vira TODO configurável quando
    // houver tenant fora do Paraná de verdade.
    private const string UF_ORIGEM_LOJA = "PR";

    private static List<FocusItemRequest> MontarItens(Domain.Entities.Sale sale)
    {
        string ufDestino = string.IsNullOrWhiteSpace(sale.Customer?.State) ? UF_ORIGEM_LOJA : sale.Customer!.State!;
        var stCalculator = new Domain.Services.Fiscal.ICMSSTCalculator();

        return sale.Items.Select((item, index) =>
        {
            var produto = item.Product;
            var ncm    = produto?.NCM;
            var csosn  = produto?.CSOSN;
            var cfop   = produto?.CFOPPadrao;

            var request = new FocusItemRequest
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

            if (produto != null && produto.TemSubstituicaoTrib)
            {
                // A Focus rejeita se mandar campo de ST com um CSOSN que não
                // espera ST (ex: "102") — força um CSOSN compatível quando o
                // catálogo não tiver um já certo, em vez de deixar a
                // inconsistência ir pra SEFAZ.
                var csosnsComSt = new[] { "201", "202", "203" };
                if (!csosnsComSt.Contains(request.IcmsSituacaoTributaria))
                    request.IcmsSituacaoTributaria = "202"; // sem permissão de crédito — mesma convenção do "102" default

                decimal aliqInterestadual = ufDestino == UF_ORIGEM_LOJA ? 0m
                    : MotorFiscalBrasileiro.ObterAliquotaInterestadual(UF_ORIGEM_LOJA, ufDestino);
                var st = stCalculator.CalcularDoProduto(produto, item.TotalPrice, aliqInterestadual);

                if (st != null)
                {
                    request.IcmsModalidadeBaseCalculoSt = "4"; // MVA
                    request.IcmsMargemValorAdicionadoSt = st.MVAUtilizado.ToString("F2", CultureInfo.InvariantCulture);
                    request.IcmsBaseCalculoSt            = st.BaseCalculoST.ToString("F2", CultureInfo.InvariantCulture);
                    request.IcmsAliquotaSt               = (produto.AliquotaInternaUFDest ?? 0m).ToString("F2", CultureInfo.InvariantCulture);
                    request.IcmsValorSt                  = st.ValorICMSST.ToString("F2", CultureInfo.InvariantCulture);
                }
            }

            return request;
        }).ToList();
    }

    private static List<FocusPagamentoRequest> MontarPagamentos(Domain.Entities.Sale sale) =>
        sale.Payments.Select(p => new FocusPagamentoRequest
        {
            FormaPagamento = TraduzirFormaPagamentoParaSefaz(p.PaymentMethod),
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