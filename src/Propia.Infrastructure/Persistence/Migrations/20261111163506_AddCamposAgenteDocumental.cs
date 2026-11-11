using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Datos que el Agente Documental extrae y antes no tenian donde guardarse (decision de Alex, 2026-11-11).
    /// ADITIVA y nullable: polizas (nit_aseguradora, tomador, asegurado, valor_asegurado, prima, deducible) y
    /// contratos_servicio (representante_legal, ciudad). "valor_poliza" se conserva. Sin datos que migrar;
    /// las tablas ya tienen RLS. Down: borra las 8 columnas.
    /// </summary>
    public partial class AddCamposAgenteDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "asegurado",
                table: "polizas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deducible",
                table: "polizas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nit_aseguradora",
                table: "polizas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "prima",
                table: "polizas",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tomador",
                table: "polizas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_asegurado",
                table: "polizas",
                type: "numeric(16,2)",
                precision: 16,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ciudad",
                table: "contratos_servicio",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "representante_legal",
                table: "contratos_servicio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "asegurado",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "deducible",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "nit_aseguradora",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "prima",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "tomador",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "valor_asegurado",
                table: "polizas");

            migrationBuilder.DropColumn(
                name: "ciudad",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "representante_legal",
                table: "contratos_servicio");
        }
    }
}
