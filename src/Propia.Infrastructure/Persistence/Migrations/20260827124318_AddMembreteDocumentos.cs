using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembreteDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "membrete_color_acento",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "membrete_contacto_footer",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "membrete_firmante_cargo",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "membrete_firmante_nombre",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "membrete_linea_legal",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "membrete_mostrar_logo",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "membrete_mostrar_numeracion",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "membrete_color_acento",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_contacto_footer",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_firmante_cargo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_firmante_nombre",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_linea_legal",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_mostrar_logo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "membrete_mostrar_numeracion",
                table: "tenants");
        }
    }
}
