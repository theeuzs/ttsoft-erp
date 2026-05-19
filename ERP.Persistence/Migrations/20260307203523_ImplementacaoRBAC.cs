using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImplementacaoRBAC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

          /*  migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")); 
                */

          /*  migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
*/
    //        migrationBuilder.AddColumn<Guid>(
     //           name: "TenantId",
     //           table: "SalePayment",
    //            type: "uniqueidentifier",
    //            nullable: false,
    //            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

    //        migrationBuilder.AddColumn<Guid>(
     //           name: "TenantId",
     //           table: "SaleItems",
     //           type: "uniqueidentifier",
     //           nullable: false,
     //           defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

     //       migrationBuilder.AddColumn<Guid>(
      //          name: "TenantId",
      //          table: "Products",
      //          type: "uniqueidentifier",
      //          nullable: false,
       //         defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

       //     migrationBuilder.AddColumn<Guid>(
       //         name: "TenantId",
       //         table: "Orcamentos",
       //         type: "uniqueidentifier",
       //         nullable: false,
        //        defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

       //     migrationBuilder.AddColumn<Guid>(
        //        name: "UsuarioId",
        //        table: "Orcamentos",
        //        type: "uniqueidentifier",
        //        nullable: false,
        //        defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

       //     migrationBuilder.AddColumn<Guid>(
       //         name: "TenantId",
        //        table: "Customers",
        //        type: "uniqueidentifier",
       //         nullable: false,
        //        defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        //    migrationBuilder.AddColumn<Guid>(
       //         name: "TenantId",
        //        table: "Categories",
       //         type: "uniqueidentifier",
       //         nullable: false,
       //         defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Caixas",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Caixas",
                type: "bit",
                nullable: false,
                defaultValue: false);

      //      migrationBuilder.AddColumn<Guid>(
      //          name: "TenantId",
      //          table: "Caixas",
       //         type: "uniqueidentifier",
      //          nullable: false,
      //          defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Caixas",
                type: "datetime2",
                nullable: true);

         //   migrationBuilder.CreateTable(
          //      name: "NfePendentes",
           //     columns: table => new
          //      {
          //          Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
          //          VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
          //          TipoNota = table.Column<string>(type: "nvarchar(max)", nullable: false),
           //         PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
           //         Referencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
           //         DataFalha = table.Column<DateTime>(type: "datetime2", nullable: false),
          //          Tentativas = table.Column<int>(type: "int", nullable: false),
          //          UltimaMensagemErro = table.Column<string>(type: "nvarchar(max)", nullable: true)
          //      },
         //       constraints: table =>
         //       {
         //           table.PrimaryKey("PK_NfePendentes", x => x.Id);
         //       });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxDiscountPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxSangriaValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionRole",
                columns: table => new
                {
                    PermissionsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RolesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionRole", x => new { x.PermissionsId, x.RolesId });
                    table.ForeignKey(
                        name: "FK_PermissionRole_Permissions_PermissionsId",
                        column: x => x.PermissionsId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissionRole_Roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionRole_RolesId",
                table: "PermissionRole",
                column: "RolesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "NfePendentes");

            migrationBuilder.DropTable(
                name: "PermissionRole");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SalePayment");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Caixas");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Caixas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Caixas");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Caixas");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
