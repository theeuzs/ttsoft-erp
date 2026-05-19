using System.Text;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Gera arquivo EFD-Contribuições para PIS e COFINS.
/// Registros: 0000, 0001, 0100, 0110, 0150, 0190, 0200,
///            A001, A010, A100, A170, A990,
///            C001, C010, C100, C170, C990,
///            F001, F010, F100, F990,
///            1001, 1100, 1990,
///            9001, 9900, 9990, 9999
/// Referência: Guia Prático EFD-Contribuições v1.34
/// </summary>
public class SpedContribuicoesGenerator
{
    private readonly List<string> _registros = new();

    // ── Bloco 0 — Abertura ────────────────────────────────────────────────────

    public void GerarBloco0(SpedConfig cfg, string incidencia = "1")
    {
        // Tipo incidência: 1=Cumulativo, 2=Não-cumulativo, 3=Ambos
        Add($"|0000|002|0|{cfg.DataInicio:ddMMyyyy}|{cfg.DataFim:ddMMyyyy}" +
            $"|{cfg.RazaoSocial}|{cfg.CNPJ}|{cfg.IE}||{cfg.CodigoMunicipio}" +
            $"|{incidencia}|{cfg.IndAtividade}|");

        Add("|0001|0|");
        Add($"|0100|{cfg.ContabNome}|{cfg.ContabCpf}|{cfg.ContabCrc}||||{cfg.ContabEmail}||");
        Add($"|0110|{incidencia}|{cfg.IndAtividade}|");
        Add($"|0150|001|{cfg.RazaoSocial}|{cfg.CNPJ}||{cfg.CodigoMunicipio}||{cfg.Endereco}|||");
        Add("|0190|UN|UNIDADE|");
        Add("|0990|" + ContarBloco("0") + "|");
    }

    // ── Bloco A — Serviços (NFS-e) ────────────────────────────────────────────

    public void IniciarBlocoA(string cnpj) 
    { 
        Add("|A001|0|"); 
        Add($"|A010|{cnpj}|");
    }

    public void AdicionarNotaServico(SpedNotaServico nota)
    {
        Add($"|A100|{nota.IndOper}|{nota.IndEmit}|001|{nota.CodSit}|" +
            $"{nota.NumDoc}|{nota.DataEmissao:ddMMyyyy}|{nota.ValorTotal:F2}|" +
            $"{nota.ValorIss:F2}|{nota.ValorPis:F2}|{nota.ValorCofins:F2}|");

        foreach (var item in nota.Itens)
            Add($"|A170|{item.NumItem}|{item.Descricao}|{item.ValorTotal:F2}|" +
                $"{item.Cst}|{item.AliqPis:F4}|{item.ValorPis:F2}|" +
                $"{item.AliqCofins:F4}|{item.ValorCofins:F2}|");
    }

    public void EncerrarBlocoA() => Add("|A990|" + ContarBloco("A") + "|");

    // ── Bloco C — Produtos (NF-e/NFC-e) ──────────────────────────────────────

    public void IniciarBlocoC(string cnpj) 
    { 
        Add("|C001|0|"); 
        Add($"|C010|{cnpj}|");
    }

    public void AdicionarNotaProduto(SpedNotaContrib nota)
    {
        Add($"|C100|{nota.IndOper}|{nota.IndEmit}|001|{nota.CodModelo}|{nota.CodSit}|" +
            $"{nota.NumDoc}|{nota.Serie}|{nota.ChaveAcesso}|{nota.DataEmissao:ddMMyyyy}|" +
            $"|{nota.ValorTotal:F2}|{nota.ValorDesconto:F2}|{nota.ValorPis:F2}|{nota.ValorCofins:F2}|");

        foreach (var item in nota.Itens)
        {
            Add($"|C170|{item.NumItem}|{item.CodItem}|{item.Descricao}|{item.Quantidade:F4}|" +
                $"{item.Unidade}|{item.ValorTotal:F2}|{item.ValorDesconto:F2}|" +
                $"{item.IndMov}|{item.CstPis}|{item.AliqPis:F4}|{item.ValorPis:F2}|" +
                $"{item.CstCofins}|{item.AliqCofins:F4}|{item.ValorCofins:F2}|{item.CFOP}|" +
                $"{item.ValorReceita:F2}|");
        }
    }

