using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendaSuspensa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendasSuspensas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataSuspensao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClienteNome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsuarioIdSuspensor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeSuspensor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAproximado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VendaFinalizadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioIdEmEdicao = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeEmEdicao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataInicioEdicao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendasSuspensas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendaSuspensaItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaSuspensaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NormalUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatorConversao = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnidadeEstoque = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabelUnidadeVenda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WholesaleMinQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaSuspensaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaSuspensaItens_VendasSuspensas_VendaSuspensaId",
                        column: x => x.VendaSuspensaId,
                        principalTable: "VendasSuspensas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendaSuspensaItens_VendaSuspensaId",
                table: "VendaSuspensaItens",
                column: "VendaSuspensaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendaSuspensaItens");

            migrationBuilder.DropTable(
                name: "VendasSuspensas");
        }
    }
}
