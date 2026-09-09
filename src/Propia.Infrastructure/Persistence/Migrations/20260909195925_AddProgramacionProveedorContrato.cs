using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramacionProveedorContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "contrato_id",
                table: "programacion_tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proveedor_id",
                table: "programacion_tareas",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contrato_id",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "proveedor_id",
                table: "programacion_tareas");
        }
    }
}
