using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentidadRegistralLabelsTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_constitucion",
                table: "tenants",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label_agrupacion",
                table: "tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label_piso",
                table: "tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "licencia_construccion",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matricula_inmobiliaria",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notaria_registro",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_reglamento_ph",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fecha_constitucion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "label_agrupacion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "label_piso",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "licencia_construccion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "matricula_inmobiliaria",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "notaria_registro",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "numero_reglamento_ph",
                table: "tenants");
        }
    }
}
