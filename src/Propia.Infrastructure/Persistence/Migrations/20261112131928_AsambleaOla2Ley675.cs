using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsambleaOla2Ley675 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "instalada_en",
                table: "sesiones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "justificacion_quorum",
                table: "sesiones",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_cancelacion",
                table: "sesiones",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "presidente_nombre",
                table: "sesiones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "presidente_persona_id",
                table: "sesiones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "punto_actual_id",
                table: "sesiones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quorum_instalacion_pct",
                table: "sesiones",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sala_propia",
                table: "sesiones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "secretario_nombre",
                table: "sesiones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "secretario_persona_id",
                table: "sesiones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "sesion_documentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "instalada_en",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "justificacion_quorum",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "motivo_cancelacion",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "presidente_nombre",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "presidente_persona_id",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "punto_actual_id",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "quorum_instalacion_pct",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "sala_propia",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "secretario_nombre",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "secretario_persona_id",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "sesion_documentos");
        }
    }
}
