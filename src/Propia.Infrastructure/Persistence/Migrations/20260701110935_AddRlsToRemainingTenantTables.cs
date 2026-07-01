using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsToRemainingTenantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // H1 (auditoria jul 2026) - backfill de RLS. Varias tablas TenantEntity (porteria,
            // reservas, servicios / servicios publicos, tableros, IA/WhatsApp, adjuntos, campos EAV,
            // bloques V3 de unidad, etc.) quedaron solo con el filtro EF (que se DESACTIVA cuando
            // app.tenant_id es NULL) y SIN politica RLS de respaldo en Postgres. Esto activa RLS +
            // policy tenant_isolation en TODA tabla del schema public con columna tenant_id NOT NULL
            // que aun no tenga RLS. No toca las que ya la tienen (NOT relrowsecurity). Mismo patron
            // que EnableRlsMiCopropiedad: FORCE + tenant_id = current_tenant_id() + grant a propia_app.
            migrationBuilder.Sql(@"
DO $$
DECLARE t text;
BEGIN
    FOR t IN
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = c.oid
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
          AND NOT c.relrowsecurity
          AND a.attname = 'tenant_id'
          AND a.attnum > 0
          AND NOT a.attisdropped
          AND a.attnotnull
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', t);
        EXECUTE format('CREATE POLICY tenant_isolation ON public.%I USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id())', t);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON public.%I TO propia_app', t);
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: es un backfill de seguridad. Revertirlo re-abriria el aislamiento
            // (y no podriamos distinguir las tablas que este migration activo de las preexistentes).
            // Si hiciera falta desactivar RLS en una tabla puntual, hacerlo con un migration explicito.
        }
    }
}
