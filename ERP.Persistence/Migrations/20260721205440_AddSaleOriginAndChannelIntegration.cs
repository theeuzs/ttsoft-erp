using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleOriginAndChannelIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClienteRepasseId",
                table: "SalesChannels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioIntegracaoId",
                table: "SalesChannels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origem",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannels_ClienteRepasseId",
                table: "SalesChannels",
                column: "ClienteRepasseId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesChannels_Customers_ClienteRepasseId",
                table: "SalesChannels",
                column: "ClienteRepasseId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesChannels_Customers_ClienteRepasseId",
                table: "SalesChannels");

            migrationBuilder.DropIndex(
                name: "IX_SalesChannels_ClienteRepasseId",
                table: "SalesChannels");

            migrationBuilder.DropColumn(
                name: "ClienteRepasseId",
                table: "SalesChannels");

            migrationBuilder.DropColumn(
                name: "UsuarioIntegracaoId",
                table: "SalesChannels");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Sales");
        }
    }
}
