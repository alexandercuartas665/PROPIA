using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Contrato de la carga masiva de unidades: un campo de sistema que la copropiedad tenga VISIBLE
/// tiene que salir como columna en la plantilla Y poder importarse.
///
/// Existe porque se rompio: la lista de columnas de UnidadesPlantillaService estaba duplicada a mano
/// y se quedo corta frente a la tabla de unidades. AREA, ESTADO, PISO, HABITACIONES, BANOS,
/// PARQUEADEROS, PAGA ADMIN, CUOTA MENSUAL y OBSERVACIONES se podian activar en Configurar pero no
/// salian en la plantilla, asi que no habia forma de cargarlos.
///
/// El test recorre UnidadCamposSistema.Todos COMPLETO, no una lista propia: si manana se agrega un
/// campo de sistema al catalogo y no se emite o no se lee, este test falla.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class UnidadesPlantillaRoundTripTests
{
    private readonly PostgresFixture _fx;

    public UnidadesPlantillaRoundTripTests(PostgresFixture fx) => _fx = fx;

    // Valor de prueba por campo, indexado por el ENCABEZADO de la plantilla.
    private static readonly Dictionary<string, string> Valores = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNIDAD PRIVADA"] = "T1-901",
        ["TIPO"] = "Apartamento",
        ["ESTADO"] = "Arrendada",
        ["AGRUPACION"] = "1",
        ["COEFICIENTE"] = "1.25",
        ["MODULO CONTRIBUTIVO 1"] = "1.1",
        ["MODULO CONTRIBUTIVO 2"] = "2.2",
        ["MODULO CONTRIBUTIVO 3"] = "3.3",
        ["MODULO CONTRIBUTIVO 4"] = "4.4",
        ["MODULO CONTRIBUTIVO 5"] = "5.5",
        ["AREA"] = "77.5",
        ["PISO"] = "9",
        ["HABITACIONES"] = "3",
        ["BANOS"] = "2",
        ["PARQUEADEROS"] = "1",
        ["MATRICULA"] = "MAT-901",
        ["REF PAGO"] = "REF-901",
        ["PAGA ADMIN"] = "No",
        ["CUOTA MENSUAL"] = "350000",
        ["OBSERVACIONES"] = "Observacion de prueba",
    };

    [Fact]
    public async Task Todo_campo_de_sistema_visible_sale_en_la_plantilla_y_se_importa()
    {
        var tenantId = await SeedTenantAsync("CP Plantilla RoundTrip");
        try
        {
            // 1. El usuario activa TODOS los campos en Unidades > Configurar.
            await MarcarVisiblesAsync(tenantId, UnidadCamposSistema.Todos.Select(c => c.Clave));

            // 2. Se genera la plantilla de esa copropiedad.
            var ws = await GenerarHojaUnidadesAsync(tenantId);
            var encabezados = Encabezados(ws);

            // 3. Cada campo del catalogo tiene su columna. Sin esto el dato no se puede ni escribir.
            foreach (var campo in UnidadCamposSistema.Todos)
            {
                Assert.True(encabezados.Contains(campo.Encabezado),
                    $"El campo '{campo.Clave}' esta visible pero la plantilla no emite su columna "
                    + $"'{campo.Encabezado}'. Columnas emitidas: {string.Join(", ", encabezados)}");
                Assert.True(Valores.ContainsKey(campo.Encabezado),
                    $"Campo de sistema nuevo '{campo.Clave}': agregale un valor de prueba a este test.");
            }

            // 4. Se llena una fila con un valor por campo y se importa.
            var wb = ws.Workbook;
            LlenarFila(ws, 5, "CP Plantilla RoundTrip", Valores);
            var res = await ImportarAsync(tenantId, wb);
            Assert.Empty(res.Errores);
            Assert.Equal(1, res.Unidades);

            // 5. Cada valor llego a la unidad. Se lee de la BD, no del DTO de respuesta.
            var u = await GetUnidadAsync(tenantId, "T1-901");
            Assert.Equal(TipoUnidad.Apartamento, u.Tipo);
            Assert.Equal("Arrendada", u.Estado);
            Assert.Equal(1.25m, u.CoeficientePropiedad);
            Assert.Equal(1.1m, u.ModuloContributivo1);
            Assert.Equal(2.2m, u.ModuloContributivo2);
            Assert.Equal(3.3m, u.ModuloContributivo3);
            Assert.Equal(4.4m, u.ModuloContributivo4);
            Assert.Equal(5.5m, u.ModuloContributivo5);
            Assert.Equal(77.5m, u.AreaM2);
            Assert.Equal(9, u.Piso);
            Assert.Equal(3, u.Habitaciones);
            Assert.Equal(2, u.Banos);
            Assert.Equal(1, u.Parqueaderos);
            Assert.Equal("MAT-901", u.MatriculaInmobiliaria);
            Assert.Equal("REF-901", u.ReferenciaPago);
            Assert.False(u.PagaAdministracion, "PAGA ADMIN = No debe guardarse como false");
            Assert.Equal(350000m, u.CuotaMensual);
            Assert.Equal("Observacion de prueba", u.Observaciones);
        }
        finally { await CleanupTenantAsync(tenantId); }
    }

    [Fact]
    public async Task Recargar_sin_una_columna_o_con_la_celda_vacia_no_borra_lo_que_ya_tenia()
    {
        var tenantId = await SeedTenantAsync("CP Plantilla Recarga");
        try
        {
            await MarcarVisiblesAsync(tenantId, UnidadCamposSistema.Todos.Select(c => c.Clave));
            var ws1 = await GenerarHojaUnidadesAsync(tenantId);
            LlenarFila(ws1, 5, "CP Plantilla Recarga", Valores);
            var alta = await ImportarAsync(tenantId, ws1.Workbook);
            Assert.Empty(alta.Errores);

            // Segunda carga de la MISMA unidad: AREA ya no viene como columna (el usuario la oculto)
            // y PISO viene en blanco. Ninguna de las dos debe borrar el dato cargado antes.
            var ws2 = await GenerarHojaUnidadesAsync(tenantId);
            LlenarFila(ws2, 5, "CP Plantilla Recarga", Valores);
            ws2.Cell(5, ColumnaDe(ws2, "COEFICIENTE")).Value = "2.50";   // esto SI cambia
            ws2.Cell(5, ColumnaDe(ws2, "PISO")).Value = "";              // vacia -> conserva
            ws2.Column(ColumnaDe(ws2, "AREA")).Delete();                 // ausente -> conserva
            var recarga = await ImportarAsync(tenantId, ws2.Workbook);
            Assert.Empty(recarga.Errores);
            Assert.Equal(1, recarga.UnidadesActualizadas);

            var u = await GetUnidadAsync(tenantId, "T1-901");
            Assert.Equal(2.50m, u.CoeficientePropiedad);   // lo que si venia, se actualiza
            Assert.Equal(77.5m, u.AreaM2);                 // columna ausente: intacta
            Assert.Equal(9, u.Piso);                       // celda vacia: intacta
        }
        finally { await CleanupTenantAsync(tenantId); }
    }

    // ===================== helpers =====================

    private async Task<IXLWorksheet> GenerarHojaUnidadesAsync(Guid tenantId)
    {
        var (db, tenantCtx) = BuildDb(tenantId);
        var svc = new UnidadesPlantillaService(db, tenantCtx, new HttpContextAccessor());
        var (bytes, _) = await svc.GenerarPlantillaCargaAsync(CancellationToken.None);
        // El workbook se queda abierto a proposito: el test escribe sobre el y lo vuelve a guardar.
        var wb = new XLWorkbook(new MemoryStream(bytes));
        return wb.Worksheet("UNIDADES PRIVADAS");
    }

    private async Task<ResultadoCargaUnidades> ImportarAsync(Guid tenantId, XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var (db, tenantCtx) = BuildDb(tenantId);
        var http = new HttpContextAccessor();
        var blob = new NoopBlobStorage();
        var dir = new Propia.Infrastructure.Directorio.DirectorioService(db, tenantCtx, blob);
        var mi = new MiCopropiedadService(db, tenantCtx, blob, new StubSeedUsuarioRolService(), dir);
        var porteria = new Propia.Infrastructure.Porteria.PorteriaService(db, tenantCtx, http, new FakeNotificacionDispatcher());
        var svc = new UnidadesCargaImportService(db, tenantCtx, http, mi, porteria, dir);
        return await svc.ImportarAsync(ms, CancellationToken.None);
    }

    private static HashSet<string> Encabezados(IXLWorksheet ws)
    {
        var res = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in ws.Row(2).CellsUsed()) res.Add(c.GetString().Trim());
        return res;
    }

    private static int ColumnaDe(IXLWorksheet ws, string encabezado)
    {
        foreach (var c in ws.Row(2).CellsUsed())
            if (string.Equals(c.GetString().Trim(), encabezado, StringComparison.OrdinalIgnoreCase))
                return c.Address.ColumnNumber;
        throw new Xunit.Sdk.XunitException($"La plantilla no tiene la columna '{encabezado}'.");
    }

    private static void LlenarFila(IXLWorksheet ws, int fila, string copropiedad, Dictionary<string, string> valores)
    {
        ws.Cell(fila, ColumnaDe(ws, "COPROPIEDAD")).Value = copropiedad;
        foreach (var c in ws.Row(2).CellsUsed())
        {
            var h = c.GetString().Trim();
            if (valores.TryGetValue(h, out var v))
                ws.Cell(fila, c.Address.ColumnNumber).Value = v;
        }
    }

    // Deja los campos indicados VISIBLES en unidad_campos_config, como haria el panel de Configurar.
    private async Task MarcarVisiblesAsync(Guid tenantId, IEnumerable<string> claves)
    {
        await using var ctx = OwnerCtx();
        foreach (var clave in claves)
        {
            ctx.UnidadCamposConfig.Add(new UnidadCampoConfig
            {
                TenantId = tenantId,
                Entidad = "unidad",
                CampoClave = clave,
                Oculto = false
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task<UnidadPrivada> GetUnidadAsync(Guid tenantId, string numero)
    {
        await using var ctx = OwnerCtx();
        return await ctx.UnidadesPrivadas.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(u => u.TenantId == tenantId && u.Numero == numero);
    }

    private (PropiaDbContext db, TenantContext ctx) BuildDb(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return (new PropiaDbContext(options, tenantCtx), tenantCtx);
    }

    private PropiaDbContext OwnerCtx()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options,
               new TenantContext());

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        await using var ctx = OwnerCtx();
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanupTenantAsync(Guid tenantId)
    {
        await using var ctx = OwnerCtx();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_campos_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_coeficientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tipos_coeficiente WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private sealed class NoopBlobStorage : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
            => Task.FromResult($"/mem/{key}");
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
