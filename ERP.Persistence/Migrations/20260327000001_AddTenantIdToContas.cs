using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToContas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ContasReceber ──────────────────────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ContasReceber",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_TenantId",
                table: "ContasReceber",
                column: "TenantId");

            // ── ContasPagar ────────────────────────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ContasPagar",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_TenantId",
                table: "ContasPagar",
                column: "TenantId");

            // ── Orcamentos ─────────────────────────────────────────────────
            // Adiciona índice para performance (coluna já existe via migration anterior)
            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_TenantId",
                table: "Orcamentos",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_ContasReceber_TenantId", "ContasReceber");
            migrationBuilder.DropColumn("TenantId", "ContasReceber");

            migrationBuilder.DropIndex("IX_ContasPagar_TenantId", "ContasPagar");
            migrationBuilder.DropColumn("TenantId", "ContasPagar");

            migrationBuilder.DropIndex("IX_Orcamentos_TenantId", "Orcamentos");
        }
    }
}
