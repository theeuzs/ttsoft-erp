// ERP.WPF/Services/HttpCaixaService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C, módulo 3 (Caixa) — implementação HTTP de ICaixaService.
/// Troca só na linha de DI (App.xaml.cs); consumidores: AbrirCaixaViewModel,
/// PdvViewModel, ResumoCaixaViewModel, FinanceiroViewModel, ContaPagarViewModel,
/// SaleViewModel.
///
/// Detalhe importante que não existia nos módulos Cliente/Produto: a API
/// resolve o usuário SEMPRE pelo JWT (ClaimTypes.NameIdentifier), nunca pelo
/// parâmetro `usuarioId` que os métodos abaixo recebem — isso é proposital
/// (S8 FIX no CaixaController: usar o body pra isso permitia abrir/mexer no
/// caixa de outro usuário). Os parâmetros continuam na assinatura só pra
/// bater com a interface local; todo consumidor no WPF já passa sempre o
/// próprio AppSession.UserId, então não muda nada na prática.
///
/// ExisteMovimentoParaSalePaymentAsync NÃO tem endpoint — é usado só
/// internamente por SaleService/DevolucaoService, ambos server-side (Vendas
/// já migrado na Fase B). Nada no WPF chama isso via ICaixaService.
/// </summary>
public class HttpCaixaService : ICaixaService
{
    private const string Base = "/api/caixa";
    private readonly HttpMessageHandler? _handler;

    public HttpCaixaService(HttpMessageHandler? handler = null) => _handler = handler;

    public async Task<CaixaDto?> ObterCaixaAbertoAsync(Guid usuarioId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/aberto"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CaixaDto>(ApiHttp.JsonOpcoes);
    }

    public async Task AbrirCaixaAsync(AbrirCaixaDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/abrir"),
            new AbrirCaixaRequestDto { ValorAbertura = dto.ValorAbertura }, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        // Caixa já aberto etc. — o controller devolve 400 {erro:"..."} via
        // InvalidOperationException. Local (CaixaService direto) lançava a
        // MESMA exceção; preserva o tipo pros catches existentes
        // (AbrirCaixaViewModel já trata InvalidOperationException).
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task RegistrarMovimentoAsync(Guid usuarioId, decimal valor, string descricao,
        PaymentMethod formaPagamento, TipoMovimentoCaixa tipo,
        decimal maxSangriaValue = 0m, Guid? vendaId = null, Guid? salePaymentId = null)
    {
        // maxSangriaValue: ignorado aqui de propósito — o controller resolve
        // o limite pelo CARGO do usuário via JWT (_tenant.MaxSangriaValue),
        // nunca aceita isso do cliente (correto: cliente não é confiável pra
        // dizer qual é o próprio limite). vendaId/salePaymentId: nenhum dos
        // 3 endpoints (sangria/suprimento/movimento) aceita esses campos no
        // corpo — só fazem sentido nos lançamentos que o SaleService cria
        // internamente (server-side), nunca a partir do WPF.
        // Corpo montado como objeto anônimo, não como o record
        // MovimentoCaixaRequest — aquele tipo vive em ERP.Api.Controllers,
        // um projeto que o WPF não referencia (e não deveria). O shape
        // JSON (Valor/Descricao/Tipo/FormaPagamento) é o que importa pro
        // model binding da API, não o tipo C# de quem manda.
        var corpo = new
        {
            Valor          = valor,
            Descricao      = descricao,
            Tipo           = tipo.ToString(),
            FormaPagamento = formaPagamento.ToString()
        };

        using var http = ApiHttp.CriarHttpClient(_handler);
        var rota = tipo switch
        {
            TipoMovimentoCaixa.Sangria    => "sangria",
            TipoMovimentoCaixa.Suprimento => "suprimento",
            _                             => "movimento"
        };

        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/{rota}"), corpo, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "registrar esse tipo de movimento de caixa");

        // Caixa fechado, sangria maior que o saldo, etc. — mesma exceção que
        // o CaixaService local lançava (InvalidOperationException), agora
        // reconstituída a partir do 400 da API.
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task FecharCaixaAsync(Guid usuarioId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/fechar"), content: null);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public Task<bool> ExisteMovimentoParaSalePaymentAsync(Guid salePaymentId)
        => throw new NotSupportedException(
            "ExisteMovimentoParaSalePaymentAsync é uso interno do servidor " +
            "(SaleService/DevolucaoService) — não existe endpoint porque nada " +
            "no WPF chama isso via ICaixaService.");

    public async Task<ResumoCaixaDto?> ObterResumoAsync(Guid usuarioId, DateTime data)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/resumo?data={data:yyyy-MM-dd}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ResumoCaixaDto>(ApiHttp.JsonOpcoes);
    }
}
