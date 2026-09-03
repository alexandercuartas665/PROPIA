using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdDatosRecepcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "administrador",
                table: "pqrsd_expedientes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_recibido",
                table: "pqrsd_expedientes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "medio_recepcion",
                table: "pqrsd_expedientes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seccional",
                table: "pqrsd_expedientes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "administrador",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "fecha_recibido",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "medio_recepcion",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "seccional",
                table: "pqrsd_expedientes");
        }
    }
}
