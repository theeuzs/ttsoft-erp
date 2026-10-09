using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Implementacao de IFiscalRecoveryStore sobre IUnitOfWork.NfePendentes. Ver as regras na interface.
///
/// ATENCAO (achado de 08/10): em producao o AppDbContext roda com QueryTrackingBehavior.NoTracking
/// (Program.cs, linha 54). Entao a entidade que o repositorio devolve esta DESANEXADA: alterar as
/// propriedades e chamar so CommitAsync() NAO grava nada, sem erro algum. Por isso toda gravacao
/// aqui chama Update(entidade) antes do commit (o mesmo padrao do NfeContingencyService). Os testes
/// usam um contexto NoTracking, como a producao, para que esse esquecimento nao passe despercebido.
///
/// Update(entidade) marca a linha inteira como modificada: as colunas nao tocadas voltam com o
/// valor que acabou de ser lido (inclusive as legadas, que por isso permanecem como estavam).
/// </summary>
public sealed class FiscalRecoveryStore : IFiscalRecoveryStore
{
    public const int TamanhoMaximoUltimaDecisao = 500;

    private readonly IUnitOfWork _uow;

    public FiscalRecoveryStore(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<IReadOnlyList<NfePendente>> ObterElegiveisAsync(string tipoNota, DateTime agoraUtc)
    {
        if (string.IsNullOrWhiteSpace(tipoNota))
            throw new ArgumentException("TipoNota obrigatorio.", nameof(tipoNota));

        ExigirUtc(agoraUtc);

        // INfePendenteRepository so expoe GetAllAsync (nao ha FindAsync): o filtro de tenant do
        // contexto ja vale aqui, e a fila e pequena, entao o restante filtra em memoria.
        var todas = await _uow.NfePendentes.GetAllAsync();

        return todas
            .Where(n => string.Equals(n.TipoNota, tipoNota, StringComparison.Ordinal))
            .Where(n => PodeSerProcessada(n.Estado))
            .Where(n => n.ProximaTentativaEm is null || n.ProximaTentativaEm.Value <= agoraUtc)
            .OrderBy(n => n.DataFalha)
            .ToList();
    }

    public async Task<bool> RegravarPayloadAsync(Guid id, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload vazio.", nameof(payloadJson));

        var nota = await ObterProcessavelAsync(id);
        if (nota is null)
            return false;

        nota.PayloadJson = payloadJson;

        _uow.NfePendentes.Update(nota);
        await _uow.CommitAsync();
        return true;
    }

    public async Task<bool> AplicarDecisaoAsync(
        Guid id, RecoveryDecision decisao, DateTime agoraUtc, bool consultou, bool postou)
    {
        ArgumentNullException.ThrowIfNull(decisao);
        ExigirUtc(agoraUtc);

        if (decisao.NovoEstado is null)
            throw new ArgumentException("Decisao de remocao deve usar RemoverAsync.", nameof(decisao));

        if (!NfePendenteEstados.EhValido(decisao.NovoEstado))
            throw new ArgumentException($"Estado invalido na decisao: '{decisao.NovoEstado}'.", nameof(decisao));

        if (decisao.FalhasTransitoriasSeguidas < 0 || decisao.FalhasDesconhecidasSeguidas < 0)
            throw new ArgumentException("Os contadores de falhas seguidas nao podem ser negativos.", nameof(decisao));

        var nota = await ObterProcessavelAsync(id);
        if (nota is null)
            return false;

        nota.Estado = decisao.NovoEstado;
        nota.ProximaTentativaEm = decisao.ProximaTentativaEm?.UtcDateTime;
        nota.FalhasTransitoriasSeguidas = decisao.FalhasTransitoriasSeguidas;
        nota.FalhasDesconhecidasSeguidas = decisao.FalhasDesconhecidasSeguidas;
        nota.UltimaDecisao = Truncar(decisao.Motivo ?? string.Empty);

        if (consultou)
        {
            nota.UltimaConsultaEm = agoraUtc;
            nota.TentativasConsulta += 1;
        }

        if (postou)
        {
            nota.UltimoPostEm = agoraUtc;
            nota.TentativasPost += 1;
        }

        _uow.NfePendentes.Update(nota);
        await _uow.CommitAsync();
        return true;
    }

    public async Task<bool> RemoverAsync(Guid id)
    {
        var nota = await ObterProcessavelAsync(id);
        if (nota is null)
            return false;

        _uow.NfePendentes.Remove(nota);
        await _uow.CommitAsync();
        return true;
    }

    /// <summary>
    /// Le a pendencia do banco (filtro de tenant aplicado) e so a devolve se o estado GRAVADO
    /// permitir mexer nela. IntervencaoManual (terminal) e qualquer estado desconhecido -> nulo.
    /// </summary>
    private async Task<NfePendente?> ObterProcessavelAsync(Guid id)
    {
        var nota = await _uow.NfePendentes.GetByIdAsync(id);

        return nota is not null && PodeSerProcessada(nota.Estado) ? nota : null;
    }

    private static bool PodeSerProcessada(string? estado) =>
        estado is NfePendenteEstados.Ativa or NfePendenteEstados.AguardandoCorrecao;

    private static void ExigirUtc(DateTime agoraUtc)
    {
        if (agoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("O horario precisa ter Kind=Utc.", "agoraUtc");
    }

    /// <summary>Corta em 500 caracteres sem partir um par substituto (emoji) ao meio.</summary>
    private static string Truncar(string texto)
    {
        if (texto.Length <= TamanhoMaximoUltimaDecisao)
            return texto;

        var corte = TamanhoMaximoUltimaDecisao;
        if (char.IsHighSurrogate(texto[corte - 1]))
            corte--;

        return texto.Substring(0, corte);
    }
}
