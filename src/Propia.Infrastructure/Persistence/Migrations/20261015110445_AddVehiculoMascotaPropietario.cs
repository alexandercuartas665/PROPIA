using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiculoMascotaPropietario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "propietario_persona_id",
                table: "unidad_placas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "propietario_persona_id",
                table: "unidad_mascotas",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "propietario_persona_id",
                table: "unidad_placas");

            migrationBuilder.DropColumn(
                name: "propietario_persona_id",
                table: "unidad_mascotas");
        }
    }
}
