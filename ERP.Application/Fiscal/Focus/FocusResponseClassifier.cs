namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Classificador unico das respostas da Focus (Etapa 4). Funcao pura: nao
/// conhece venda, banco, cliente HTTP nem a fila. Substitui a interpretacao
/// por texto livre (mensagem.Contains("UnprocessableEntity")) espalhada hoje.
///
/// Decide por status HTTP + codigo + status + status_sefaz, nunca por
/// substring de mensagem.
/// </summary>
public static class FocusResponseClassifier
{
    public static FocusVerdict Classify(FocusResponse response, FocusOperation operation)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.TransportError is not null || response.HttpStatus == 0)
        {
            return new FocusVerdict(
                FocusVerdictKind.Transitorio,
                Detalhe: response.TransportError ?? "Sem resposta HTTP.");
        }

        var http = response.HttpStatus;

        if (http is >= 200 and <= 299)
            return ClassificarDocumento(response);

        switch (http)
        {
            case 404:
                // A regra especial "404 = referencia nao recebida" vale so no
                // GET por ref e so com o codigo documentado. Qualquer outro
                // 404 (POST em endpoint errado, proxy, HTML) e desconhecido.
                if (operation == FocusOperation.Get && Igual(response.Codigo, "nao_encontrado"))
                    return new FocusVerdict(FocusVerdictKind.NaoEncontrado, Detalhe: response.Mensagem);
                return Desconhecido(response);

            case 409:
            case 422:
                return ClassificarConflito(response, operation, http);

            case 401:
            case 403:
                return new FocusVerdict(
                    FocusVerdictKind.ErroDeConfiguracao,
                    Detalhe: $"HTTP {http}: {response.Codigo ?? response.Mensagem}");

            case 400:
                return new FocusVerdict(
                    FocusVerdictKind.ErroDeRequisicao,
                    Detalhe: $"HTTP 400: {response.Codigo ?? response.Mensagem}");

            case 408:
            case 429:
                return new FocusVerdict(FocusVerdictKind.Transitorio, Detalhe: $"HTTP {http}");
        }

        if (http is >= 500 and <= 599)
            return new FocusVerdict(FocusVerdictKind.Transitorio, Detalhe: $"HTTP {http}");

        return Desconhecido(response);
    }

    private static FocusVerdict ClassificarDocumento(FocusResponse response)
    {
        if (Igual(response.Status, "autorizado"))
            return new FocusVerdict(FocusVerdictKind.Autorizado, response.StatusSefaz);

        if (Igual(response.Status, "processando_autorizacao"))
            return new FocusVerdict(FocusVerdictKind.Processando, response.StatusSefaz);

        if (Igual(response.Status, "denegado"))
            return new FocusVerdict(FocusVerdictKind.Denegado, response.StatusSefaz, response.MensagemSefaz);

        if (Igual(response.Status, "erro_autorizacao"))
            return new FocusVerdict(FocusVerdictKind.RejeicaoFiscal, response.StatusSefaz, response.MensagemSefaz);

        // Inclui "cancelado": no ciclo de recuperacao nao deve ocorrer; se
        // ocorrer, cai em Desconhecido e vai para intervencao, nunca para
        // reenvio automatico.
        return Desconhecido(response);
    }

    private static FocusVerdict ClassificarConflito(FocusResponse response, FocusOperation operation, int http)
    {
        // Os codigos already_processed / pending_operation foram confirmados
        // pelo suporte da Focus para o segundo POST de NFC-e. No GET, 409/422
        // nao e esperado.
        if (operation != FocusOperation.Post)
            return Desconhecido(response);

        if (Igual(response.Codigo, "already_processed"))
            return new FocusVerdict(FocusVerdictKind.JaProcessado, Detalhe: response.Mensagem);

        if (Igual(response.Codigo, "pending_operation"))
            return new FocusVerdict(FocusVerdictKind.OperacaoPendente, Detalhe: response.Mensagem);

        // 422 com outro codigo: validacao do payload, definitiva (comportamento
        // atual preservado). 422 sem codigo (corpo nao-JSON) e 409 com outro
        // codigo nao sao tratados como definitivos: Desconhecido.
        if (http == 422 && !string.IsNullOrWhiteSpace(response.Codigo))
        {
            return new FocusVerdict(
                FocusVerdictKind.RejeicaoDefinitiva,
                Detalhe: $"{response.Codigo}: {response.Mensagem}");
        }

        return Desconhecido(response);
    }

    private static FocusVerdict Desconhecido(FocusResponse response) =>
        new(FocusVerdictKind.Desconhecido,
            response.StatusSefaz,
            $"HTTP {response.HttpStatus}; codigo={response.Codigo ?? "-"}; status={response.Status ?? "-"}");

    private static bool Igual(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
