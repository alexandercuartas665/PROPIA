using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLectorBuzon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "imap_host",
                table: "tenant_email_configs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "imap_port",
                table: "tenant_email_configs",
                type: "integer",
                nullable: false,
                defaultValue: 993);

            migrationBuilder.AddColumn<bool>(
                name: "lectura_activa",
                table: "tenant_email_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lectura_desde",
                table: "tenant_email_configs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "lectura_uid_validity",
                table: "tenant_email_configs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lectura_ultima_revision",
                table: "tenant_email_configs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lectura_ultimo_error",
                table: "tenant_email_configs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "lectura_ultimo_uid",
                table: "tenant_email_configs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "correos_recibidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    imap_uid = table.Column<long>(type: "bigint", nullable: false),
                    remitente_correo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    remitente_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    para = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    asunto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_correo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: true),
                    adjuntos_json = table.Column<string>(type: "text", nullable: true),
                    tema = table.Column<int>(type: "integer", nullable: false),
                    confianza = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    resumen = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    accion_sugerida = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    datos_json = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    clasificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correos_recibidos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_correos_recibidos_tenant_id_estado",
                table: "correos_recibidos",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_correos_recibidos_tenant_id_fecha_correo",
                table: "correos_recibidos",
                columns: new[] { "tenant_id", "fecha_correo" });

            migrationBuilder.CreateIndex(
                name: "IX_correos_recibidos_tenant_id_message_id",
                table: "correos_recibidos",
                columns: new[] { "tenant_id", "message_id" },
                unique: true);

            // Cuentas ya configuradas (correo saliente): IMAP de Gmail por defecto.
            migrationBuilder.Sql("ALTER TABLE tenant_email_configs NO FORCE ROW LEVEL SECURITY; UPDATE tenant_email_configs SET imap_host = 'imap.gmail.com' WHERE imap_host IS NULL; ALTER TABLE tenant_email_configs FORCE ROW LEVEL SECURITY;");

            // RLS: correos_recibidos es TenantEntity. FORCE para que tambien el owner respete la policy;
            // propia_app (runtime) solo ve las filas de la copropiedad fijada en app.tenant_id.
            migrationBuilder.Sql("ALTER TABLE correos_recibidos ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE correos_recibidos FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON correos_recibidos
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON correos_recibidos TO propia_app;");

            // Menu: "Correos recibidos" (com-correos) va en el grupo de Infraestructura (decision de Alex).
            // 1) Si existe una seccion propia visible cuyo nombre empieza por "Infraestructura", al final de ella.
            // 2) Si no, junto a "Agentes de IA" cuando el Super Admin la movio a otra seccion.
            // 3) Si no, queda en Comunicacion (catalogo base, sin override). Idempotente (igual que AddMenuSiniestros).
            migrationBuilder.Sql(@"
INSERT INTO menu_overrides (id, node_key, parent_key, sort_order, created_at, is_custom, hidden)
SELECT gen_random_uuid(), 'com-correos', t.parent_key, t.sort_order, now(), false, false
FROM (
    SELECT s.node_key AS parent_key,
           COALESCE((SELECT MAX(i.sort_order) FROM menu_overrides i WHERE i.parent_key = s.node_key), 0) + 1 AS sort_order,
           1 AS prio
    FROM menu_overrides s
    WHERE s.parent_key IS NULL AND s.is_custom AND NOT s.hidden AND s.label ILIKE 'Infraestructura%'
    UNION ALL
    SELECT s.parent_key, COALESCE(s.sort_order, 0) + 1, 2
    FROM menu_overrides s
    WHERE s.node_key = 'ia-agentes' AND s.parent_key IS NOT NULL
) t
WHERE NOT EXISTS (SELECT 1 FROM menu_overrides x WHERE x.node_key = 'com-correos')
ORDER BY t.prio, t.parent_key
LIMIT 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_overrides WHERE node_key = 'com-correos';");

            migrationBuilder.DropTable(
                name: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "imap_host",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "imap_port",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_activa",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_desde",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_uid_validity",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_ultima_revision",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_ultimo_error",
                table: "tenant_email_configs");

            migrationBuilder.DropColumn(
                name: "lectura_ultimo_uid",
                table: "tenant_email_configs");
        }
    }
}
