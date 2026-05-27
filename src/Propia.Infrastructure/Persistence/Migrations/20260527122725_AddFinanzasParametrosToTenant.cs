using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanzasParametrosToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dia_corte",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "finanzas_configuradas",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "tenants",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "COP");

            migrationBuilder.AddColumn<int>(
                name: "periodo_gracia_dias",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "tasa_mora_es_legal",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tasa_mora_valor",
                table: "tenants",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dia_corte",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "finanzas_configuradas",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "moneda",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "periodo_gracia_dias",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tasa_mora_es_legal",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tasa_mora_valor",
                table: "tenants");
        }
    }
}
