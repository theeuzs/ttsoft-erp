using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposFocusNfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NfceAmbiente",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NfceReferencia",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NfceStatusFocus",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NfceUrlDanfe",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NfceAmbiente",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "NfceReferencia",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "NfceStatusFocus",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "NfceUrlDanfe",
                table: "Sales");
        }
    }
}
