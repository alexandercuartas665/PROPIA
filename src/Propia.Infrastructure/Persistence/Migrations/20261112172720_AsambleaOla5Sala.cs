using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsambleaOla5Sala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grabacion_egress_id",
                table: "sesiones",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "grabacion_iniciada_en",
                table: "sesiones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "salida_pendiente_en",
                table: "sesion_participantes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grabacion_egress_id",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "grabacion_iniciada_en",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "salida_pendiente_en",
                table: "sesion_participantes");
        }
    }
}
