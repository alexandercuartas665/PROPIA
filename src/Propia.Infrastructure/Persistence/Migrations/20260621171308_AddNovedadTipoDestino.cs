using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNovedadTipoDestino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "destino_id",
                table: "novedades_turno",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "destino_tipo",
                table: "novedades_turno",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "novedades_turno",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "destino_id",
                table: "novedades_turno");

            migrationBuilder.DropColumn(
                name: "destino_tipo",
                table: "novedades_turno");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "novedades_turno");
        }
    }
}
