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
    private readonly IUnitOfWork _uow;
    private readonly IRequestTenant _tenant;

    // Etapa 2 (Fiscal) — IFocusNfeHttpClient removido do construtor: era
    // injetado, mas nenhum dos 5 métodos desta classe nunca o usava
    // (confirmado em duas auditorias — Etapa 1C e Etapa 2). O registro do
    // IFocusNfeHttpClient em si continua existindo, pra outros consumidores
    // (INfceEmissionService/INfeEmissionService, ainda usados localmente por
    // NotaFiscalAvulsaService/NfseEmissionService).
    public NfeContingencyService(IUnitOfWork uow, IRequestTenant tenant)
    {
        _uow = uow;
        _tenant = tenant;
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

    public async Task RegistrarNotaPendenteAsync(Guid vendaId, string tipoNota, string payloadJson, bool emProducao)
    {
        var notaPendente = new NfePendente
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            VendaId = vendaId,
            TipoNota = tipoNota,
            PayloadJson = payloadJson,
            Referencia = vendaId.ToString(),
            DataFalha = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
            Tentativas = 0,
            CriadaEmProducao = emProducao
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