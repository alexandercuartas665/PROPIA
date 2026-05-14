using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnableRlsMiCopropiedad : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Las 6 tablas del modulo 2.3 son TenantEntity y deben respetar RLS por tenant_id.
        // Mismo patron que en usuarios_tenant: ENABLE + FORCE + POLICY tenant_isolation.
        var tablas = new[]
        {
            "torres",
            "unidades_privadas",
            "zonas_comunes",
            "equipos_activos",
            "contratos_servicio",
            "miembros_consejo"
        };

        foreach (var tabla in tablas)
        {
            migrationBuilder.Sql($@"
ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON {tabla}
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());
");
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        var tablas = new[]
        {
            "torres", "unidades_privadas", "zonas_comunes",
            "equipos_activos", "contratos_servicio", "miembros_consejo"
        };
        foreach (var tabla in tablas)
        {
            migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {tabla};");
            migrationBuilder.Sql($"ALTER TABLE {tabla} DISABLE ROW LEVEL SECURITY;");
        }
    }
}
