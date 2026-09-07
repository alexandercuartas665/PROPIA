using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PqrsdDestinatarioCanalesFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "canal",
                table: "pqrsd_respuesta_destinatarios");

            migrationBuilder.AddColumn<bool>(
                name: "enviar_correo",
                table: "pqrsd_respuesta_destinatarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "enviar_whats_app",
                table: "pqrsd_respuesta_destinatarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enviar_correo",
                table: "pqrsd_respuesta_destinatarios");

            migrationBuilder.DropColumn(
                name: "enviar_whats_app",
                table: "pqrsd_respuesta_destinatarios");

            migrationBuilder.AddColumn<int>(
                name: "canal",
                table: "pqrsd_respuesta_destinatarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
