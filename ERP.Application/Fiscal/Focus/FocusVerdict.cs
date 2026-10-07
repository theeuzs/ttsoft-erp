namespace ERP.Application.Fiscal.Focus;

/// <summary>Em que operacao a resposta foi obtida. Algumas respostas so fazem sentido em uma delas.</summary>
public enum FocusOperation
{
    Post = 1,
    Get = 2
}

/// <summary>
/// O que a Focus DISSE. Nao diz o que o ERP pode fazer: isso e papel da
/// politica de recuperacao (Etapa 4A-4), separada de proposito.
/// </summary>
public enum FocusVerdictKind
{
    /// <summary>200 + status autorizado.</summary>
    Autorizado = 1,
    /// <summary>200 + status processando_autorizacao.</summary>
    Processando,
    /// <summary>200 + status denegado (a ref fica consumida).</summary>
    Denegado,
    /// <summary>200 + status erro_autorizacao; ver FocusVerdict.StatusSefaz (704 = data atrasada).</summary>
    RejeicaoFiscal,
    /// <summary>GET 404 com codigo nao_encontrado. A regra de tratar isso como "nao recebida" e da politica.</summary>
    NaoEncontrado,
    /// <summary>POST 409/422 com codigo already_processed (ja autorizada).</summary>
    JaProcessado,
    /// <summary>POST 409/422 com codigo pending_operation (ainda em processamento).</summary>
    OperacaoPendente,
    /// <summary>POST 422 com outro codigo (validacao do payload); definitivo.</summary>
    RejeicaoDefinitiva,
    /// <summary>Sem resposta HTTP, 408, 429 ou 5xx: pode melhorar sozinho, tentar de novo com backoff.</summary>
    Transitorio,
    /// <summary>401/403: problema de credencial/configuracao, nao da nota.</summary>
    ErroDeConfiguracao,
    /// <summary>400: requisicao invalida, nao se corrige com o passar do tempo.</summary>
    ErroDeRequisicao,
    /// <summary>Qualquer combinacao nao prevista. NUNCA deve gerar acao automatica sobre o documento.</summary>
    Desconhecido
}

public sealed record FocusVerdict(
    FocusVerdictKind Kind,
    string? StatusSefaz = null,
    string? Detalhe = null)
{
    /// <summary>Rejeicao 704: NFC-e com data/hora de emissao atrasada (tolerancia de 5 minutos).</summary>
    public bool EhRejeicao704 =>
        Kind == FocusVerdictKind.RejeicaoFiscal && StatusSefaz == "704";
}
