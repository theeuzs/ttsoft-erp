using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

// 1. A Interface
public interface INfeContingencyService
{
    Task<bool> VerificarConexaoSefazAsync();
    /// <param name="emProducao">
    /// Ambiente da Focus em que a emissao FALHOU (a configuracao lida pelo chamador). Parametro OBRIGATORIO, sem valor padrao e nunca
    /// deduzido aqui: uma pendencia so e reenviada no ambiente em que nasceu (trava de ambiente).
    /// </param>
    Task RegistrarNotaPendenteAsync(Guid vendaId, string tipoNota, string payloadJson, bool emProducao);
    
    // 👇 Os novos poderes do Robô 👇
    Task<IEnumerable<NfePendente>> ObterNotasPendentesAsync();
    Task RemoverNotaPendenteAsync(Guid id);
    Task RegistrarFalhaTentativaAsync(Guid id, string erro);
}