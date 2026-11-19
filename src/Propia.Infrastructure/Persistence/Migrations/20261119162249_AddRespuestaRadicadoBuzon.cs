using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRespuestaRadicadoBuzon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "acuse_destino",
                table: "correos_recibidos",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acuse_enviado_at",
                table: "correos_recibidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "acuse_error",
                table: "correos_recibidos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plantilla_acuse_id",
                table: "buzones_correo",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "responder_radicado",
                table: "buzones_correo",
                type: "boolean",
                nullable: false,
                // Los buzones que ya existen quedan respondiendo el radicado (ADD COLUMN con DEFAULT: sin UPDATE por fila,
                // no choca con la RLS FORCE de buzones_correo).
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_buzones_correo_plantilla_acuse_id",
                table: "buzones_correo",
                column: "plantilla_acuse_id");

            migrationBuilder.AddForeignKey(
                name: "FK_buzones_correo_plantillas_documento_plantilla_acuse_id",
                table: "buzones_correo",
                column: "plantilla_acuse_id",
                principalTable: "plantillas_documento",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_buzones_correo_plantillas_documento_plantilla_acuse_id",
                table: "buzones_correo");

            migrationBuilder.DropIndex(
                name: "IX_buzones_correo_plantilla_acuse_id",
                table: "buzones_correo");

            migrationBuilder.DropColumn(
                name: "acuse_destino",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "acuse_enviado_at",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "acuse_error",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "plantilla_acuse_id",
                table: "buzones_correo");

            migrationBuilder.DropColumn(
                name: "responder_radicado",
                table: "buzones_correo");
        }
    }
}
