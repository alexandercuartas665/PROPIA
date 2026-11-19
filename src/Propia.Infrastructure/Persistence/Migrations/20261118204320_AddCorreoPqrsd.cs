using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorreoPqrsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "pqrsd_automatico",
                table: "correos_recibidos",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pqrsd_expediente_id",
                table: "correos_recibidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pqrsd_radicado",
                table: "correos_recibidos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "propuesta_pqrsd_json",
                table: "correos_recibidos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "umbral_auto_pqrsd",
                table: "buzones_correo",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: true);

            // Buzones existentes: radican PQRSD solos desde 90% de confianza (igual que los nuevos). La tabla tiene
            // FORCE RLS: se suelta solo durante el UPDATE (regla de backfills del proyecto).
            migrationBuilder.Sql("ALTER TABLE buzones_correo NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("UPDATE buzones_correo SET umbral_auto_pqrsd = 0.900 WHERE umbral_auto_pqrsd IS NULL;");
            migrationBuilder.Sql("ALTER TABLE buzones_correo FORCE ROW LEVEL SECURITY;");

            migrationBuilder.CreateIndex(
                name: "IX_correos_recibidos_pqrsd_expediente_id",
                table: "correos_recibidos",
                column: "pqrsd_expediente_id");

            migrationBuilder.AddForeignKey(
                name: "FK_correos_recibidos_pqrsd_expedientes_pqrsd_expediente_id",
                table: "correos_recibidos",
                column: "pqrsd_expediente_id",
                principalTable: "pqrsd_expedientes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_correos_recibidos_pqrsd_expedientes_pqrsd_expediente_id",
                table: "correos_recibidos");

            migrationBuilder.DropIndex(
                name: "IX_correos_recibidos_pqrsd_expediente_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "pqrsd_automatico",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "pqrsd_expediente_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "pqrsd_radicado",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "propuesta_pqrsd_json",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "umbral_auto_pqrsd",
                table: "buzones_correo");
        }
    }
}
