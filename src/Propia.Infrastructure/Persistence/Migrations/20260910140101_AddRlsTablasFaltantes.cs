using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Migracion SOLO de seguridad: no cambia el modelo ni el esquema.
    ///
    /// Cierra el hallazgo de RlsCoverageTests: 18 tablas operativas de Capa 2 (TenantEntity,
    /// todas con tenant_id NOT NULL) estaban sin Row-Level Security (relrowsecurity=false y
    /// cero politicas). El HasQueryFilter de EF Core las seguia filtrando, pero les faltaba la
    /// red de seguridad final que exige el contrato multi-tenant del proyecto.
    ///
    /// Patron replicado de AddCamposEntidadesVinculadasUnidad / AddEquipoZonaCampoDefiniciones:
    /// ENABLE + FORCE ROW LEVEL SECURITY, politica tenant_isolation sobre current_tenant_id()
    /// y GRANT al rol de aplicacion propia_app. El DROP POLICY IF EXISTS previo hace el bloque
    /// reaplicable sin error.
    /// </remarks>
    public partial class AddRlsTablasFaltantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE contrato_expedientes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE contrato_expedientes FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON contrato_expedientes;
                CREATE POLICY tenant_isolation ON contrato_expedientes
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON contrato_expedientes TO propia_app;

                ALTER TABLE contrato_etapas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE contrato_etapas FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON contrato_etapas;
                CREATE POLICY tenant_isolation ON contrato_etapas
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON contrato_etapas TO propia_app;

                ALTER TABLE contrato_campos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE contrato_campos FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON contrato_campos;
                CREATE POLICY tenant_isolation ON contrato_campos
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON contrato_campos TO propia_app;

                ALTER TABLE contrato_campo_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE contrato_campo_valores FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON contrato_campo_valores;
                CREATE POLICY tenant_isolation ON contrato_campo_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON contrato_campo_valores TO propia_app;

                ALTER TABLE directorio_contactos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directorio_contactos FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON directorio_contactos;
                CREATE POLICY tenant_isolation ON directorio_contactos
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON directorio_contactos TO propia_app;

                ALTER TABLE directorio_adjuntos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directorio_adjuntos FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON directorio_adjuntos;
                CREATE POLICY tenant_isolation ON directorio_adjuntos
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON directorio_adjuntos TO propia_app;

                ALTER TABLE etiquetas_usuario ENABLE ROW LEVEL SECURITY;
                ALTER TABLE etiquetas_usuario FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON etiquetas_usuario;
                CREATE POLICY tenant_isolation ON etiquetas_usuario
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON etiquetas_usuario TO propia_app;

                ALTER TABLE usuario_tenant_etiquetas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE usuario_tenant_etiquetas FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON usuario_tenant_etiquetas;
                CREATE POLICY tenant_isolation ON usuario_tenant_etiquetas
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_tenant_etiquetas TO propia_app;

                ALTER TABLE informes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE informes FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON informes;
                CREATE POLICY tenant_isolation ON informes
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON informes TO propia_app;

                ALTER TABLE informe_secciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE informe_secciones FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON informe_secciones;
                CREATE POLICY tenant_isolation ON informe_secciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON informe_secciones TO propia_app;

                ALTER TABLE informe_plantillas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE informe_plantillas FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON informe_plantillas;
                CREATE POLICY tenant_isolation ON informe_plantillas
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON informe_plantillas TO propia_app;

                ALTER TABLE informe_plantilla_secciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE informe_plantilla_secciones FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON informe_plantilla_secciones;
                CREATE POLICY tenant_isolation ON informe_plantilla_secciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON informe_plantilla_secciones TO propia_app;

                ALTER TABLE polizas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE polizas FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON polizas;
                CREATE POLICY tenant_isolation ON polizas
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON polizas TO propia_app;

                ALTER TABLE poliza_campos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE poliza_campos FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON poliza_campos;
                CREATE POLICY tenant_isolation ON poliza_campos
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON poliza_campos TO propia_app;

                ALTER TABLE poliza_campo_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE poliza_campo_valores FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON poliza_campo_valores;
                CREATE POLICY tenant_isolation ON poliza_campo_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON poliza_campo_valores TO propia_app;

                ALTER TABLE poliza_reclamaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE poliza_reclamaciones FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON poliza_reclamaciones;
                CREATE POLICY tenant_isolation ON poliza_reclamaciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON poliza_reclamaciones TO propia_app;

                ALTER TABLE pqrsd_formulario_publico_configs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_formulario_publico_configs FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON pqrsd_formulario_publico_configs;
                CREATE POLICY tenant_isolation ON pqrsd_formulario_publico_configs
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_formulario_publico_configs TO propia_app;

                ALTER TABLE pqrsd_tareas_configs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_tareas_configs FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON pqrsd_tareas_configs;
                CREATE POLICY tenant_isolation ON pqrsd_tareas_configs
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_tareas_configs TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON contrato_expedientes;
                ALTER TABLE contrato_expedientes NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE contrato_expedientes DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON contrato_etapas;
                ALTER TABLE contrato_etapas NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE contrato_etapas DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON contrato_campos;
                ALTER TABLE contrato_campos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE contrato_campos DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON contrato_campo_valores;
                ALTER TABLE contrato_campo_valores NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE contrato_campo_valores DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON directorio_contactos;
                ALTER TABLE directorio_contactos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE directorio_contactos DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON directorio_adjuntos;
                ALTER TABLE directorio_adjuntos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE directorio_adjuntos DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON etiquetas_usuario;
                ALTER TABLE etiquetas_usuario NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE etiquetas_usuario DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON usuario_tenant_etiquetas;
                ALTER TABLE usuario_tenant_etiquetas NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE usuario_tenant_etiquetas DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON informes;
                ALTER TABLE informes NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE informes DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON informe_secciones;
                ALTER TABLE informe_secciones NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE informe_secciones DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON informe_plantillas;
                ALTER TABLE informe_plantillas NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE informe_plantillas DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON informe_plantilla_secciones;
                ALTER TABLE informe_plantilla_secciones NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE informe_plantilla_secciones DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON polizas;
                ALTER TABLE polizas NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE polizas DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON poliza_campos;
                ALTER TABLE poliza_campos NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE poliza_campos DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON poliza_campo_valores;
                ALTER TABLE poliza_campo_valores NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE poliza_campo_valores DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON poliza_reclamaciones;
                ALTER TABLE poliza_reclamaciones NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE poliza_reclamaciones DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON pqrsd_formulario_publico_configs;
                ALTER TABLE pqrsd_formulario_publico_configs NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_formulario_publico_configs DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS tenant_isolation ON pqrsd_tareas_configs;
                ALTER TABLE pqrsd_tareas_configs NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_tareas_configs DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
