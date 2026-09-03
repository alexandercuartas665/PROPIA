using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V01PanelSnapshotSinRlsMasUnidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cantidad_unidades",
                table: "panel_snapshot_copropiedades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // V-01: panel_snapshot_copropiedades es GLOBAL de organizacion (una fila por Org+Tenant),
            // no una TenantEntity. Tenia por error una politica RLS por tenant que reventaba el recalculo
            // cross-tenant (insertar filas de otros tenants bajo el app.tenant_id activo -> 42501). Se quita
            // la RLS por tenant; la proteccion es por OrganizacionId a nivel de servicio (mismo patron que
            // el resto de tablas globales de Capa 1, ver QuitarRlsOrgColaboradorCopropiedades).
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON panel_snapshot_copropiedades;
                ALTER TABLE panel_snapshot_copropiedades NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE panel_snapshot_copropiedades DISABLE ROW LEVEL SECURITY;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE panel_snapshot_copropiedades ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON panel_snapshot_copropiedades
                    USING (tenant_id = current_tenant_id());
            ");

            migrationBuilder.DropColumn(
                name: "cantidad_unidades",
                table: "panel_snapshot_copropiedades");
        }
    }
}
