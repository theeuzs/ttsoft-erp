using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefinarRecebivelOperadora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FormaPagamento",
                table: "RecebiveisOperadora",
                newName: "FormaRecebimento");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "RecebiveisOperadora",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Nsu",
                table: "RecebiveisOperadora",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nsu",
                table: "RecebiveisOperadora");

            migrationBuilder.RenameColumn(
                name: "FormaRecebimento",
                table: "RecebiveisOperadora",
                newName: "FormaPagamento");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RecebiveisOperadora",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
