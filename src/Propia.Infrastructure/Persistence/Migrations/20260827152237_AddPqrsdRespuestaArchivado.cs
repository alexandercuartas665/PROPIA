using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdRespuestaArchivado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "archivada",
                table: "pqrsd_respuestas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archivada_at",
                table: "pqrsd_respuestas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archivada_por_usuario_id",
                table: "pqrsd_respuestas",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archivada",
                table: "pqrsd_respuestas");

            migrationBuilder.DropColumn(
                name: "archivada_at",
                table: "pqrsd_respuestas");

            migrationBuilder.DropColumn(
                name: "archivada_por_usuario_id",
                table: "pqrsd_respuestas");
        }
    }
}
