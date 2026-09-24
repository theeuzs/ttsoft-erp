using ERP.Application.DTOs.FocusNfe;
using FluentResults;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfceEmissionService
{
    // S{N} FIX — achado testando Fase C (Módulo 5): Focus manda chave_nfe e
    // numero na resposta de autorização, e isso sempre foi descartado —
    // Sale.NfceChave/NfceNumero (colunas que já existiam) ficavam NULL pra
    // toda nota emitida, mesmo autorizada. Chave/Numero adicionados no FIM
    // da tupla de propósito — quem só acessa por nome (result.Sucesso etc.)
    // não quebra, só quem desestrutura (var (a,b,c,d) = ...) precisa dos 2
    // novos nomes.
    Task<(bool Sucesso, string Mensagem, string UrlDanfe, string UrlXml, string Chave, string Numero)> EmitirNfceAsync(string referencia, FocusNfceRequest nfce, string token, bool isProducao);
}