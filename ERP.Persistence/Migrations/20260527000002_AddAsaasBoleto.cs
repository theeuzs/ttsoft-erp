using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace ERP.Persistence.Migrations
{
    public partial class AddAsaasBoleto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsaasPaymentId", table: "ContasReceber",
                type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoletoUrl", table: "ContasReceber",
                type: "nvarchar(500)", maxLength: 500, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoletoBarCode", table: "ContasReceber",
                type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceUrl", table: "ContasReceber",
                type: "nvarchar(500)", maxLength: 500, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsaasStatus", table: "ContasReceber",
                type: "nvarchar(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AsaasPaymentId", table: "ContasReceber");
            migrationBuilder.DropColumn(name: "BoletoUrl",      table: "ContasReceber");
            migrationBuilder.DropColumn(name: "BoletoBarCode",  table: "ContasReceber");
            migrationBuilder.DropColumn(name: "InvoiceUrl",     table: "ContasReceber");
            migrationBuilder.DropColumn(name: "AsaasStatus",    table: "ContasReceber");
        }
    }
}
