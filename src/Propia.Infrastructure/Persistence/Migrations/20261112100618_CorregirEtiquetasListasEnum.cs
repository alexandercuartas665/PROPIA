using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Datos: etiquetas de listas sembradas con el nombre crudo del enum ("EnEjecucion", "Circulacion"...) pasan a
    /// su texto legible (mismo mapa que CatalogoListasSeeder.EtiquetasEnum). Solo donde la etiqueta es EXACTAMENTE la
    /// cruda: no pisa lo que A&amp;D renombro en el catalogo global ni lo que la copropiedad edito (override_visual).
    /// La clave "e:&lt;Enum&gt;" y los registros (que guardan el enum) no cambian.
    /// </summary>
    public partial class CorregirEtiquetasListasEnum : Migration
    {
        private static readonly (string Lista, string Enum, string Label)[] Cambios =
        {
            ("mantenimiento.intervestado", "EnEjecucion", "En ejecución"),
            ("mantenimiento.intervestado", "EnRevision", "En revisión"),
            ("mantenimiento.periodicidad", "Unica", "Única"),
            ("equipo.categoria", "Climatizacion", "Climatización"),
            ("equipo.categoria", "Recreacion", "Recreación"),
            ("zona.categoria", "Circulacion", "Circulación"),
            ("zona.estado", "EnMantenimiento", "En mantenimiento"),
            ("persona.sexo", "NoBinario", "No binario"),
            ("persona.sexo", "NoIdentificado", "No identificado"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Aplicar(migrationBuilder, haciaLegible: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Aplicar(migrationBuilder, haciaLegible: false);

        private static void Aplicar(MigrationBuilder mb, bool haciaLegible)
        {
            var sb = new System.Text.StringBuilder();
            // lista_opciones tiene RLS FORCE: el owner solo la actualiza con el FORCE suspendido durante el UPDATE.
            sb.AppendLine("ALTER TABLE lista_opciones NO FORCE ROW LEVEL SECURITY;");
            foreach (var (lista, nombre, label) in Cambios)
            {
                var desde = (haciaLegible ? nombre : label).Replace("'", "''");
                var hacia = (haciaLegible ? label : nombre).Replace("'", "''");
                var clave = "e:" + nombre;
                sb.AppendLine($"UPDATE catalogo_opciones SET label = '{hacia}', updated_at = now() WHERE lista = '{lista}' AND clave = '{clave}' AND label = '{desde}';");
                sb.AppendLine($"UPDATE lista_opciones SET label = '{hacia}', updated_at = now() WHERE lista = '{lista}' AND clave = '{clave}' AND label = '{desde}' AND override_visual = false;");
            }
            sb.AppendLine("ALTER TABLE lista_opciones FORCE ROW LEVEL SECURITY;");
            mb.Sql(sb.ToString());
        }
    }
}
