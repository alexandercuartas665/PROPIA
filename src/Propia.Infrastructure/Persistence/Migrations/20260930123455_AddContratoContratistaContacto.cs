using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoContratistaContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correo_contratista",
                table: "contratos_servicio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "direccion_contratista",
                table: "contratos_servicio",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefono_contratista",
                table: "contratos_servicio",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correo_contratista",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "direccion_contratista",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "telefono_contratista",
                table: "contratos_servicio");
        }
    }
}
