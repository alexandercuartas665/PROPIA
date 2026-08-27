using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsRespuestasVersionesDestinatariosGmail : Migration
    {
        // Tablas multi-tenant creadas recientemente sin su politica RLS (red de seguridad de BD).
        // gmail_envio_app_configs NO va aqui: es global (sin tenant_id).
        private static readonly string[] Tablas =
        {
            "pqrsd_respuesta_versiones",
            "pqrsd_respuesta_destinatarios",
            "gmail_envio_conexiones"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var t in Tablas)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {t} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {t}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var t in Tablas)
            {
                migrationBuilder.Sql($@"
                    DROP POLICY IF EXISTS tenant_isolation ON {t};
                    ALTER TABLE {t} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE {t} DISABLE ROW LEVEL SECURITY;
                ");
            }
        }
    }
}
