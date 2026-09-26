using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDescripcionCamposDefinicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "zona_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "vehiculo_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "usuario_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "unidad_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "tercero_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "persona_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "mascota_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "equipo_campos_definiciones",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "zona_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "vehiculo_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "usuario_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "unidad_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "tercero_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "persona_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "mascota_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "equipo_campos_definiciones");
        }
    }
}
