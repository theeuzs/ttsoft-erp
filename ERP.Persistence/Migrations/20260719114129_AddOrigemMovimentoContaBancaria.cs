using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrigemMovimentoContaBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrigemId",
                table: "MovimentosContaBancaria",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigemTipo",
                table: "MovimentosContaBancaria",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigemId",
                table: "MovimentosContaBancaria");

            migrationBuilder.DropColumn(
                name: "OrigemTipo",
                table: "MovimentosContaBancaria");
        }
    }
}
