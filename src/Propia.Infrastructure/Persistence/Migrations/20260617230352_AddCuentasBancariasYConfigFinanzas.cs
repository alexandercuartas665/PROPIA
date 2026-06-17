using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCuentasBancariasYConfigFinanzas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chartld",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comunicacion_factura",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_acta_asamblea",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_acta_consejo",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_factura",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_nota_credito",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_paz_y_salvo",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "consecutivo_r_c",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "convenio_recaudo",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cuenta_contable",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estrato_facturacion",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "formas_de_pago",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minimo_saldo_cartera",
                table: "tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "minimo_saldo_pronto_pago",
                table: "tenants",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "multiplo_redondeo",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "multiplo_redondeo_cuota_extra",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "multiplo_redondeo_pronto_pago",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tipos_pago_permitidos",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "wenjoy_codigo_recaudo",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zona_facturacion",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cuentas_bancarias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo_cuenta = table.Column<int>(type: "integer", nullable: false),
                    banco = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ver_en_factura = table.Column<bool>(type: "boolean", nullable: false),
                    cancelada = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_cancelacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas_bancarias", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_bancarias_tenant_id_numero_cuenta",
                table: "cuentas_bancarias",
                columns: new[] { "tenant_id", "numero_cuenta" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cuentas_bancarias");

            migrationBuilder.DropColumn(
                name: "chartld",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "comunicacion_factura",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_acta_asamblea",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_acta_consejo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_factura",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_nota_credito",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_paz_y_salvo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "consecutivo_r_c",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "convenio_recaudo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "cuenta_contable",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "estrato_facturacion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "formas_de_pago",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "minimo_saldo_cartera",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "minimo_saldo_pronto_pago",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "multiplo_redondeo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "multiplo_redondeo_cuota_extra",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "multiplo_redondeo_pronto_pago",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tipos_pago_permitidos",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "wenjoy_codigo_recaudo",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "zona_facturacion",
                table: "tenants");
        }
    }
}
