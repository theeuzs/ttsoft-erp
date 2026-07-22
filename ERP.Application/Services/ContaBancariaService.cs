// ── ERP.Application/Services/ContaBancariaService.cs ──────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class ContaBancariaService : IContaBancariaService
{
    private readonly IUnitOfWork _uow;
    public ContaBancariaService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ContaBancariaDto>> ObterContasAtivasAsync()
    {
        var contas = await _uow.ContasBancarias.GetAllAtivasAsync();
        var result = new List<ContaBancariaDto>();

        foreach (var c in contas)
        {
            var saldo = await _uow.ContasBancarias.GetSaldoAsync(c.Id);
            result.Add(MapDto(c, saldo));
        }
        return result;
    }

    public async Task<ContaBancariaDto?> ObterPorIdAsync(Guid id)
    {
        var conta = await _uow.ContasBancarias.GetByIdAsync(id);
        if (conta is null) return null;

        var saldo = await _uow.ContasBancarias.GetSaldoAsync(id);
        return MapDto(conta, saldo);
    }

    public async Task CriarContaAsync(CriarContaBancariaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Apelido))
            throw new InvalidOperationException("Informe um apelido para a conta (ex: 'Conta Principal').");

        var conta = new ContaBancaria
        {
            Apelido      = dto.Apelido.Trim(),
            Banco        = dto.Banco?.Trim()       ?? string.Empty,
            Agencia      = dto.Agencia?.Trim()     ?? string.Empty,
            NumeroConta  = dto.NumeroConta?.Trim() ?? string.Empty,
            SaldoInicial = dto.SaldoInicial,
            IsAtiva      = true
        };

        await _uow.ContasBancarias.AddAsync(conta);
        await _uow.CommitAsync();
    }

    public async Task InativarContaAsync(Guid id)
    {
        var conta = await _uow.ContasBancarias.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Conta bancária não encontrada.");

        conta.IsAtiva = false;
        _uow.ContasBancarias.Update(conta);
        await _uow.CommitAsync();
    }

    public async Task RegistrarMovimentoAsync(
        Guid contaBancariaId, decimal valor, string descricao, TipoMovimentoContaBancaria tipo,
        OrigemMovimentoFinanceiro origemTipo = OrigemMovimentoFinanceiro.Manual, Guid? origemId = null)
    {
        if (valor <= 0)
            throw new InvalidOperationException("O valor do lançamento deve ser maior que zero.");

        var conta = await _uow.ContasBancarias.GetByIdAsync(contaBancariaId)
            ?? throw new InvalidOperationException("Conta bancária não encontrada.");

        if (!conta.IsAtiva)
            throw new InvalidOperationException("Não é possível lançar em uma conta inativa.");

        var movimento = new MovimentoContaBancaria
        {
            ContaBancariaId = contaBancariaId,
            Valor           = valor,
            Descricao       = string.IsNullOrWhiteSpace(descricao) ? "Lançamento manual" : descricao.Trim(),
            Tipo            = tipo,
            DataHora        = DateTime.Now,
            OrigemTipo      = origemTipo,
            OrigemId        = origemId
        };

        await _uow.ContasBancarias.AddMovimentoAsync(movimento);
        await _uow.CommitAsync();
    }

    public async Task<IReadOnlyList<MovimentoContaBancariaDto>> ObterExtratoAsync(Guid contaBancariaId)
    {
        var movimentos = await _uow.ContasBancarias.GetMovimentosAsync(contaBancariaId);
        return movimentos.Select(m => new MovimentoContaBancariaDto
        {
            Id         = m.Id,
            DataHora   = m.DataHora,
            Tipo       = m.Tipo,
            Descricao  = m.Descricao,
            Valor      = m.Valor,
            OrigemTipo = m.OrigemTipo,
            OrigemId   = m.OrigemId
        }).ToList();
    }

    public async Task<PosicaoFinanceiraDto> ObterPosicaoFinanceiraAsync()
    {
        // Soma de todos os Caixas ABERTOS agora (qualquer operador) — reaproveita
        // GetSaldoDinheiroAsync já existente e testado, sem tocar em CaixaRepository.
        var todosCaixas   = await _uow.Caixas.GetAllAsync();
        var caixasAbertos = todosCaixas.Where(c => c.Status == StatusCaixa.Aberto);

        decimal saldoCaixas = 0m;
        foreach (var caixa in caixasAbertos)
            saldoCaixas += await _uow.Caixas.GetSaldoDinheiroAsync(caixa.Id);

        var contasAtivas = await _uow.ContasBancarias.GetAllAtivasAsync();
        var resumoContas = new List<SaldoContaBancariaResumoDto>();

        foreach (var conta in contasAtivas)
        {
            var saldo = await _uow.ContasBancarias.GetSaldoAsync(conta.Id);
            resumoContas.Add(new SaldoContaBancariaResumoDto(conta.Id, conta.Apelido, saldo));
        }

        var saldoTotalContas = resumoContas.Sum(c => c.Saldo);

        return new PosicaoFinanceiraDto(
            SaldoTotalCaixasAbertos:    saldoCaixas,
            ContasBancarias:            resumoContas,
            SaldoTotalContasBancarias:  saldoTotalContas,
            SaldoConsolidado:           saldoCaixas + saldoTotalContas);
    }

    // ── Conciliação Bancária ──────────────────────────────────────────────────
    public async Task<IReadOnlyList<SugestaoConciliacaoDto>> ProcessarExtratoOfxAsync(
        Guid contaBancariaId, string conteudoOfx)
    {
        var transacoes = OfxParser.Parse(conteudoOfx);
        var sugestoes = new List<SugestaoConciliacaoDto>();

        foreach (var t in transacoes)
        {
            // S17 FIX: se essa transação (pelo FitId) já foi processada numa
            // importação anterior, pula — evita reconciliar/recriar a mesma
            // linha se o mesmo arquivo (ou período sobreposto) for importado de novo.
            if (await _uow.ContasBancarias.FitIdJaProcessadoAsync(contaBancariaId, t.FitId))
                continue;

            var tipo = t.Valor < 0 ? TipoMovimentoContaBancaria.Saida : TipoMovimentoContaBancaria.Entrada;
            var valorAbsoluto = Math.Abs(t.Valor);

            // Janela de 3 dias pra cada lado — bancos às vezes postam com atraso
            // em relação ao dia em que o lançamento foi feito no sistema.
            var candidatos = (await _uow.ContasBancarias.BuscarCandidatosConciliacaoAsync(
                contaBancariaId, valorAbsoluto, tipo, t.Data.AddDays(-3), t.Data.AddDays(3))).ToList();

            var sugestao = new SugestaoConciliacaoDto
            {
                FitId                = t.FitId,
                Data                 = t.Data,
                Valor                = t.Valor,
                Descricao            = t.Descricao,
                QuantidadeCandidatos = candidatos.Count
            };

            // Só sugere automaticamente quando existe exatamente um candidato —
            // ambíguo (0 ou 2+) fica para o usuário decidir manualmente.
            if (candidatos.Count == 1)
            {
                var unico = candidatos[0];
                sugestao.MovimentoSugeridoId        = unico.Id;
                sugestao.MovimentoSugeridoDescricao = unico.Descricao;
                sugestao.MovimentoSugeridoData      = unico.DataHora;
            }

            sugestoes.Add(sugestao);
        }

        return sugestoes;
    }

    public async Task ConfirmarConciliacaoAsync(Guid movimentoId, string fitId)
        => await _uow.ContasBancarias.MarcarConciliadoAsync(movimentoId, fitId);

    public async Task CriarEConciliarAsync(Guid contaBancariaId, SugestaoConciliacaoDto transacaoOfx)
    {
        var tipo = transacaoOfx.Valor < 0 ? TipoMovimentoContaBancaria.Saida : TipoMovimentoContaBancaria.Entrada;

        var movimento = new MovimentoContaBancaria
        {
            ContaBancariaId = contaBancariaId,
            Valor           = Math.Abs(transacaoOfx.Valor),
            Descricao       = transacaoOfx.Descricao,
            Tipo            = tipo,
            DataHora        = transacaoOfx.Data,
            Conciliado      = true, // já nasce conciliado: veio confirmado do próprio extrato do banco.
            FitId           = transacaoOfx.FitId,
            OrigemTipo      = OrigemMovimentoFinanceiro.ConciliacaoOfx
        };

        await _uow.ContasBancarias.AddMovimentoAsync(movimento);
        await _uow.CommitAsync();
    }

    // ── Conta padrão (recebe vendas PIX/Cartão automaticamente do PDV) ────────
    public async Task<ContaBancariaDto?> ObterContaPadraoAsync()
    {
        var conta = await _uow.ContasBancarias.GetContaPadraoAsync();
        if (conta is null) return null;

        var saldo = await _uow.ContasBancarias.GetSaldoAsync(conta.Id);
        return MapDto(conta, saldo);
    }

    public async Task DefinirComoContaPadraoAsync(Guid contaBancariaId)
        => await _uow.ContasBancarias.DefinirContaPadraoAsync(contaBancariaId);

    public async Task RegistrarRecebimentoVendaAsync(Guid? vendaId, decimal valor, string descricao)
    {
        // S17 FIX: fecha o ciclo PDV → Financeiro. Antes, vendas em PIX/Cartão só
        // tocavam o CaixaMovimento (corretamente, sem contar como dinheiro físico),
        // mas nunca criavam nada em Conta Bancária — a Conciliação Bancária nunca
        // teria dado real pra conciliar, só lançamento manual de teste.
        var contaPadrao = await _uow.ContasBancarias.GetContaPadraoAsync();
        if (contaPadrao is null) return; // sem conta padrão configurada — não trava a venda por isso.

        await _uow.ContasBancarias.AddMovimentoAsync(new MovimentoContaBancaria
        {
            ContaBancariaId = contaPadrao.Id,
            Valor           = valor,
            Descricao       = descricao,
            OrigemTipo      = OrigemMovimentoFinanceiro.Venda,
            OrigemId        = vendaId,
            Tipo            = TipoMovimentoContaBancaria.Entrada,
            DataHora        = DateTime.Now
        });
        await _uow.CommitAsync();
    }

    public async Task RegistrarEstornoVendaAsync(Guid vendaId, decimal valor, string descricao)
    {
        // S17 FIX cancelamento: NUNCA apaga ou edita a entrada original — cria
        // uma Saída compensatória nova, preservando o histórico de auditoria
        // no Extrato Financeiro (a mesma venda aparece duas vezes: a entrada
        // original e o estorno, exatamente como um extrato bancário de verdade
        // mostraria).
        var contaPadrao = await _uow.ContasBancarias.GetContaPadraoAsync();
        if (contaPadrao is null) return; // sem conta padrão — a entrada original também não teria sido criada.

        await _uow.ContasBancarias.AddMovimentoAsync(new MovimentoContaBancaria
        {
            ContaBancariaId = contaPadrao.Id,
            Valor           = valor,
            Descricao       = descricao,
            OrigemTipo      = OrigemMovimentoFinanceiro.Venda,
            OrigemId        = vendaId,
            Tipo            = TipoMovimentoContaBancaria.Saida,
            DataHora        = DateTime.Now
        });
        await _uow.CommitAsync();
    }

    public async Task<IReadOnlyList<MovimentoContaBancariaDto>> ObterNaoConciliadosAsync(Guid contaBancariaId)
    {
        var movimentos = await _uow.ContasBancarias.GetNaoConciliadosAsync(contaBancariaId);
        return movimentos.Select(m => new MovimentoContaBancariaDto
        {
            Id         = m.Id,
            DataHora   = m.DataHora,
            Tipo       = m.Tipo,
            Descricao  = m.Descricao,
            Valor      = m.Valor,
            OrigemTipo = m.OrigemTipo,
            OrigemId   = m.OrigemId
        }).ToList();
    }

    private static ContaBancariaDto MapDto(ContaBancaria c, decimal saldo) => new()
    {
        Id           = c.Id,
        Apelido      = c.Apelido,
        Banco        = c.Banco,
        Agencia      = c.Agencia,
        NumeroConta  = c.NumeroConta,
        SaldoInicial = c.SaldoInicial,
        SaldoAtual   = saldo,
        IsAtiva      = c.IsAtiva,
        ContaPadrao  = c.ContaPadrao
    };
}