using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfeStatusService
{
    // 👇 Mudou de ConsultarStatusNfceAsync para ConsultarStatusNotaAsync
    // Chave/Numero/UrlXml/TipoDocumentoEncontrado adicionados no fim de
    // propósito (achado implementando NfeStatusReconciliationHostedService,
    // 25/09): sem Chave/Numero, reconciliar uma venda "Processando" pra
    // "Autorizada" nunca conseguiria gravar a chave — mesmo gap que
    // EmitirNfceAsync/EmitirNfeA4Async já tinham, corrigido lá, só que aqui
    // do lado da consulta. TipoDocumentoEncontrado ("NFE"/"NFCE"/"") resolve
    // outra ambiguidade: o método tenta os dois endpoints e nunca dizia qual
    // respondeu — sem isso não dá pra saber se a NotaFiscal de reconciliação
    // deve ser Tipo="NFE" ou "NFCE". Quem só acessa por nome não quebra;
    // quem desestrutura precisa dos 4 nomes novos.
    Task<(bool Sucesso, string Status, string UrlDanfe, string Chave, string Numero, string UrlXml, string TipoDocumentoEncontrado)> ConsultarStatusNotaAsync(string referencia, string token, bool isProducao);
    
    Task<string> ConsultarMotivoRejeicaoAsync(string referencia, string token, bool isProducao);
}