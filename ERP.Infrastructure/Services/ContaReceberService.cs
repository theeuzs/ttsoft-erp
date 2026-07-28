using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// 1.6.4: IServiceProvider removido — injeção direta de AppDbContext e IRequestTenant.
/// O padrão CreateScope criava um novo scope sem herdar o tenant do request corrente,
/// fazendo queries sem filtro de tenant ou com tenant errado.
/// </summary>
public class ContaReceberService : IContaReceberService
{
    private readonly IUnitOfWork    _uow;
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;
    private readonly AsaasService?  _asaas;

    public ContaReceberService(
        IUnitOfWork uow, AppDbContext ctx, IRequestTenant tenant, AsaasService? asaas = null)
    {
        _uow    = uow;
        _ctx    = ctx;
        _tenant = tenant;
        _asaas  = asaas;
    }

    /// <summary>
    /// Registra um evento na linha do tempo da conta — Tipo: "Criada",
    /// "Desconto", "Pagamento" ou "Cancelamento". Simples Add+SaveChanges (a
    /// entidade é nova, então não sofre do problema de ChangeTracker.Clear()
    /// detacher entidades já rastreadas — esse é o caso seguro).
    /// </summary>
    private async Task RegistrarEventoAsync(Guid contaId, string tipo, decimal? valor, string? observacao)
    {
        await _ctx.ContaReceberEventos.AddAsync(new ContaReceberEvento
        {
            ContaReceberId = contaId,
            Tipo           = tipo,
            UsuarioId      = _tenant.UserId == Guid.Empty ? null : _tenant.UserId,
            UsuarioNome    = string.IsNullOrEmpty(_tenant.UserName) ? null : _tenant.UserName,
            Valor          = valor,
            Observacao     = observacao,
            DataEvento     = DateTime.Now
        });
        await _ctx.SaveChangesAsync();
    }

