using ERP.Application.Fiscal.Recovery;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Etapa 4A-4, passo 2: as colunas novas de NfePendente. SQLite real
/// (EnsureCreated monta o esquema a partir do modelo): prova que as 9 colunas
/// estao mapeadas e que uma pendencia criada como o codigo de hoje cria
/// (sem tocar nelas) volta Ativa e zerada.
///
/// NAO prova o DEFAULT do SQL Server nem o limite de tamanho: isso e da
/// migration, conferido por SQL depois de aplicada.
/// </summary>
public class NfePendenteRecuperacaoTests
{
    private static AppDbContext CriarContexto(Guid tenantId)
    {
        var connection = new SqliteConnection($"DataSource=pendente_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        connection.Open();

        AppDbContext.SetGlobalTenantId(tenantId);
        AppDbContext.SetQueryTenantId(tenantId);
        var tenant = new ERP.Tests.FakeRequestTenant { TenantId = tenantId };

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options, tenant);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact(DisplayName = "NfePendente nova nasce Ativa, com contadores zerados e datas tecnicas nulas")]
    public void NovaPendencia_Padroes()
    {
        var n = new NfePendente();

        n.Estado.Should().Be(NfePendenteEstados.Ativa,
            "o Domain nao referencia Application, entao o literal \"Ativa\" precisa bater com a constante");
        n.ProximaTentativaEm.Should().BeNull();
        n.UltimaDecisao.Should().BeNull();
        n.UltimaConsultaEm.Should().BeNull();
        n.UltimoPostEm.Should().BeNull();
        n.TentativasPost.Should().Be(0);
        n.TentativasConsulta.Should().Be(0);
        n.FalhasTransitoriasSeguidas.Should().Be(0);
        n.FalhasDesconhecidasSeguidas.Should().Be(0);
    }

    [Fact(DisplayName = "Modelo do EF mapeia as 9 colunas novas, com a nulabilidade certa")]
    public void Modelo_MapeiaColunasNovas()
    {
        using var ctx = CriarContexto(Guid.NewGuid());
        var entidade = ctx.Model.FindEntityType(typeof(NfePendente));
        entidade.Should().NotBeNull();

        (string Nome, bool PodeSerNula)[] esperadas =
        {
            (nameof(NfePendente.Estado), false),
            (nameof(NfePendente.ProximaTentativaEm), true),
            (nameof(NfePendente.UltimaDecisao), true),
            (nameof(NfePendente.UltimaConsultaEm), true),
            (nameof(NfePendente.UltimoPostEm), true),
            (nameof(NfePendente.TentativasPost), false),
            (nameof(NfePendente.TentativasConsulta), false),
            (nameof(NfePendente.FalhasTransitoriasSeguidas), false),
            (nameof(NfePendente.FalhasDesconhecidasSeguidas), false)
        };

        foreach (var (nome, podeSerNula) in esperadas)
        {
            var propriedade = entidade!.FindProperty(nome);
            propriedade.Should().NotBeNull($"coluna {nome}");
            propriedade!.IsNullable.Should().Be(podeSerNula, $"coluna {nome}");
        }
    }

    [Fact(DisplayName = "Pendencia criada como o codigo de hoje cria (sem tocar nas colunas novas) volta Ativa e zerada")]
    public void IdaEVolta_ComoOCodigoDeHojeCria()
    {
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();
        using var ctx = CriarContexto(tenantId);

        // Mesmos campos que NfeContingencyService.RegistrarNotaPendenteAsync preenche.
        ctx.NfePendentes.Add(new NfePendente
        {
            Id = id,
            TenantId = tenantId,
            VendaId = Guid.NewGuid(),
            TipoNota = "NFCE",
            PayloadJson = "{}",
            Referencia = "ref-de-hoje",
            DataFalha = new DateTime(2026, 10, 8, 9, 0, 0),
            Tentativas = 0
        });
        ctx.SaveChanges();

        var lida = ctx.NfePendentes.AsNoTracking().Single(n => n.Id == id);

        lida.Estado.Should().Be(NfePendenteEstados.Ativa);
        lida.ProximaTentativaEm.Should().BeNull();
        lida.UltimaDecisao.Should().BeNull();
        lida.TentativasPost.Should().Be(0);
        lida.TentativasConsulta.Should().Be(0);
        lida.FalhasTransitoriasSeguidas.Should().Be(0);
        lida.FalhasDesconhecidasSeguidas.Should().Be(0);
        lida.Tentativas.Should().Be(0, "a coluna legada continua intacta");
    }

    [Fact(DisplayName = "Todas as colunas novas preenchidas fazem ida e volta no banco sem perder nada")]
    public void IdaEVolta_ComTodasAsColunasNovas()
    {
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var proxima = new DateTime(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);
        var consulta = new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);
        var post = new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc);
        using var ctx = CriarContexto(tenantId);

        ctx.NfePendentes.Add(new NfePendente
        {
            Id = id,
            TenantId = tenantId,
            VendaId = Guid.NewGuid(),
            TipoNota = "NFCE",
            PayloadJson = "{}",
            Referencia = "ref-completa",
            DataFalha = new DateTime(2026, 10, 8, 9, 0, 0),
            Tentativas = 2,
            Estado = NfePendenteEstados.AguardandoCorrecao,
            ProximaTentativaEm = proxima,
            UltimaDecisao = "ErroDeConfiguracao: credencial/configuracao; consulta espacada ate corrigir.",
            UltimaConsultaEm = consulta,
            UltimoPostEm = post,
            TentativasPost = 3,
            TentativasConsulta = 7,
            FalhasTransitoriasSeguidas = 4,
            FalhasDesconhecidasSeguidas = 1
        });
        ctx.SaveChanges();

        var lida = ctx.NfePendentes.AsNoTracking().Single(n => n.Id == id);

        lida.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        lida.ProximaTentativaEm.Should().Be(proxima);
        lida.UltimaDecisao.Should().StartWith("ErroDeConfiguracao");
        lida.UltimaConsultaEm.Should().Be(consulta);
        lida.UltimoPostEm.Should().Be(post);
        lida.TentativasPost.Should().Be(3);
        lida.TentativasConsulta.Should().Be(7);
        lida.FalhasTransitoriasSeguidas.Should().Be(4);
        lida.FalhasDesconhecidasSeguidas.Should().Be(1);
        lida.Tentativas.Should().Be(2);
    }
}
