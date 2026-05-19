using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrcamentoFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataFollowUp",
                table: "Orcamentos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataUltimoContato",
                table: "Orcamentos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoPerda",
                table: "Orcamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacaoFollowUp",
                table: "Orcamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusFollowUp",
                table: "Orcamentos",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataFollowUp",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "DataUltimoContato",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "MotivoPerda",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ObservacaoFollowUp",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "StatusFollowUp",
                table: "Orcamentos");
        }
    }
}
