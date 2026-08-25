using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdShareTokenYAdjuntoCompartido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "share_token",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "compartido",
                table: "pqrsd_adjuntos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "share_token",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "compartido",
                table: "pqrsd_adjuntos");
        }
    }
}
