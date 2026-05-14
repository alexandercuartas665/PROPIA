using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnableRowLevelSecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------------
        // Funcion: current_tenant_id()
        // Lee la variable de sesion app.tenant_id. Si no esta seteada o esta vacia,
        // retorna NULL - eso fuerza a que las policies bloqueen todas las filas.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION current_tenant_id() RETURNS uuid AS $$
DECLARE
    val text;
BEGIN
    BEGIN
        val := current_setting('app.tenant_id', true);
    EXCEPTION WHEN OTHERS THEN
        val := NULL;
    END;
    IF val IS NULL OR val = '' THEN
        RETURN NULL;
    END IF;
    RETURN val::uuid;
END;
$$ LANGUAGE plpgsql STABLE;
");

        // ---------------------------------------------------------------------
        // Activar Row-Level Security en tablas con tenant_id.
        // FORCE: aplica incluso al rol propietario de la tabla.
        // Policy tenant_isolation: solo filas donde tenant_id coincide con la sesion.
        //
        // IMPORTANTE: las tablas globales (tenants, organizaciones, personas,
        // empresas, super_admin_*) NO llevan RLS porque no tienen tenant_id.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
ALTER TABLE usuarios_tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE usuarios_tenant FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON usuarios_tenant
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());
");

        // ---------------------------------------------------------------------
        // Trigger de inmutabilidad para super_admin_logs.
        // Bloquea UPDATE y DELETE - solo INSERT permitido.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION super_admin_log_immutable() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'super_admin_logs es append-only - UPDATE y DELETE estan prohibidos';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_super_admin_logs_immutable
    BEFORE UPDATE OR DELETE ON super_admin_logs
    FOR EACH ROW EXECUTE FUNCTION super_admin_log_immutable();
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_super_admin_logs_immutable ON super_admin_logs;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS super_admin_log_immutable();");
        migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON usuarios_tenant;");
        migrationBuilder.Sql("ALTER TABLE usuarios_tenant DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS current_tenant_id();");
    }
}