    public async Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao)
    {
        var conta = new ContaReceber
        {
            CustomerId     = clienteId,
            SaleId         = vendaId,
            ValorTotal     = valor,
            ValorRecebido  = 0,
            DataEmissao    = DateTime.Now,
            DataVencimento = DateTime.Now.AddDays(30),
            Status         = "Pendente",
            Descricao      = descricao
        };
        await _uow.ContasReceber.AddAsync(conta);
        await _uow.CommitAsync();

        await RegistrarEventoAsync(conta.Id, "Criada", valor, descricao);
    }

    public async Task<IEnumerable<ContaReceber>> GetPendentesAsync()
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

    public async Task<IEnumerable<ContaReceber>> GetPorClienteAsync(Guid clienteId)
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.CustomerId == clienteId)
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync();

    public async Task<IEnumerable<ContaReceber>> GetInadimplentesAsync()
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente" && c.DataVencimento.Date < DateTime.Today)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

    public async Task DarBaixaParcialAsync(Guid contaId, decimal valorRecebido)
    {
        var tenantId = _tenant.TenantId;
        var agora    = DateTime.UtcNow;
        var dataPag  = DateTime.Now;

        // Atômico: soma relativa (ValorRecebido = ValorRecebido + X) direto no
        // SQL, não Math.Min(conta.ValorRecebido + X, ...) calculado em C# a
        // partir de uma leitura AsNoTracking — essa era a versão com
        // lost-update (duas baixas quase simultâneas perdiam uma). O teto
        // agora é ValorTotal-ValorDesconto (o antigo usava só ValorTotal,
        // o que superestimava quanto ainda cabia receber quando já havia
        // desconto aplicado).
        var linhas = await _ctx.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ContasReceber
            SET
                ValorRecebido = CASE
                    WHEN ValorRecebido + {valorRecebido} >= ValorTotal - ValorDesconto THEN ValorTotal - ValorDesconto
                    ELSE ValorRecebido + {valorRecebido}
                END,
                Status = CASE
                    WHEN ValorRecebido + {valorRecebido} + ValorDesconto >= ValorTotal THEN 'Pago'
                    ELSE 'Pendente'
                END,
                DataPagamento = CASE
                    WHEN ValorRecebido + {valorRecebido} + ValorDesconto >= ValorTotal THEN {dataPag}
                    ELSE NULL
                END,
                UpdatedAt = {agora}
            WHERE Id = {contaId} AND TenantId = {tenantId} AND Status <> 'Cancelado'");

        if (linhas == 0)
            throw new InvalidOperationException("Conta não encontrada ou está cancelada — não é possível dar baixa.");

        await RegistrarEventoAsync(contaId, "Pagamento", valorRecebido, null);
    }

    public async Task DarBaixaTotalAsync(Guid contaId)
    {
        var conta = await _ctx.ContasReceber.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ContasReceber SET ValorRecebido={conta.ValorTotal}, Status={"Pago"}, DataPagamento={DateTime.Now}, UpdatedAt={DateTime.UtcNow} WHERE Id={contaId} AND TenantId={_tenant.TenantId}");

        await RegistrarEventoAsync(contaId, "Pagamento", conta.ValorTotal - conta.ValorRecebido, "Baixa total");
    }

    public async Task CancelarAsync(Guid contaId, string motivo)
    {
        // Antes: buscava a conta só pra confirmar que existia e descartava
        // (`_ = await ...`), sem checar o status — dava pra cancelar uma
        // conta já paga. Agora a própria guarda do UPDATE impede isso.
        var linhas = await _ctx.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ContasReceber
            SET Status={"Cancelado"}, MotivoCancelamento={motivo}, UpdatedAt={DateTime.UtcNow}
            WHERE Id={contaId} AND TenantId={_tenant.TenantId} AND Status <> 'Pago'");

        if (linhas == 0)
        {
            var existe = await _ctx.ContasReceber.AsNoTracking().AnyAsync(c => c.Id == contaId);
            throw existe
                ? new InvalidOperationException("Não é possível cancelar uma conta já paga.")
                : new KeyNotFoundException("Conta não encontrada.");
        }

        await RegistrarEventoAsync(contaId, "Cancelamento", null, motivo);
    }

    public async Task DarDescontoAsync(Guid contaId, decimal valorDesconto, string motivo)
    {
        var conta = await _ctx.ContasReceber.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        // Essa leitura é só pra mensagem de erro amigável e pro texto da
        // Descricao — a validação de verdade é a guarda do WHERE no UPDATE
        // atômico abaixo, que reavalia o saldo no momento exato da escrita.
        var saldoAtual = conta.ValorTotal - conta.ValorRecebido - conta.ValorDesconto;
        if (valorDesconto > saldoAtual)
            throw new InvalidOperationException(
                $"Desconto de {valorDesconto:C} maior que o saldo devido ({saldoAtual:C}).");

        var descricaoComMotivo = $"{conta.Descricao} [Desconto de {valorDesconto:C}: {motivo}]";
        var dataPag = DateTime.Now;
        var agora   = DateTime.UtcNow;

        var linhas = await _ctx.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ContasReceber
            SET ValorDesconto = ValorDesconto + {valorDesconto},
                Status = CASE
                    WHEN ValorRecebido + ValorDesconto + {valorDesconto} >= ValorTotal THEN 'Pago'
                    ELSE Status
                END,
                DataPagamento = CASE
                    WHEN ValorRecebido + ValorDesconto + {valorDesconto} >= ValorTotal THEN {dataPag}
                    ELSE DataPagamento
                END,
                Descricao = {descricaoComMotivo}, UpdatedAt = {agora}
            WHERE Id = {contaId} AND TenantId = {_tenant.TenantId}
              AND Status <> 'Cancelado'
              AND ValorTotal - ValorRecebido - ValorDesconto >= {valorDesconto}");

        if (linhas == 0)
            throw new InvalidOperationException(
                "Não foi possível aplicar o desconto — a conta pode ter sido cancelada ou paga nesse meio tempo. Confira o saldo atual e tente de novo.");

        await RegistrarEventoAsync(contaId, "Desconto", valorDesconto, motivo);
    }

    public async Task DarBaixaEmLoteAsync(
        IEnumerable<Guid> contaIds, decimal valorAPagar, decimal valorDesconto, string formaPagamento)
    {
        var ids = contaIds.ToList();
        if (ids.Count == 0) return;

        var contas = await _ctx.ContasReceber.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.TenantId == _tenant.TenantId)
            .OrderBy(c => c.DataVencimento) // mais antiga primeiro — cliente escolheu QUAIS contas, não qual ordem
            .ToListAsync();

        if (contas.Count == 0) return;

        var saldoTotalSelecionado = contas.Sum(c => c.ValorTotal - c.ValorRecebido - c.ValorDesconto);
        var restanteAPagar    = valorAPagar;
        var restanteDesconto  = valorDesconto;

        foreach (var conta in contas)
        {
            var saldoConta = conta.ValorTotal - conta.ValorRecebido - conta.ValorDesconto;
            if (saldoConta <= 0) continue;

            // Desconto rateado proporcional ao peso dessa conta no total selecionado.
            var descontoDaConta = saldoTotalSelecionado > 0
                ? Math.Round(valorDesconto * (saldoConta / saldoTotalSelecionado), 2)
                : 0;
            descontoDaConta = Math.Min(descontoDaConta, restanteDesconto);
            descontoDaConta = Math.Min(descontoDaConta, saldoConta);

            var pagamentoDaConta = Math.Max(0, Math.Min(restanteAPagar, saldoConta - descontoDaConta));
            var dataPag = DateTime.Now;
            var agora   = DateTime.UtcNow;

            // Atômico igual DarBaixaParcialAsync — soma relativa no SQL, não
            // o valor absoluto computado a partir do snapshot lido no início
            // do método. O rateio em si (quanto vai pra cada conta) ainda
            // parte desse snapshot — risco residual menor, aceitável aqui
            // porque é o operador clicando "Receber" uma vez, não dois
            // terminais disputando a mesma conta ao mesmo tempo.
            var linhas = await _ctx.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ContasReceber
                SET ValorRecebido = ValorRecebido + {pagamentoDaConta},
                    ValorDesconto = ValorDesconto + {descontoDaConta},
                    Status = CASE
                        WHEN ValorRecebido + {pagamentoDaConta} + ValorDesconto + {descontoDaConta} >= ValorTotal THEN 'Pago'
                        ELSE 'Pendente'
                    END,
                    DataPagamento = CASE
                        WHEN ValorRecebido + {pagamentoDaConta} + ValorDesconto + {descontoDaConta} >= ValorTotal THEN {dataPag}
                        ELSE DataPagamento
                    END,
                    FormaPagamento = {formaPagamento}, UpdatedAt = {agora}
                WHERE Id = {conta.Id} AND TenantId = {_tenant.TenantId} AND Status <> 'Cancelado'");

            if (linhas > 0)
            {
                if (pagamentoDaConta > 0)
                    await RegistrarEventoAsync(conta.Id, "Pagamento", pagamentoDaConta, $"Baixa em lote ({formaPagamento})");
                if (descontoDaConta > 0)
                    await RegistrarEventoAsync(conta.Id, "Desconto", descontoDaConta, "Rateado na baixa em lote");
            }

            restanteAPagar   -= pagamentoDaConta;
            restanteDesconto -= descontoDaConta;
        }
    }

    public async Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync()
    {
        var pendentes = await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.Status == "Pendente")
            .ToListAsync();

        return (
            pendentes.Sum(c => c.ValorTotal - c.ValorRecebido - c.ValorDesconto),
            pendentes.Where(c => c.DataVencimento.Date < DateTime.Today).Sum(c => c.ValorTotal - c.ValorRecebido - c.ValorDesconto),
            pendentes.Select(c => c.CustomerId).Distinct().Count()
        );
    }

    public async Task<int> CountInadimplentesAsync()
    {
        var contas = await _uow.ContasReceber.GetAllAsync();
        return contas.Count(c => c.DataVencimento.Date < DateTime.Today && c.Status == "Pendente");
    }

    public async Task<IEnumerable<ContaReceberEvento>> GetEventosAsync(Guid contaId)
        => await _ctx.ContaReceberEventos.AsNoTracking()
            .Where(e => e.ContaReceberId == contaId)
            .OrderBy(e => e.DataEvento)
            .ToListAsync();

    public async Task<IEnumerable<ParcelaDto>> GerarParcelasAsync(GerarParcelasDto dto)
    {
        if (dto.NumeroParcelas < 1)
            throw new ArgumentException("Número de parcelas deve ser maior que zero.");

        var parcelamentoId = Guid.NewGuid();
        var valorParcela   = Math.Round(dto.ValorTotal / dto.NumeroParcelas, 2);
        var resto          = dto.ValorTotal - (valorParcela * dto.NumeroParcelas);

        var parcelas = Enumerable.Range(1, dto.NumeroParcelas).Select(i => new ContaReceber
        {
            Id             = Guid.NewGuid(),
            TenantId       = _tenant.TenantId,
            CustomerId     = dto.CustomerId,
            SaleId         = dto.SaleId,
            ValorTotal     = i == dto.NumeroParcelas ? valorParcela + resto : valorParcela,
            ValorRecebido  = 0m,
            DataEmissao    = DateTime.Now,
            DataVencimento = dto.PrimeiroVencimento.AddDays(dto.IntervalosDias * (i - 1)),
            Status         = "Pendente",
            NumeroParcela  = i,
            TotalParcelas  = dto.NumeroParcelas,
            ParcelamentoId = parcelamentoId,
            FormaPagamento = dto.FormaPagamento,
            Descricao      = string.IsNullOrWhiteSpace(dto.Descricao)
                ? $"Parcela {i}/{dto.NumeroParcelas}"
                : $"{dto.Descricao} — Parcela {i}/{dto.NumeroParcelas}"
        }).ToList();

        _ctx.ContasReceber.AddRange(parcelas);
        await _ctx.SaveChangesAsync();

        foreach (var p in parcelas)
            await RegistrarEventoAsync(p.Id, "Criada", p.ValorTotal, p.Descricao);

        return parcelas.Select(MapToParcelaDto);
    }

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByParcelamentoAsync(Guid parcelamentoId)
        => (await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.ParcelamentoId == parcelamentoId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync()).Select(MapToParcelaDto);

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByVendaAsync(Guid vendaId)
        => (await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.SaleId == vendaId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync()).Select(MapToParcelaDto);

    private static ParcelaDto MapToParcelaDto(ContaReceber c) => new()
    {
        Id             = c.Id,
        NumeroParcela  = c.NumeroParcela,
        TotalParcelas  = c.TotalParcelas,
        ValorTotal     = c.ValorTotal,
        ValorRecebido  = c.ValorRecebido,
        DataVencimento = c.DataVencimento,
        DataPagamento  = c.DataPagamento,
        Status         = c.Status,
        FormaPagamento = c.FormaPagamento,
        ParcelamentoId = c.ParcelamentoId
    };

    // S15 FIX: movido de ContasReceberController.GerarBoleto — lógica idêntica,
    // só trocando IActionResult por um resultado tipado que o controller mapeia.
    public async Task<GerarBoletoResultado> GerarBoletoAsync(Guid contaId)
    {
        // S17 FIX: _asaas é opcional (o WPF nunca registrou AsaasService no
        // próprio DI, já que gerar boleto sempre foi feature de API/Portal).
        // Checagem explícita aqui em vez de deixar estourar NullReferenceException
        // lá embaixo, no meio da lógica.
        if (_asaas is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.AsaasIndisponivel,
                Erro: "Geração de boleto não está disponível neste ambiente.");

        var conta = await _ctx.ContasReceber
            .Include(c => c.Customer)
            .Where(c => c.Id == contaId)
            .FirstOrDefaultAsync();

        if (conta is null)
            return new GerarBoletoResultado(GerarBoletoStatus.ContaNaoEncontrada);

        if (!string.IsNullOrEmpty(conta.AsaasPaymentId))
            return new GerarBoletoResultado(
                GerarBoletoStatus.JaPossuiBoleto,
                BoletoUrl: conta.BoletoUrl, InvoiceUrl: conta.InvoiceUrl,
                BoletoBarCode: conta.BoletoBarCode, AsaasStatus: conta.AsaasStatus);

        if (conta.Customer is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.ClienteNaoVinculado, Erro: "Conta sem cliente vinculado.");

        // 1. Obter/criar cliente no Asaas
        var cpfCnpj = conta.Customer.Document ?? "";
        if (string.IsNullOrEmpty(cpfCnpj))
            return new GerarBoletoResultado(
                GerarBoletoStatus.ClienteSemDocumento,
                Erro: "Cliente sem CPF/CNPJ cadastrado. Preencha antes de gerar boleto.");

        var asaasClientId = await _asaas.ObterOuCriarClienteAsync(
            conta.Customer.Name, cpfCnpj, conta.Customer.Email, conta.Customer.Phone);

        if (asaasClientId is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.FalhaAoRegistrarClienteAsaas,
                Erro: "Não foi possível registrar o cliente no Asaas. Verifique a API Key.");

        // 2. Gerar boleto
        var resultado = await _asaas.GerarBoletoAsync(
            asaasClientId,
            conta.ValorTotal - conta.ValorRecebido,
            conta.DataVencimento,
            $"{conta.Descricao} — Parcela {conta.NumeroParcela}/{conta.TotalParcelas}");

        if (resultado is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.FalhaAoGerarBoleto, Erro: "Erro ao gerar boleto no Asaas.");

        // 3. Salvar IDs na conta
        conta.AsaasPaymentId = resultado.AsaasPaymentId;
        conta.BoletoUrl      = resultado.BoletoUrl;
        conta.InvoiceUrl     = resultado.InvoiceUrl;
        conta.BoletoBarCode  = resultado.BoletoBarCode;
        conta.AsaasStatus    = resultado.Status;
        await _ctx.SaveChangesAsync();

        return new GerarBoletoResultado(
            GerarBoletoStatus.Sucesso,
            BoletoUrl: resultado.BoletoUrl, InvoiceUrl: resultado.InvoiceUrl,
            BoletoBarCode: resultado.BoletoBarCode, AsaasStatus: resultado.Status);
    }
}