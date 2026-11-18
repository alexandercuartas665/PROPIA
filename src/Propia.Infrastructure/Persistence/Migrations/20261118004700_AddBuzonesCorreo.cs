using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lector de buzon: varios buzones por copropiedad (tabla buzones_correo, cada uno con su cuenta IMAP y su avance)
    /// y temas configurables (correos_recibidos.tema_clave = clave de la lista por copropiedad "correo.tema"). Pasa la
    /// lectura que estuviera activa en tenant_email_configs (Ola 1) a un buzon y quita de alli las columnas de lectura:
    /// el correo saliente queda independiente.
    /// </summary>
    public partial class AddBuzonesCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buzones_correo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cuenta = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_cifrada = table.Column<string>(type: "text", nullable: true),
                    imap_host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    imap_port = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    lectura_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uid_validity = table.Column<long>(type: "bigint", nullable: true),
                    ultimo_uid = table.Column<long>(type: "bigint", nullable: true),
                    ultima_revision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    temas_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buzones_correo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buzones_correo_tenant_id_cuenta",
                table: "buzones_correo",
                columns: new[] { "tenant_id", "cuenta" },
                unique: true);

            // RLS: buzones_correo es TenantEntity. FORCE para que tambien el owner respete la policy;
            // propia_app (runtime) solo ve las filas de la copropiedad fijada en app.tenant_id.
            migrationBuilder.Sql("ALTER TABLE buzones_correo ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE buzones_correo FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON buzones_correo
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON buzones_correo TO propia_app;");

            migrationBuilder.AddColumn<Guid>(
                name: "buzon_id",
                table: "correos_recibidos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tema_clave",
                table: "correos_recibidos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_correos_recibidos_buzon_id",
                table: "correos_recibidos",
                column: "buzon_id");

            migrationBuilder.AddForeignKey(
                name: "FK_correos_recibidos_buzones_correo_buzon_id",
                table: "correos_recibidos",
                column: "buzon_id",
                principalTable: "buzones_correo",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Datos (tablas con FORCE: NO FORCE solo durante el backfill).
            // 1) La lectura de la Ola 1 (activa o con cuenta y avance) pasa a un buzon "Administracion" con la misma
            //    cuenta, contrasena y avance por UID. 2) Los correos ya leidos quedan ligados a ese buzon. 3) El tema
            //    numerico de la Ola 1 pasa a la clave de la lista "correo.tema".
            migrationBuilder.Sql(@"
ALTER TABLE tenant_email_configs NO FORCE ROW LEVEL SECURITY;
ALTER TABLE buzones_correo NO FORCE ROW LEVEL SECURITY;
ALTER TABLE correos_recibidos NO FORCE ROW LEVEL SECURITY;

INSERT INTO buzones_correo (id, nombre, cuenta, password_cifrada, imap_host, imap_port, activo, lectura_desde,
                            uid_validity, ultimo_uid, ultima_revision, ultimo_error, temas_json, created_at, tenant_id)
SELECT gen_random_uuid(), 'Administraci' || chr(243) || 'n', trim(c.smtp_user), c.smtp_password_encrypted,
       COALESCE(NULLIF(trim(c.imap_host), ''), 'imap.gmail.com'), CASE WHEN c.imap_port > 0 THEN c.imap_port ELSE 993 END,
       c.lectura_activa, c.lectura_desde, c.lectura_uid_validity, c.lectura_ultimo_uid, c.lectura_ultima_revision,
       c.lectura_ultimo_error, NULL, now(), c.tenant_id
FROM tenant_email_configs c
WHERE (c.lectura_activa OR c.lectura_desde IS NOT NULL)
  AND c.smtp_user IS NOT NULL AND trim(c.smtp_user) <> ''
  AND NOT EXISTS (SELECT 1 FROM buzones_correo b WHERE b.tenant_id = c.tenant_id AND lower(b.cuenta) = lower(trim(c.smtp_user)));

UPDATE correos_recibidos r SET buzon_id = b.id
FROM buzones_correo b
WHERE r.buzon_id IS NULL AND b.tenant_id = r.tenant_id;

UPDATE correos_recibidos SET tema_clave = CASE tema
    WHEN 1 THEN 'recibo_servicio' WHEN 2 THEN 'pqrsd' WHEN 3 THEN 'poliza_contrato'
    WHEN 4 THEN 'otro' WHEN 5 THEN 'irrelevante' ELSE NULL END
WHERE tema_clave IS NULL;

ALTER TABLE correos_recibidos FORCE ROW LEVEL SECURITY;
ALTER TABLE buzones_correo FORCE ROW LEVEL SECURITY;
ALTER TABLE tenant_email_configs FORCE ROW LEVEL SECURITY;");

            migrationBuilder.DropColumn(name: "tema", table: "correos_recibidos");
            migrationBuilder.DropColumn(name: "imap_host", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "imap_port", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_activa", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_desde", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_uid_validity", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_ultima_revision", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_ultimo_error", table: "tenant_email_configs");
            migrationBuilder.DropColumn(name: "lectura_ultimo_uid", table: "tenant_email_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tema",
                table: "correos_recibidos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(name: "imap_host", table: "tenant_email_configs", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<int>(name: "imap_port", table: "tenant_email_configs", type: "integer", nullable: false, defaultValue: 993);
            migrationBuilder.AddColumn<bool>(name: "lectura_activa", table: "tenant_email_configs", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "lectura_desde", table: "tenant_email_configs", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<long>(name: "lectura_uid_validity", table: "tenant_email_configs", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "lectura_ultima_revision", table: "tenant_email_configs", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<string>(name: "lectura_ultimo_error", table: "tenant_email_configs", type: "character varying(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<long>(name: "lectura_ultimo_uid", table: "tenant_email_configs", type: "bigint", nullable: true);

            migrationBuilder.Sql(@"
ALTER TABLE correos_recibidos NO FORCE ROW LEVEL SECURITY;
UPDATE correos_recibidos SET tema = CASE tema_clave
    WHEN 'recibo_servicio' THEN 1 WHEN 'pqrsd' THEN 2 WHEN 'poliza_contrato' THEN 3
    WHEN 'otro' THEN 4 WHEN 'irrelevante' THEN 5 ELSE 0 END;
ALTER TABLE correos_recibidos FORCE ROW LEVEL SECURITY;
ALTER TABLE tenant_email_configs NO FORCE ROW LEVEL SECURITY;
UPDATE tenant_email_configs SET imap_host = 'imap.gmail.com' WHERE imap_host IS NULL;
ALTER TABLE tenant_email_configs FORCE ROW LEVEL SECURITY;");

            migrationBuilder.DropForeignKey(
                name: "FK_correos_recibidos_buzones_correo_buzon_id",
                table: "correos_recibidos");

            migrationBuilder.DropTable(
                name: "buzones_correo");

            migrationBuilder.DropIndex(
                name: "IX_correos_recibidos_buzon_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "buzon_id",
                table: "correos_recibidos");

            migrationBuilder.DropColumn(
                name: "tema_clave",
                table: "correos_recibidos");
        }
    }
}
