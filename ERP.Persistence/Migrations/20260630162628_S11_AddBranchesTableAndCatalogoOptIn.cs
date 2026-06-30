using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S11_AddBranchesTableAndCatalogoOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CatalogoMostrarEstoque",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CatalogoMostrarPreco",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CatalogoPublicoHabilitado",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatalogoMostrarEstoque",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CatalogoMostrarPreco",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CatalogoPublicoHabilitado",
                table: "Branches");
        }
    }
}
