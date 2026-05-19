using System.Text;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Gera arquivo EFD (Escrituração Fiscal Digital) básico para Simples Nacional.
/// Registros implementados: 0000, 0001, 0100, 0150, 0190, 0200, C001, C100, C170, C990, H001, H010, H990, 9001, 9900, 9990, 9999
/// Referência: Guia Prático EFD-ICMS/IPI v3.1.3
/// </summary>
public class SpedEfdGenerator
{
    private readonly List<string> _registros = new();
    private int _totalLinhas = 0;

    // ── Bloco 0 — Abertura e Identificação ───────────────────────────────────

    public void GerarBloco0(SpedConfig cfg)
    {
        Adicionar($"|0000|015|0|{cfg.DataInicio:ddMMyyyy}|{cfg.DataFim:ddMMyyyy}" +
                  $"|{cfg.RazaoSocial}|{cfg.CNPJ}|{cfg.IE}|{cfg.CodigoMunicipio}" +
                  $"|{cfg.IM}|{cfg.SUFRAMA}|{cfg.IndPerfil}|{cfg.IndAtividade}|");

        Adicionar("|0001|0|");

        // 0100 — Dados do Contabilista
        Adicionar($"|0100|{cfg.ContabNome}|{cfg.ContabCpf}|{cfg.ContabCrc}|||||" +
                  $"{cfg.ContabEmail}|{cfg.ContabFone}||");

        // 0150 — Tabela de Cadastro do Participante (emitente = própria empresa)
        Adicionar($"|0150|001|{cfg.RazaoSocial}|{cfg.CNPJ}|||" +
                  $"{cfg.CodigoMunicipio}||{cfg.Endereco}|||");

        // 0190 — Unidades de medida
        Adicionar("|0190|UN|UNIDADE|");
        Adicionar("|0190|SC|SACO|");
        Adicionar("|0190|MT|METRO|");
        Adicionar("|0190|KG|QUILOGRAMA|");

        Adicionar("|0990|" + ContarBloco("0") + "|");
    }

    // ── Bloco C — Documentos Fiscais de Produtos ─────────────────────────────

    public void IniciarBlocoC() => Adicionar("|C001|0|");

    public void AdicionarNota(SpedNota nota)
    {
        // C100 — NF-e / NFC-e
        Adicionar($"|C100|{nota.IndOper}|{nota.IndEmit}|001|{nota.CodModelo}" +
                  $"|{nota.CodSit}|{nota.NumDoc}|{nota.Serie}|{nota.DataEmissao:ddMMyyyy}" +
                  $"|||{nota.ValorTotal:F2}|{nota.ValorDesconto:F2}|0|0|{nota.ValorIcms:F2}" +
                  $"|{nota.ValorIpi:F2}|{nota.ValorPis:F2}|{nota.ValorCofins:F2}" +
                  $"|{nota.ChaveAcesso}||");

        // C170 — Itens da NF
        foreach (var item in nota.Itens)
        {
            Adicionar($"|C170|{item.NumItem}|{item.CodItem}|{item.Descricao}|" +
                      $"{item.Quantidade:F4}|{item.Unidade}|{item.ValorTotal:F2}|" +
                      $"{item.ValorDesconto:F2}|{item.IndMov}|{item.CstIcms}|{item.CFOP}|" +
                      $"{item.CodNat}|{item.ValorBaseIcms:F2}|{item.AliqIcms:F2}|" +
                      $"{item.ValorIcms:F2}||||||||||||||");
        }
    }

    public void EncerrarBlocoC() => Adicionar("|C990|" + ContarBloco("C") + "|");

    // ── Bloco H — Inventário ─────────────────────────────────────────────────

    public void GerarBlocoH(IEnumerable<SpedItemInventario> itens, string dtBase)
    {
        Adicionar("|H001|0|");
        Adicionar($"|H005|{dtBase}||02|");

        foreach (var item in itens)
        {
            Adicionar($"|H010|{item.CodItem}|{item.Unidade}|{item.Quantidade:F3}" +
                      $"|{item.ValorUnitario:F2}|{item.ValorItem:F2}|{item.IndProp}|" +
                      $"||{item.VlCustoPadrao:F2}|");
        }

        Adicionar("|H990|" + ContarBloco("H") + "|");
    }

