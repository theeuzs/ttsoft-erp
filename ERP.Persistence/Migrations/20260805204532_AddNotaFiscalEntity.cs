using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaFiscalEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasFiscais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serie = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Finalidade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefNFe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UrlDanfe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    XmlUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ambiente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestinatarioNome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestinatarioDocumento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasFiscais_Sales_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_Chave",
                table: "NotasFiscais",
                column: "Chave");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_VendaId",
                table: "NotasFiscais",
                column: "VendaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotasFiscais");
        }
    }
}
