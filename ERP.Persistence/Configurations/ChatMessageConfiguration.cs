// ── ERP.Persistence/EntityConfigurations/ChatMessageConfiguration.cs ──────────
using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("ChatMessages");

        b.HasKey(m => m.Id);

        b.Property(m => m.TenantId)
         .IsRequired();

        b.Property(m => m.RemetenteNome)
         .IsRequired()
         .HasMaxLength(100);

        b.Property(m => m.Mensagem)
         .IsRequired()
         .HasMaxLength(2000);

        b.Property(m => m.Sala)
         .HasMaxLength(100);

        b.Property(m => m.IsRead)
         .HasDefaultValue(false);

        // Índice composto para busca eficiente de histórico por tenant + sala + data
        b.HasIndex(m => new { m.TenantId, m.Sala, m.CreatedAt })
         .HasDatabaseName("IX_ChatMessages_Tenant_Sala_Data");

        // Soft-delete global filter (padrão do projeto)
        b.HasQueryFilter(m => !m.IsDeleted);
    }
}
