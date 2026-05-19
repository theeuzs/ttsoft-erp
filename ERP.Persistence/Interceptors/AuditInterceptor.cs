using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ERP.Domain.Entities;

namespace ERP.Persistence.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        BeforeSave(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        BeforeSave(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void BeforeSave(DbContext context)
    {
        if (context == null) return;

        context.ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditLog>();

        // Varre tudo que está sendo salvo no banco neste exato milissegundo
        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Ignora se não mudou nada, ou se for a própria tabela de Auditoria (evita loop infinito)
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            // Prepara a ficha criminal
            var auditEntry = new AuditLog
            {
                // 👇 Melhoria 1: Grava apenas "Product" em vez do caminho inteiro gigante
                EntityType = entry.Metadata.ClrType.Name, 
                Timestamp = DateTime.Now,
                MachineName = Environment.MachineName,
                UserId = ERP.Domain.CurrentUser.Id,
                UserName = string.IsNullOrWhiteSpace(ERP.Domain.CurrentUser.Name) ? "SISTEMA" : ERP.Domain.CurrentUser.Name
            };

            // 👇 Melhoria 2: A Mágica da Identificação Humana 👇
            var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            string idBanco = primaryKey?.CurrentValue?.ToString() ?? "N/A";

            // Procura se a tabela tem algum campo legível (Nome, Descrição, etc)
            var propNome = entry.Properties.FirstOrDefault(p => 
                p.Metadata.Name == "Name" || 
                p.Metadata.Name == "Nome" || 
                p.Metadata.Name == "Descricao" || 
                p.Metadata.Name == "RazaoSocial" ||
                p.Metadata.Name == "SKU");

            string nomeHumano = propNome?.CurrentValue?.ToString();

            // Junta o Nome com um pedaço do ID para ficar perfeito na tela!
            if (!string.IsNullOrEmpty(nomeHumano))
            {
                string idCurto = idBanco.Length > 8 ? idBanco.Substring(0, 8) : idBanco;
                auditEntry.EntityId = $"{nomeHumano} \n(ID: {idCurto}...)";
            }
            else
            {
                auditEntry.EntityId = idBanco;
            }

            var oldValues = new Dictionary<string, object>();
            var newValues = new Dictionary<string, object>();

            // Vasculha cada campo (Nome, Preço, Estoque) para ver o que mudou
            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.Action = "Create";
                        newValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.Action = "Delete";
                        oldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            var original = property.OriginalValue;
                            var current = property.CurrentValue;

                            // 👇 A MÁGICA: Só anota na fofoca se o valor REALMENTE for diferente!
                            if (!Equals(original, current))
                            {
                                auditEntry.Action = "Update";
                                oldValues[propertyName] = original;
                                newValues[propertyName] = current;
                            }
                        }
                        break;
                }
            }

            // 👇 SE NÃO MUDOU NADA REAL (só salvou sem mexer), IGNORA E NÃO CRIA LOG!
            if (entry.State == EntityState.Modified && oldValues.Count == 0)
                continue;

            // 👇 A MÁGICA 2: WriteIndented = true deixa o JSON bonitão e pulando linhas!
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            
            auditEntry.OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, jsonOptions);
            auditEntry.NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, jsonOptions);

            auditEntries.Add(auditEntry);
        }

        // Adiciona as fofocas no banco antes dele salvar de fato
        if (auditEntries.Any())
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
    }
}