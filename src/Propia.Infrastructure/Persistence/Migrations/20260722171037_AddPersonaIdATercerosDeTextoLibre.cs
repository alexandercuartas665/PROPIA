using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaIdATercerosDeTextoLibre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "persona_id",
                table: "unidad_titularidades",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "persona_id",
                table: "unidad_empleadas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contacto_persona_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_empresa_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_persona_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "persona_id",
                table: "unidad_titularidades");

            migrationBuilder.DropColumn(
                name: "persona_id",
                table: "unidad_empleadas");

            migrationBuilder.DropColumn(
                name: "contacto_persona_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "proveedor_empresa_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "proveedor_persona_id",
                table: "contratos_servicio");
        }
    }
}
