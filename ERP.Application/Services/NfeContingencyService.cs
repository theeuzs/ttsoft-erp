using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeContingencyService : INfeContingencyService
{
    private readonly IFocusNfeHttpClient _httpClient;
    private readonly IUnitOfWork _uow;

    // Recebemos o UnitOfWork por injeção de dependência, igualzinho ao SaleService!
    public NfeContingencyService(IFocusNfeHttpClient httpClient, IUnitOfWork uow)
    {
        _httpClient = httpClient;
        _uow = uow;
    }

    public async Task<bool> VerificarConexaoSefazAsync()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 2000); 
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        }
        catch
        {
            return false; 
        }
    }

    public async Task RegistrarNotaPendenteAsync(Guid vendaId, string tipoNota, string payloadJson)
    {
        var notaPendente = new NfePendente
        {
            Id = Guid.NewGuid(),
            VendaId = vendaId,
            TipoNota = tipoNota,
            PayloadJson = payloadJson,
            Referencia = vendaId.ToString(),
            DataFalha = DateTime.Now,
            Tentativas = 0
        };

        await _uow.NfePendentes.AddAsync(notaPendente);
        await _uow.CommitAsync(); // Salva no banco de forma segura pelo EF Core
    }

    public async Task<IEnumerable<NfePendente>> ObterNotasPendentesAsync()
    {
        // Puxa as notas pendentes e ordena da mais antiga para a mais nova
        var notas = await _uow.NfePendentes.GetAllAsync();
        return notas.OrderBy(n => n.DataFalha);
    }

    public async Task RemoverNotaPendenteAsync(Guid id)
    {
        var nota = await _uow.NfePendentes.GetByIdAsync(id);
        if (nota != null)
        {
            _uow.NfePendentes.Remove(nota);
            await _uow.CommitAsync();
        }
    }

    public async Task RegistrarFalhaTentativaAsync(Guid id, string erro)
    {
        var nota = await _uow.NfePendentes.GetByIdAsync(id);
        if (nota != null)
        {
            nota.Tentativas += 1;
            nota.UltimaMensagemErro = erro;
            
            _uow.NfePendentes.Update(nota);
            await _uow.CommitAsync();
        }
    }
}