using System.Globalization;

namespace ERP.Application.Fiscal.Recovery;

public enum MotivoRegeneracao
{
    Permitida = 1,
    /// <summary>PermitirRegeneracaoDataEmissao esta desligado (padrao).</summary>
    Desligada,
    /// <summary>Ligado, mas a janela (N) nao foi configurada ou e invalida.</summary>
    JanelaNaoConfigurada,
    /// <summary>O payload nao tem um DataEmissao valido (com offset) para medir a idade.</summary>
    PayloadSemDataValida,
    /// <summary>Agora nao e posterior ao DataEmissao original.</summary>
    DataNaoPosterior,
    /// <summary>A idade do DataEmissao original passa da janela aprovada.</summary>
    ForaDaJanela,
    /// <summary>A nova data cairia em outro mes/ano (no fuso da data original = Brasil).</summary>
    CruzaMesOuAno
}

/// <summary>
/// D8: regras para regenerar DataEmissao automaticamente. Funcao pura.
/// A idade e medida pelo DataEmissao CONGELADO no PayloadJson, nunca pela
/// data da venda: e esse timestamp que a SEFAZ julga.
/// </summary>
public static class DataEmissaoRegeneracao
{
    /// <summary>Formato que FusoBrasilHelper.AgoraNoBrasilComOffset gera: 2026-10-07T21:38:20-03:00.</summary>
    private const string FormatoDataEmissao = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>
    /// Le o DataEmissao do payload. Estrito: exige o offset no texto (sem ele
    /// o .NET assumiria o fuso do servidor, o erro que o FusoBrasilHelper
    /// existe para evitar). Qualquer outra coisa devolve nulo.
    /// </summary>
    public static DateTimeOffset? ParseDataEmissao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return DateTimeOffset.TryParseExact(
            texto.Trim(), FormatoDataEmissao, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var valor)
            ? valor
            : null;
    }

    /// <summary>
    /// Ordem das checagens (a primeira que falha e o motivo devolvido):
    /// desligada, janela nao configurada, payload sem data, data nao
    /// posterior, fora da janela, cruza mes/ano.
    /// O mes e comparado no offset do proprio DataEmissao (Brasil), nao em UTC.
    /// </summary>
    public static MotivoRegeneracao Avaliar(
        DateTimeOffset? dataEmissaoOriginal,
        DateTimeOffset agora,
        RecoveryPolicyOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        if (!opcoes.PermitirRegeneracaoDataEmissao)
            return MotivoRegeneracao.Desligada;

        if (opcoes.JanelaMaximaRegeneracaoHoras is null || opcoes.JanelaMaximaRegeneracaoHoras <= 0)
            return MotivoRegeneracao.JanelaNaoConfigurada;

        if (dataEmissaoOriginal is null)
            return MotivoRegeneracao.PayloadSemDataValida;

        var original = dataEmissaoOriginal.Value;

        if (agora <= original)
            return MotivoRegeneracao.DataNaoPosterior;

        var janela = TimeSpan.FromHours(opcoes.JanelaMaximaRegeneracaoHoras.Value);
        if (agora - original > janela)
            return MotivoRegeneracao.ForaDaJanela;

        var agoraNoFusoOriginal = agora.ToOffset(original.Offset);
        if (agoraNoFusoOriginal.Year != original.Year || agoraNoFusoOriginal.Month != original.Month)
            return MotivoRegeneracao.CruzaMesOuAno;

        return MotivoRegeneracao.Permitida;
    }
}