    // ── Bloco 9 — Encerramento ────────────────────────────────────────────────

    public string Encerrar()
    {
        Adicionar("|9001|0|");

        // 9900 — Contagem por tipo de registro
        var grupos = _registros
            .Select(r => r.TrimStart('|').Split('|')[0])
            .GroupBy(r => r)
            .OrderBy(g => g.Key);

        foreach (var g in grupos)
            Adicionar($"|9900|{g.Key}|{g.Count()}|");

        var total = _totalLinhas + 3; // 9990 + 9999 + esta linha
        Adicionar("|9990|" + (total + 1) + "|");
        Adicionar("|9999|" + (total + 2) + "|");

        return string.Join("\r\n", _registros) + "\r\n";
    }

    private void Adicionar(string linha)
    {
        _registros.Add(linha);
        _totalLinhas++;
    }

    private int ContarBloco(string bloco) =>
        _registros.Count(r => r.TrimStart('|').StartsWith(bloco)) + 1; // +1 para o 9990
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class SpedConfig
{
    public DateTime DataInicio     { get; set; }
    public DateTime DataFim        { get; set; }
    public string   RazaoSocial    { get; set; } = string.Empty;
    public string   CNPJ           { get; set; } = string.Empty;
    public string   IE             { get; set; } = string.Empty;
    public string   CodigoMunicipio { get; set; } = string.Empty;
    public string   IM             { get; set; } = string.Empty;
    public string   SUFRAMA        { get; set; } = string.Empty;
    public string   IndPerfil      { get; set; } = "C"; // A, B ou C
    public string   IndAtividade   { get; set; } = "0"; // 0=Outros
    public string   Endereco       { get; set; } = string.Empty;
    // Contabilista
    public string ContabNome  { get; set; } = string.Empty;
    public string ContabCpf   { get; set; } = string.Empty;
    public string ContabCrc   { get; set; } = string.Empty;
    public string ContabEmail { get; set; } = string.Empty;
    public string ContabFone  { get; set; } = string.Empty;
}

public class SpedNota
{
    public string   IndOper      { get; set; } = "1"; // 0=Entrada, 1=Saída
    public string   IndEmit      { get; set; } = "0"; // 0=Próprio
    public string   CodModelo    { get; set; } = "55"; // 55=NF-e, 65=NFC-e
    public string   CodSit       { get; set; } = "00"; // 00=Normal
    public string   NumDoc       { get; set; } = string.Empty;
    public string   Serie        { get; set; } = "1";
    public DateTime DataEmissao  { get; set; }
    public decimal  ValorTotal   { get; set; }
    public decimal  ValorDesconto { get; set; }
    public decimal  ValorIcms    { get; set; }
    public decimal  ValorIpi     { get; set; }
    public decimal  ValorPis     { get; set; }
    public decimal  ValorCofins  { get; set; }
    public string   ChaveAcesso  { get; set; } = string.Empty;
    public List<SpedItemNota> Itens { get; set; } = new();
}

public class SpedItemNota
{
    public int     NumItem      { get; set; }
    public string  CodItem      { get; set; } = string.Empty;
    public string  Descricao    { get; set; } = string.Empty;
    public decimal Quantidade   { get; set; }
    public string  Unidade      { get; set; } = "UN";
    public decimal ValorTotal   { get; set; }
    public decimal ValorDesconto { get; set; }
    public string  IndMov       { get; set; } = "0";
    public string  CstIcms      { get; set; } = "400";
    public string  CFOP         { get; set; } = "5102";
    public string  CodNat       { get; set; } = "001";
    public decimal ValorBaseIcms { get; set; }
    public decimal AliqIcms     { get; set; }
    public decimal ValorIcms    { get; set; }
}

public class SpedItemInventario
{
    public string  CodItem       { get; set; } = string.Empty;
    public string  Unidade       { get; set; } = "UN";
    public decimal Quantidade    { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorItem     => Quantidade * ValorUnitario;
    public string  IndProp       { get; set; } = "0"; // 0=Próprio
    public decimal VlCustoPadrao { get; set; }
}
