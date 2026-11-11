using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <summary>S{N} FIX: indice unico de NfesRecebidas passa a ser (TenantId, Chave).</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111190000_FixNfesRecebidasChaveIndexPorTenant")]
    public partial class FixNfesRecebidasChaveIndexPorTenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_NfesRecebidas_Chave", table: "NfesRecebidas");
            migrationBuilder.CreateIndex(
                name: "IX_NfesRecebidas_TenantId_Chave",
                table: "NfesRecebidas",
                columns: new[] { "TenantId", "Chave" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_NfesRecebidas_TenantId_Chave", table: "NfesRecebidas");
            migrationBuilder.CreateIndex(
                name: "IX_NfesRecebidas_Chave",
                table: "NfesRecebidas",
                column: "Chave",
                unique: true);
        }
    }
}
