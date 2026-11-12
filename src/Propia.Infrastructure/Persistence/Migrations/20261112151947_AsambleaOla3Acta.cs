using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsambleaOla3Acta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "borrador_html",
                table: "actas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "comunicado_id",
                table: "actas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "editada",
                table: "actas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "error_redaccion",
                table: "actas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estado_redaccion",
                table: "actas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "firma_presidente_en",
                table: "actas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "firma_presidente_ip",
                table: "actas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "firma_presidente_metodo",
                table: "actas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "firma_presidente_usuario_id",
                table: "actas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "firma_secretario_en",
                table: "actas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "firma_secretario_ip",
                table: "actas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "firma_secretario_metodo",
                table: "actas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "firma_secretario_usuario_id",
                table: "actas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero",
                table: "actas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "redactada_en",
                table: "actas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "redactada_por_ia",
                table: "actas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_actas_tenant_id_numero",
                table: "actas",
                columns: new[] { "tenant_id", "numero" },
                unique: true,
                filter: "numero IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_actas_tenant_id_numero",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "borrador_html",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "comunicado_id",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "editada",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "error_redaccion",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "estado_redaccion",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_presidente_en",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_presidente_ip",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_presidente_metodo",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_presidente_usuario_id",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_secretario_en",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_secretario_ip",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_secretario_metodo",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "firma_secretario_usuario_id",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "numero",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "redactada_en",
                table: "actas");

            migrationBuilder.DropColumn(
                name: "redactada_por_ia",
                table: "actas");
        }
    }
}