    public void EncerrarBlocoC() => Add("|C990|" + ContarBloco("C") + "|");

    // ── Bloco F — Demais Documentos ───────────────────────────────────────────

    public void GerarBlocoF(string cnpj, decimal totalReceitas)
    {
        Add("|F001|0|");
        Add($"|F010|{cnpj}|");
        Add($"|F100|1|||||{totalReceitas:F2}|{totalReceitas:F2}|01||||||||||");
        Add("|F990|" + ContarBloco("F") + "|");
    }

    // ── Bloco 1 — Complemento ─────────────────────────────────────────────────

    public void GerarBloco1(decimal totalPis, decimal totalCofins)
    {
        Add("|1001|0|");
        Add($"|1100|{DateTime.Now.Year}{DateTime.Now.Month:D2}|01|{totalPis:F2}|||||||||||");
        Add("|1990|" + ContarBloco("1") + "|");
    }

    // ── Encerramento ──────────────────────────────────────────────────────────

    public string Encerrar()
    {
        Add("|9001|0|");

        var grupos = _registros
            .Select(r => r.TrimStart('|').Split('|')[0])
            .GroupBy(r => r)
            .OrderBy(g => g.Key);

        foreach (var g in grupos)
            Add($"|9900|{g.Key}|{g.Count()}|");

        var total = _registros.Count + 2;
        Add("|9990|" + (total + 1) + "|");
        Add("|9999|" + (total + 2) + "|");

        return string.Join("\r\n", _registros) + "\r\n";
    }

    private void Add(string linha) => _registros.Add(linha);

    private int ContarBloco(string bloco) =>
        _registros.Count(r => r.TrimStart('|').StartsWith(bloco)) + 1;
}

// ── DTOs EFD-Contribuições ────────────────────────────────────────────────────

public class SpedNotaServico
{
    public string   IndOper      { get; set; } = "1";
    public string   IndEmit      { get; set; } = "0";
    public string   CodSit       { get; set; } = "00";
    public string   NumDoc       { get; set; } = string.Empty;
    public DateTime DataEmissao  { get; set; }
    public decimal  ValorTotal   { get; set; }
    public decimal  ValorIss     { get; set; }
    public decimal  ValorPis     { get; set; }
    public decimal  ValorCofins  { get; set; }
    public List<SpedItemServico> Itens { get; set; } = new();
}

public class SpedItemServico
{
    public int     NumItem    { get; set; }
    public string  Descricao  { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public string  Cst        { get; set; } = "07"; // Simples: isento
    public decimal AliqPis    { get; set; } = 0.0065m;
    public decimal ValorPis   { get; set; }
    public decimal AliqCofins { get; set; } = 0.03m;
    public decimal ValorCofins { get; set; }
}

public class SpedNotaContrib
{
    public string   IndOper      { get; set; } = "1";
    public string   IndEmit      { get; set; } = "0";
    public string   CodModelo    { get; set; } = "55";
    public string   CodSit       { get; set; } = "00";
    public string   NumDoc       { get; set; } = string.Empty;
    public string   Serie        { get; set; } = "1";
    public string   ChaveAcesso  { get; set; } = string.Empty;
    public DateTime DataEmissao  { get; set; }
    public decimal  ValorTotal   { get; set; }
    public decimal  ValorDesconto { get; set; }
    public decimal  ValorPis     { get; set; }
    public decimal  ValorCofins  { get; set; }
    public List<SpedItemContrib> Itens { get; set; } = new();
}

public class SpedItemContrib
{
    public int     NumItem      { get; set; }
    public string  CodItem      { get; set; } = string.Empty;
    public string  Descricao    { get; set; } = string.Empty;
    public decimal Quantidade   { get; set; }
    public string  Unidade      { get; set; } = "UN";
    public decimal ValorTotal   { get; set; }
    public decimal ValorDesconto { get; set; }
    public string  IndMov       { get; set; } = "0";
    public string  CstPis       { get; set; } = "07";
    public decimal AliqPis      { get; set; } = 0m;
    public decimal ValorPis     { get; set; } = 0m;
    public string  CstCofins    { get; set; } = "07";
    public decimal AliqCofins   { get; set; } = 0m;
    public decimal ValorCofins  { get; set; } = 0m;
    public string  CFOP         { get; set; } = "5102";
    public decimal ValorReceita { get; set; }
}
