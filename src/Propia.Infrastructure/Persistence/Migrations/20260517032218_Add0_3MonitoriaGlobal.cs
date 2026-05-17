using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add0_3MonitoriaGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "metricas_uso_diarias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    total_tenants = table.Column<int>(type: "integer", nullable: false),
                    tenants_activos = table.Column<int>(type: "integer", nullable: false),
                    total_organizaciones = table.Column<int>(type: "integer", nullable: false),
                    total_usuarios = table.Column<int>(type: "integer", nullable: false),
                    total_super_admins = table.Column<int>(type: "integer", nullable: false),
                    tareas_creadas24h = table.Column<int>(type: "integer", nullable: false),
                    pqrsds_radicadas24h = table.Column<int>(type: "integer", nullable: false),
                    comunicados_enviados24h = table.Column<int>(type: "integer", nullable: false),
                    notificaciones_despachadas24h = table.Column<int>(type: "integer", nullable: false),
                    incidentes_abiertos = table.Column<int>(type: "integer", nullable: false),
                    incidentes_criticos = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metricas_uso_diarias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sistema_incidentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    severidad = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    servicio_afectado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_impactado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asignado_super_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detectado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resuelto_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    causa_raiz = table.Column<string>(type: "text", nullable: true),
                    solucion_aplicada = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sistema_incidentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sistema_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    severidad = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    modulo_origen_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    detalle_json = table.Column<string>(type: "text", nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sistema_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_metricas_uso_diarias_fecha",
                table: "metricas_uso_diarias",
                column: "fecha",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sistema_incidentes_asignado_super_admin_id",
                table: "sistema_incidentes",
                column: "asignado_super_admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_sistema_incidentes_detectado_at",
                table: "sistema_incidentes",
                column: "detectado_at");

            migrationBuilder.CreateIndex(
                name: "IX_sistema_incidentes_estado_severidad",
                table: "sistema_incidentes",
                columns: new[] { "estado", "severidad" });

            migrationBuilder.CreateIndex(
                name: "IX_sistema_logs_created_at",
                table: "sistema_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_sistema_logs_severidad_created_at",
                table: "sistema_logs",
                columns: new[] { "severidad", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sistema_logs_tenant_id",
                table: "sistema_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sistema_logs_tipo_evento_created_at",
                table: "sistema_logs",
                columns: new[] { "tipo_evento", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metricas_uso_diarias");

            migrationBuilder.DropTable(
                name: "sistema_incidentes");

            migrationBuilder.DropTable(
                name: "sistema_logs");
        }
    }
}
