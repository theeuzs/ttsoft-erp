// ── ERP.Domain/Entities/ContaBancaria.cs ──────────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Conta bancária cadastrada pelo tenant. Junto com Caixa, alimenta o relatório de
/// Balanço consolidado. Decisão de design (ver roadmap): mantida como entidade própria,
/// com MovimentoContaBancaria espelhando a forma de CaixaMovimento — não fundida na
/// tabela de Caixa existente, para não alterar o comportamento de algo já em produção
/// (SangriaPolicy, PDV e ContaPagar dependem do formato atual de CaixaMovimento).
/// </summary>
public class ContaBancaria : BaseEntity
{
    /// <summary>Nome de exibição escolhido pelo usuário (ex: "Conta Principal Itaú").</summary>
    public string  Apelido       { get; set; } = string.Empty;
    public string  Banco         { get; set; } = string.Empty;
    public string  Agencia       { get; set; } = string.Empty;
    public string  NumeroConta   { get; set; } = string.Empty;
    public decimal SaldoInicial  { get; set; }
    public bool    IsAtiva       { get; set; } = true;

    /// <summary>
    /// Marca qual conta recebe automaticamente os lançamentos de vendas em
    /// PIX/Cartão vindas do PDV. Só uma conta pode ser padrão por vez.
    /// </summary>
    public bool ContaPadrao { get; set; } = false;

    public List<MovimentoContaBancaria> Movimentos { get; set; } = new();
}