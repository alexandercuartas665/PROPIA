using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoCamposPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "asociado_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "asociado_tipo",
                table: "contratos_servicio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "categoria",
                table: "contratos_servicio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "forma_pago_cuotas",
                table: "contratos_servicio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_contrato",
                table: "contratos_servicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pago_mensual",
                table: "contratos_servicio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "tipo_contrato",
                table: "contratos_servicio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_total",
                table: "contratos_servicio",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "asociado_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "asociado_tipo",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "forma_pago_cuotas",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "numero_contrato",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "pago_mensual",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "tipo_contrato",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "valor_total",
                table: "contratos_servicio");
        }
    }
}
