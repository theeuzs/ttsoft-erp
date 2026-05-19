using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
{
    // SaleId1 nunca foi aplicado ao banco — nada a dropar
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
{
}
    }
}
