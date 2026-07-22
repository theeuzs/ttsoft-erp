using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAtivo = table.Column<bool>(type: "bit", nullable: false),
                    Capacidades = table.Column<int>(type: "int", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InternalStatus = table.Column<int>(type: "int", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataPedidoExterno = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrders_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalOrders_Sales_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IniciadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalPedidosProcessados = table.Column<int>(type: "int", nullable: false),
                    TotalConflitos = table.Column<int>(type: "int", nullable: false),
                    TotalErros = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingSessions_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SalesChannelPricingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PercentualAjuste = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AplicarAutomaticamente = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesChannelPricingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesChannelPricingPolicies_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkuMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkuExterno = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BufferSeguranca = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkuMappings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SkuMappings_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkuExterno = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescricaoItem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantidade = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrderItems_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErroCodigo = table.Column<int>(type: "int", nullable: true),
                    ErroMensagem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcluidaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderActions_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resolvido = table.Column<bool>(type: "bit", nullable: false),
                    ResolvidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvidoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoResolucao = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderConflicts_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEvents_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShadowStockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LiberadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShadowStockReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShadowStockReservations_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShadowStockReservations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderItems_ExternalOrderId",
                table: "ExternalOrderItems",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderItems_ProductId",
                table: "ExternalOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_CorrelationId",
                table: "ExternalOrders",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_SalesChannelId_ExternalOrderId",
                table: "ExternalOrders",
                columns: new[] { "SalesChannelId", "ExternalOrderId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_VendaId",
                table: "ExternalOrders",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderActions_CorrelationId",
                table: "OrderActions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderActions_ExternalOrderId",
                table: "OrderActions",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderConflicts_CorrelationId",
                table: "OrderConflicts",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderConflicts_ExternalOrderId",
                table: "OrderConflicts",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_CorrelationId",
                table: "OrderEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_ExternalOrderId",
                table: "OrderEvents",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingSessions_SalesChannelId_IniciadoEm",
                table: "ProcessingSessions",
                columns: new[] { "SalesChannelId", "IniciadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesChannelPricingPolicies_SalesChannelId",
                table: "SalesChannelPricingPolicies",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ShadowStockReservations_ExternalOrderId",
                table: "ShadowStockReservations",
                column: "ExternalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShadowStockReservations_ProductId_Status",
                table: "ShadowStockReservations",
                columns: new[] { "ProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ProductId",
                table: "SkuMappings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_SalesChannelId_SkuExterno",
                table: "SkuMappings",
                columns: new[] { "SalesChannelId", "SkuExterno" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalOrderItems");

            migrationBuilder.DropTable(
                name: "OrderActions");

            migrationBuilder.DropTable(
                name: "OrderConflicts");

            migrationBuilder.DropTable(
                name: "OrderEvents");

            migrationBuilder.DropTable(
                name: "ProcessingSessions");

            migrationBuilder.DropTable(
                name: "SalesChannelPricingPolicies");

            migrationBuilder.DropTable(
                name: "ShadowStockReservations");

            migrationBuilder.DropTable(
                name: "SkuMappings");

            migrationBuilder.DropTable(
                name: "ExternalOrders");

            migrationBuilder.DropTable(
                name: "SalesChannels");
        }
    }
}
