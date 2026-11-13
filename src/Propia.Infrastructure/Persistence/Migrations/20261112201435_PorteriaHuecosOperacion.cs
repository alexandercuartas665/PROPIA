using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PorteriaHuecosOperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "intervencion_id",
                table: "novedades_turno",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entregado_doc",
                table: "correspondencias",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recordatorios",
                table: "correspondencias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ultimo_recordatorio_at",
                table: "correspondencias",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "intervencion_id",
                table: "novedades_turno");

            migrationBuilder.DropColumn(
                name: "entregado_doc",
                table: "correspondencias");

            migrationBuilder.DropColumn(
                name: "recordatorios",
                table: "correspondencias");

            migrationBuilder.DropColumn(
                name: "ultimo_recordatorio_at",
                table: "correspondencias");
        }
    }
}
