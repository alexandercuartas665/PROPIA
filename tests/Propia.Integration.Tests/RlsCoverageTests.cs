using Microsoft.EntityFrameworkCore;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Test GENERICO de cobertura de Row-Level Security.
///
/// No comprueba una tabla concreta: recorre el catalogo de PostgreSQL de la base real
/// (Testcontainers, con TODAS las migraciones aplicadas) y exige que CUALQUIER tabla
/// que tenga columna tenant_id cumpla las tres condiciones del contrato multi-tenant:
///   1. relrowsecurity     = true  -> RLS habilitada
///   2. relforcerowsecurity= true  -> FORCE, para que ni el owner de la tabla la evada
///   3. al menos una fila en pg_policy -> existe politica de aislamiento
///
/// Asi, cualquier tabla de tenant que se agregue en el futuro sin RLS rompe este test
/// sin que nadie tenga que acordarse de escribir un test por entidad.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RlsCoverageTests
{
    private readonly PostgresFixture _fx;

    public RlsCoverageTests(PostgresFixture fx) => _fx = fx;

    /// <summary>
    /// Tablas que tienen columna tenant_id pero NO son tablas operativas de Capa 2.
    ///
    /// Esta lista debe quedarse VACIA. NO agregar tablas aqui para "hacer pasar" el test:
    /// si el test senala una tabla incumplidora, la solucion es agregarle RLS en una
    /// migracion, no exonerarla. Solo se justifica una entrada aqui si la tabla es
    /// demostrablemente global y su tenant_id es un dato descriptivo, no de aislamiento.
    /// </summary>
    private static readonly string[] TablasExentas = Array.Empty<string>();

    /// <summary>Tablas de infraestructura que nunca llevan RLS.</summary>
    private static readonly string[] TablasInfraestructura = ["__EFMigrationsHistory"];

    [Fact]
    public async Task Toda_tabla_con_tenant_id_tiene_RLS_FORCE_y_politica()
    {
        var tablas = await LeerEstadoRlsAsync();

        // Sanity check: si la consulta de catalogo no devuelve nada, el test seria un
        // falso verde. La base migrada tiene decenas de tablas de tenant.
        Assert.True(
            tablas.Count > 0,
            "La consulta de catalogo no encontro ninguna tabla con columna tenant_id. "
            + "Revisar la consulta o el estado de las migraciones: el test estaria dando un falso verde.");

        var sinRls = tablas.Where(t => !t.RowSecurity).Select(t => t.Nombre).ToList();
        var sinForce = tablas.Where(t => t.RowSecurity && !t.ForceRowSecurity).Select(t => t.Nombre).ToList();
        var sinPolitica = tablas.Where(t => t.Politicas == 0).Select(t => t.Nombre).ToList();

        var errores = new List<string>();
        if (sinRls.Count > 0)
            errores.Add($"Tablas de tenant sin RLS habilitada (relrowsecurity=false) [{sinRls.Count}]: {string.Join(", ", sinRls)}");
        if (sinForce.Count > 0)
            errores.Add($"Tablas de tenant sin RLS FORCE (relforcerowsecurity=false) [{sinForce.Count}]: {string.Join(", ", sinForce)}");
        if (sinPolitica.Count > 0)
            errores.Add($"Tablas de tenant sin ninguna politica en pg_policy [{sinPolitica.Count}]: {string.Join(", ", sinPolitica)}");

        // Pista de triage (NO una exencion): una tabla incumplidora cuyo tenant_id es NULLABLE
        // suele ser una tabla global con referencia opcional al tenant. En esas la politica
        // simple tenant_id = current_tenant_id() ocultaria las filas con tenant_id NULL, asi que
        // hay que decidir explicitamente la politica correcta, no copiarla y pegarla.
        var incumplidoras = tablas
            .Where(t => !t.RowSecurity || !t.ForceRowSecurity || t.Politicas == 0)
            .ToList();
        var nullables = incumplidoras.Where(t => t.TenantIdNullable).Select(t => t.Nombre).ToList();

        var pista = nullables.Count == 0
            ? string.Empty
            : Environment.NewLine
              + $"De las anteriores, estas tienen tenant_id NULLABLE (revisar si son globales) [{nullables.Count}]: "
              + string.Join(", ", nullables);

        Assert.True(
            errores.Count == 0,
            $"Se revisaron {tablas.Count} tablas con columna tenant_id y hay {errores.Count} regla(s) de RLS incumplidas."
            + Environment.NewLine
            + string.Join(Environment.NewLine, errores)
            + pista
            + Environment.NewLine
            + "Cada tabla listada necesita, en una migracion: ENABLE ROW LEVEL SECURITY, "
            + "FORCE ROW LEVEL SECURITY, CREATE POLICY tenant_isolation USING (tenant_id = current_tenant_id()) "
            + "WITH CHECK (tenant_id = current_tenant_id()) y GRANT a propia_app.");
    }

    /// <summary>
    /// Complemento del test anterior: las politicas de las tablas de tenant deben aplicar a
    /// PUBLIC o al rol de aplicacion. Una politica creada solo para otro rol dejaria la tabla
    /// con "al menos una politica" pero sin aislamiento efectivo para la aplicacion.
    /// </summary>
    [Fact]
    public async Task Politicas_de_tenant_aplican_al_rol_de_aplicacion()
    {
        var tablas = await LeerEstadoRlsAsync();
        var conPoliticas = tablas.Where(t => t.Politicas > 0).ToList();

        Assert.True(conPoliticas.Count > 0, "No hay ninguna tabla de tenant con politicas: revisar migraciones.");

        var sinPoliticaAplicable = conPoliticas
            .Where(t => t.PoliticasAplicablesAApp == 0)
            .Select(t => t.Nombre)
            .ToList();

        Assert.True(
            sinPoliticaAplicable.Count == 0,
            "Tablas de tenant cuyas politicas no aplican ni a PUBLIC ni al rol propia_app "
            + $"[{sinPoliticaAplicable.Count}]: {string.Join(", ", sinPoliticaAplicable)}");
    }

    // ---------------- Helpers ----------------

    private sealed record EstadoRlsTabla(
        string Nombre,
        bool RowSecurity,
        bool ForceRowSecurity,
        int Politicas,
        int PoliticasAplicablesAApp,
        bool TenantIdNullable);

    /// <summary>
    /// Lee el estado de RLS de todas las tablas ordinarias del esquema public que tienen
    /// columna tenant_id. Se conecta con el rol de aplicacion (propia_app): los catalogos
    /// de PostgreSQL son legibles por cualquier rol y asi el test refleja lo que ve la app.
    /// </summary>
    private async Task<List<EstadoRlsTabla>> LeerEstadoRlsAsync()
    {
        const string sql = @"
            SELECT c.relname                                                     AS nombre,
                   c.relrowsecurity                                              AS row_security,
                   c.relforcerowsecurity                                         AS force_row_security,
                   (SELECT count(*) FROM pg_policy p WHERE p.polrelid = c.oid)   AS politicas,
                   (SELECT count(*) FROM pg_policy p
                      WHERE p.polrelid = c.oid
                        AND (p.polroles = '{0}'::oid[]
                             OR EXISTS (SELECT 1 FROM pg_roles r
                                          WHERE r.oid = ANY (p.polroles)
                                            AND r.rolname = 'propia_app')))      AS politicas_app,
                   (SELECT col.is_nullable = 'YES'
                      FROM information_schema.columns col
                     WHERE col.table_schema = 'public'
                       AND col.table_name = c.relname
                       AND col.column_name = 'tenant_id')         AS tenant_id_nullable
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind = 'r'
              AND EXISTS (SELECT 1
                            FROM information_schema.columns col
                           WHERE col.table_schema = 'public'
                             AND col.table_name = c.relname
                             AND col.column_name = 'tenant_id')
            ORDER BY c.relname;";

        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());

        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var resultado = new List<EstadoRlsTabla>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var nombre = reader.GetString(0);
            if (TablasInfraestructura.Contains(nombre, StringComparer.OrdinalIgnoreCase)) continue;
            if (TablasExentas.Contains(nombre, StringComparer.OrdinalIgnoreCase)) continue;

            resultado.Add(new EstadoRlsTabla(
                nombre,
                reader.GetBoolean(1),
                reader.GetBoolean(2),
                Convert.ToInt32(reader.GetValue(3)),
                Convert.ToInt32(reader.GetValue(4)),
                !reader.IsDBNull(5) && reader.GetBoolean(5)));
        }

        return resultado;
    }
}
