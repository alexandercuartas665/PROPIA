using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturaProveedorDirectorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_empresa_id",
                table: "zona_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_persona_id",
                table: "zona_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_empresa_id",
                table: "equipo_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_persona_id",
                table: "equipo_facturas",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proveedor_empresa_id",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "proveedor_persona_id",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "proveedor_empresa_id",
                table: "equipo_facturas");

            migrationBuilder.DropColumn(
                name: "proveedor_persona_id",
                table: "equipo_facturas");
        }
    }
}
