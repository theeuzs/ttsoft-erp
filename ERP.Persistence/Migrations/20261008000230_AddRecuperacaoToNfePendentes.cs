using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecuperacaoToNfePendentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "NfePendentes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Ativa");

            migrationBuilder.AddColumn<int>(
                name: "FalhasDesconhecidasSeguidas",
                table: "NfePendentes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FalhasTransitoriasSeguidas",
                table: "NfePendentes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximaTentativaEm",
                table: "NfePendentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TentativasConsulta",
                table: "NfePendentes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TentativasPost",
                table: "NfePendentes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaConsultaEm",
                table: "NfePendentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimaDecisao",
                table: "NfePendentes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoPostEm",
                table: "NfePendentes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "FalhasDesconhecidasSeguidas",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "FalhasTransitoriasSeguidas",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "ProximaTentativaEm",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "TentativasConsulta",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "TentativasPost",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "UltimaConsultaEm",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "UltimaDecisao",
                table: "NfePendentes");

            migrationBuilder.DropColumn(
                name: "UltimoPostEm",
                table: "NfePendentes");
        }
    }
}
