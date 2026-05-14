using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMiCopropiedad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ciudad",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "departamento",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "tenants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "digito_verificacion",
                table: "tenants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estrato",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "foto_fachada_url",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo_copropiedad",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contratos_servicio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nit_proveedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    valor_mensual = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contratos_servicio", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipos_activos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    categoria = table.Column<int>(type: "integer", nullable: false),
                    marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    numero_serie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecha_instalacion = table.Column<DateOnly>(type: "date", nullable: true),
                    garantia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    ubicacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipos_activos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "miembros_consejo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_miembros_consejo", x => x.id);
                    table.ForeignKey(
                        name: "FK_miembros_consejo_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "torres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cantidad_pisos = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_torres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zonas_comunes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    categoria = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    es_reservable = table.Column<bool>(type: "boolean", nullable: false),
                    tarifa_reserva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    capacidad_personas = table.Column<int>(type: "integer", nullable: true),
                    horarios_uso = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reglas_uso = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zonas_comunes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidades_privadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    torre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    piso = table.Column<int>(type: "integer", nullable: true),
                    coeficiente_propiedad = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    habitaciones = table.Column<int>(type: "integer", nullable: true),
                    banos = table.Column<int>(type: "integer", nullable: true),
                    parqueaderos = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidades_privadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidades_privadas_torres_torre_id",
                        column: x => x.torre_id,
                        principalTable: "torres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contratos_servicio_fecha_fin",
                table: "contratos_servicio",
                column: "fecha_fin");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_servicio_tenant_id",
                table: "contratos_servicio",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipos_activos_tenant_id",
                table: "equipos_activos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_miembros_consejo_persona_id",
                table: "miembros_consejo",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_miembros_consejo_tenant_id",
                table: "miembros_consejo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_miembros_consejo_tenant_id_cargo",
                table: "miembros_consejo",
                columns: new[] { "tenant_id", "cargo" });

            migrationBuilder.CreateIndex(
                name: "IX_torres_tenant_id",
                table: "torres",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_privadas_tenant_id",
                table: "unidades_privadas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_privadas_tenant_id_numero",
                table: "unidades_privadas",
                columns: new[] { "tenant_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidades_privadas_torre_id",
                table: "unidades_privadas",
                column: "torre_id");

            migrationBuilder.CreateIndex(
                name: "IX_zonas_comunes_tenant_id",
                table: "zonas_comunes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contratos_servicio");

            migrationBuilder.DropTable(
                name: "equipos_activos");

            migrationBuilder.DropTable(
                name: "miembros_consejo");

            migrationBuilder.DropTable(
                name: "unidades_privadas");

            migrationBuilder.DropTable(
                name: "zonas_comunes");

            migrationBuilder.DropTable(
                name: "torres");

            migrationBuilder.DropColumn(
                name: "ciudad",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "departamento",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "digito_verificacion",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "estrato",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "foto_fachada_url",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "tipo_copropiedad",
                table: "tenants");
        }
    }
}
