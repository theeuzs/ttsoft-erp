using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

// 1. A Interface
public interface INfeContingencyService
{
    Task<bool> VerificarConexaoSefazAsync();
    Task RegistrarNotaPendenteAsync(Guid vendaId, string tipoNota, string payloadJson);
    
    // 👇 Os novos poderes do Robô 👇
    Task<IEnumerable<NfePendente>> ObterNotasPendentesAsync();
    Task RemoverNotaPendenteAsync(Guid id);
    Task RegistrarFalhaTentativaAsync(Guid id, string erro);
}