using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiciosYContratos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "expediente_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proyecto_tarea_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "renovacion_automatica",
                table: "contratos_servicio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "servicio_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contrato_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_contrato_adjuntos_contratos_servicio_contrato_id",
                        column: x => x.contrato_id,
                        principalTable: "contratos_servicio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ejecutor_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ejecutor_empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ejecutor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    costo_mensual = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    costo_anual = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "servicio_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicio_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_servicio_adjuntos_servicios_servicio_id",
                        column: x => x.servicio_id,
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servicio_contactos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rol = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    telefono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicio_contactos", x => x.id);
                    table.ForeignKey(
                        name: "FK_servicio_contactos_servicios_servicio_id",
                        column: x => x.servicio_id,
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contratos_servicio_servicio_id",
                table: "contratos_servicio",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_adjuntos_contrato_id",
                table: "contrato_adjuntos",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_adjuntos_tenant_id",
                table: "contrato_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_adjuntos_servicio_id",
                table: "servicio_adjuntos",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_adjuntos_tenant_id",
                table: "servicio_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_contactos_servicio_id",
                table: "servicio_contactos",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_contactos_tenant_id",
                table: "servicio_contactos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicios_tenant_id",
                table: "servicios",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_contratos_servicio_servicios_servicio_id",
                table: "contratos_servicio",
                column: "servicio_id",
                principalTable: "servicios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS por tenant (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "servicios", "servicio_contactos", "servicio_adjuntos", "contrato_adjuntos" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contratos_servicio_servicios_servicio_id",
                table: "contratos_servicio");

            migrationBuilder.DropTable(
                name: "contrato_adjuntos");

            migrationBuilder.DropTable(
                name: "servicio_adjuntos");

            migrationBuilder.DropTable(
                name: "servicio_contactos");

            migrationBuilder.DropTable(
                name: "servicios");

            migrationBuilder.DropIndex(
                name: "IX_contratos_servicio_servicio_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "expediente_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "proyecto_tarea_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "renovacion_automatica",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "servicio_id",
                table: "contratos_servicio");
        }
    }
}
