using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuitarRlsOrgColaboradorCopropiedades : Migration
    {
        // org_colaborador_copropiedades es una tabla de ORGANIZACION (Capa 1). Se le habia puesto
        // RLS de tenant (tenant_id = current_tenant_id()) por tener tenant_id, pero eso IMPIDE las
        // operaciones org que cruzan copropiedades (asignar colaborador a todas, reasignar al
        // desactivar): esas escrituras insertan filas de varios tenants en una sola sesion.
        // El resto de tablas org_* NO llevan RLS; alineamos esta con ellas. El aislamiento sigue
        // por los EF query filters + la autorizacion de la capa de aplicacion.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON org_colaborador_copropiedades;
                ALTER TABLE org_colaborador_copropiedades NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE org_colaborador_copropiedades DISABLE ROW LEVEL SECURITY;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE org_colaborador_copropiedades ENABLE ROW LEVEL SECURITY;
                ALTER TABLE org_colaborador_copropiedades FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON org_colaborador_copropiedades
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_colaborador_copropiedades TO propia_app;
            ");
        }
    }
}
