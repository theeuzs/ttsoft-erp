namespace ERP.Application.Fiscal;

/// <summary>
/// O que a persistencia de uma autorizacao REALMENTE gravou (F-2). Cada indicador e conferido por leitura
/// de volta no banco, e nao deduzido de a chamada ter terminado sem excecao: o ISaleService, por exemplo,
/// simplesmente nao faz nada quando nao acha a venda.
///
/// A falha no registro da NotaFiscal NAO aparece aqui: ela continua sendo lancada, como sempre foi.
/// </summary>
public sealed record PersistenciaAutorizacaoResultado(
    bool VendaEncontrada,
    bool DadosDaVendaGravados,
    bool NumeroItemFiscalGravado)
{
    /// <summary>Nada foi gravado porque a venda nao existe (para este tenant).</summary>
    public static PersistenciaAutorizacaoResultado VendaAusente { get; } = new(false, false, false);

    public bool Completa => VendaEncontrada && DadosDaVendaGravados && NumeroItemFiscalGravado;

    /// <summary>Texto curto, sem acento, do que ficou pendente (vai para o motivo gravado e para o log).</summary>
    public string ResumoDoQueFalta()
    {
        if (!VendaEncontrada)
            return "venda nao encontrada";

        var faltas = new List<string>();
        if (!DadosDaVendaGravados) faltas.Add("dados da venda nao confirmados no banco");
        if (!NumeroItemFiscalGravado) faltas.Add("numero fiscal do item nao confirmado no banco");

        return faltas.Count == 0 ? "nada" : string.Join("; ", faltas);
    }
}
