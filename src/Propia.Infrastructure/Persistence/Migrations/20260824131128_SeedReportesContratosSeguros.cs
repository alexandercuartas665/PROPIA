using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedReportesContratosSeguros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ola 6 - Reportes de Contratos (2.5) y Seguros. Nueva categoria global
            // (tenant_id NULL) + 3 reportes base. Idempotente para poder re-aplicar en prod.

            // "Otros" (orden 8) baja a 9 para dejar sitio a la nueva categoria.
            migrationBuilder.Sql(@"
                UPDATE reporte_categorias SET orden = 9
                WHERE id = 'aaaaaaaa-0001-4000-8000-000000000008' AND orden = 8;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO reporte_categorias (id, tenant_id, nombre, icono, color, modulo_origen, orden, es_activa, created_at, created_by)
                VALUES ('aaaaaaaa-0001-4000-8000-000000000009', NULL, 'Contratos y Seguros', 'fi-rr-file-invoice', '#0891b2', '2.5', 8, true, now(), NULL)
                ON CONFLICT (id) DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO reporte_catalogo (id, tenant_id, categoria_id, nombre, descripcion, modulo_origen, clave, audiencias_json, es_activo, es_sistema, orden, created_at, created_by)
                SELECT gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000009', v.nombre, v.descripcion, '2.5', v.clave, v.audiencias, true, true, v.orden, now(), NULL
                FROM (VALUES
                    ('Contratos proximos a vencer',
                        'Contratos con semaforo amarillo/rojo por % de dias transcurridos, con dias restantes y valor.',
                        'contratos.por_vencer', '[""ADMINISTRADOR"",""CONSEJO""]', 1),
                    ('Resumen de contratos',
                        'Contratos activos, por vencer, vencidos y valor total contratado del periodo.',
                        'contratos.resumen', '[""ADMINISTRADOR"",""CONSEJO""]', 2),
                    ('Polizas de seguro proximas a vencer',
                        'Polizas con semaforo de vencimiento, dias restantes y valor asegurado.',
                        'seguros.polizas_por_vencer', '[""ADMINISTRADOR"",""CONSEJO""]', 3)
                ) AS v(nombre, descripcion, clave, audiencias, orden)
                WHERE NOT EXISTS (
                    SELECT 1 FROM reporte_catalogo rc WHERE rc.clave = v.clave AND rc.tenant_id IS NULL
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM reporte_catalogo
                WHERE tenant_id IS NULL AND clave IN ('contratos.por_vencer', 'contratos.resumen', 'seguros.polizas_por_vencer');
            ");
            migrationBuilder.Sql(@"
                DELETE FROM reporte_categorias WHERE id = 'aaaaaaaa-0001-4000-8000-000000000009';
            ");
            migrationBuilder.Sql(@"
                UPDATE reporte_categorias SET orden = 8
                WHERE id = 'aaaaaaaa-0001-4000-8000-000000000008' AND orden = 9;
            ");
        }
    }
}
