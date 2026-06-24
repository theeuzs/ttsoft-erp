using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexCaixaAberta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // S9: unique partial index — garante que cada usuário tem no máximo 1 caixa aberto por tenant.
            // Substitui o check-then-act em CaixaService.AbrirCaixaAsync, que tinha janela de TOCTOU.
            // Status = 1 corresponde a StatusCaixa.Aberto.
            // Filtragem por Status exclui caixas fechados — o mesmo usuário pode abrir/fechar/reabrir.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX IX_Caixa_UsuarioTenantAberto
                  ON Caixas (UsuarioId, TenantId)
                  WHERE Status = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Caixa_UsuarioTenantAberto ON Caixas;");
        }
    }
}