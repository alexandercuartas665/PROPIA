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
/// Homogenizacion (Fase 1, decision de Alex 2026-09-14): las hojas PERSONAS/VEHICULOS/MASCOTAS de la
/// plantilla se generan config-driven desde su catalogo (&lt;Entidad&gt;CamposSistema), IGUAL que UNIDADES
/// (encabezado canonico + alias del tenant en la ayuda, orden de catalogo, campos propios [Label]).
/// El importador es ALIAS-AWARE: acepta el encabezado canonico O el alias del tenant.
///
/// Cubre: round-trip por hoja (marca/modelo/color de vehiculo; sexo/fecha nacimiento a la persona GLOBAL;
/// tipo/raza/nombre de mascota), la guarda de MERGE (celda vacia no pisa la persona global compartida),
/// la guarda de COLISION (un alias que choca con el canonico de otro campo no lo secuestra), el import por
/// alias, y que ocultar un campo lo saca de la plantilla.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PersonasVehiculosMascotasPlantillaTests
{
    private readonly PostgresFixture _fx;
    public PersonasVehiculosMascotasPlantillaTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Vehiculos_marca_modelo_color_salen_en_la_plantilla_y_se_importan()
    {
        var tenantId = await SeedTenantAsync("CP Veh RoundTrip");
        try
        {
            var wb = await GenerarAsync(tenantId);
            var veh = wb.Worksheet("VEHICULOS");
            var enc = Encabezados(veh);
            foreach (var h in new[] { "PLACA", "TIPO DE VEHICULO", "MARCA", "MODELO", "COLOR" })
                Assert.Contains(h, enc);

            LlenarFila(wb.Worksheet("UNIDADES PRIVADAS"), 5, "CP Veh RoundTrip",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-100", ["COEFICIENTE"] = "1.00" });
            LlenarFila(veh, 5, "CP Veh RoundTrip", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-100", ["PLACA"] = "XYZ987", ["TIPO DE VEHICULO"] = "Camioneta",
                ["MARCA"] = "Toyota", ["MODELO"] = "2020", ["COLOR"] = "Rojo",
            });
            var res = await ImportarAsync(tenantId, wb);
            Assert.Empty(res.Errores);

            var v = await GetVehiculoAsync(tenantId, "XYZ987");
            Assert.Equal(TipoVehiculo.Camioneta, v.Tipo);
            Assert.Equal("Toyota", v.Marca);
            Assert.Equal("2020", v.Modelo);
            Assert.Equal("Rojo", v.Color);
        }
        finally { await CleanupAsync(tenantId, "XYZ987", null); }
    }

    [Fact]
    public async Task Personas_sexo_y_fecha_nacimiento_se_importan_a_la_persona_global()
    {
        var tenantId = await SeedTenantAsync("CP Per RoundTrip");
        var doc = "PER" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var wb = await GenerarAsync(tenantId);
            var per = wb.Worksheet("PERSONAS");
            var enc = Encabezados(per);
            foreach (var h in new[] { "TIPO RESIDENTE", "TIPO ID", "NOMBRE", "IDENTIFICACION", "EMAIL", "TELEFONO", "SEXO", "FECHA NACIMIENTO" })
                Assert.Contains(h, enc);
            // Homogenizacion: PROFESION y ROLL quedan EXCLUIDAS.
            Assert.DoesNotContain("PROFESION", enc);
            Assert.DoesNotContain("ROLL", enc);

            LlenarFila(wb.Worksheet("UNIDADES PRIVADAS"), 5, "CP Per RoundTrip",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-101", ["COEFICIENTE"] = "1.00" });
            LlenarFila(per, 5, "CP Per RoundTrip", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-101", ["TIPO RESIDENTE"] = "Propietario", ["TIPO ID"] = "CC",
                ["NOMBRE"] = "Ana Ruiz", ["IDENTIFICACION"] = doc, ["SEXO"] = "F", ["FECHA NACIMIENTO"] = "1990-05-20",
            });
            var res = await ImportarAsync(tenantId, wb);
            Assert.Empty(res.Errores);

            var p = await GetPersonaAsync(doc);
            Assert.Equal(GeneroPersona.Femenino, p.Genero);
            Assert.Equal(new DateOnly(1990, 5, 20), p.FechaNacimiento);
        }
        finally { await CleanupAsync(tenantId, null, doc); }
    }

    [Fact]
    public async Task Recargar_persona_con_sexo_vacio_no_borra_el_genero_de_la_persona_global()
    {
        var tenantId = await SeedTenantAsync("CP Per Merge");
        var doc = "PER" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // 1) Alta con SEXO = M.
            var wb1 = await GenerarAsync(tenantId);
            LlenarFila(wb1.Worksheet("UNIDADES PRIVADAS"), 5, "CP Per Merge",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-102", ["COEFICIENTE"] = "1.00" });
            LlenarFila(wb1.Worksheet("PERSONAS"), 5, "CP Per Merge", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-102", ["TIPO RESIDENTE"] = "Propietario", ["TIPO ID"] = "CC",
                ["NOMBRE"] = "Beto Diaz", ["IDENTIFICACION"] = doc, ["SEXO"] = "M",
            });
            Assert.Empty((await ImportarAsync(tenantId, wb1)).Errores);
            Assert.Equal(GeneroPersona.Masculino, (await GetPersonaAsync(doc)).Genero);

            // 2) Recarga de la MISMA persona con SEXO en blanco (con reemplazo del vinculo, que borra
            //    unidad_personas y lo repone; la Persona GLOBAL persiste). El SEXO en blanco NO debe borrar
            //    el genero que ya tenia la persona global.
            var wb2 = await GenerarAsync(tenantId);
            LlenarFila(wb2.Worksheet("UNIDADES PRIVADAS"), 5, "CP Per Merge",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-102", ["COEFICIENTE"] = "1.00" });
            LlenarFila(wb2.Worksheet("PERSONAS"), 5, "CP Per Merge", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-102", ["TIPO RESIDENTE"] = "Propietario", ["TIPO ID"] = "CC",
                ["NOMBRE"] = "Beto Diaz", ["IDENTIFICACION"] = doc,   // SEXO ausente
            });
            Assert.Empty((await ImportarAsync(tenantId, wb2, reemplazar: true)).Errores);
            Assert.Equal(GeneroPersona.Masculino, (await GetPersonaAsync(doc)).Genero);   // intacto
        }
        finally { await CleanupAsync(tenantId, null, doc); }
    }

    [Fact]
    public async Task Import_por_alias_del_tenant_resuelve_al_canonico()
    {
        var tenantId = await SeedTenantAsync("CP Veh Alias");
        try
        {
            // La copropiedad renombro COLOR -> "TONO".
            await RenombrarAsync(tenantId, "vehiculos", "color", "Tono");

            // La plantilla generada mantiene el encabezado CANONICO (COLOR) y pone el alias en la AYUDA.
            var wb = await GenerarAsync(tenantId);
            var veh = wb.Worksheet("VEHICULOS");
            Assert.Contains("COLOR", Encabezados(veh));
            Assert.Contains("Tono", veh.Cell(3, ColumnaDe(veh, "COLOR")).GetString());

            // Un archivo del usuario trae la columna con SU nombre ("TONO") en vez del canonico.
            veh.Cell(2, ColumnaDe(veh, "COLOR")).Value = "TONO";
            LlenarFila(wb.Worksheet("UNIDADES PRIVADAS"), 5, "CP Veh Alias",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-103", ["COEFICIENTE"] = "1.00" });
            LlenarFila(veh, 5, "CP Veh Alias", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-103", ["PLACA"] = "AAA111", ["TIPO DE VEHICULO"] = "Automovil",
                ["TONO"] = "Verde",
            });
            Assert.Empty((await ImportarAsync(tenantId, wb)).Errores);
            Assert.Equal("Verde", (await GetVehiculoAsync(tenantId, "AAA111")).Color);
        }
        finally { await CleanupAsync(tenantId, "AAA111", null); }
    }

    [Fact]
    public async Task Alias_que_choca_con_el_canonico_de_otro_campo_no_lo_secuestra()
    {
        var tenantId = await SeedTenantAsync("CP Veh Colision");
        try
        {
            // Alias malicioso/torpe: la copropiedad renombro COLOR como "MARCA" (que es el canonico de OTRO
            // campo). La guarda debe hacer que gane el canonico: una columna MARCA llena MARCA, no COLOR.
            await RenombrarAsync(tenantId, "vehiculos", "color", "Marca");

            var wb = await GenerarAsync(tenantId);
            var veh = wb.Worksheet("VEHICULOS");
            LlenarFila(wb.Worksheet("UNIDADES PRIVADAS"), 5, "CP Veh Colision",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-104", ["COEFICIENTE"] = "1.00" });
            LlenarFila(veh, 5, "CP Veh Colision", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-104", ["PLACA"] = "BBB222", ["TIPO DE VEHICULO"] = "Automovil",
                ["MARCA"] = "Renault",   // debe ir a MARCA (canonico), no a COLOR (alias en colision)
            });
            Assert.Empty((await ImportarAsync(tenantId, wb)).Errores);
            var v = await GetVehiculoAsync(tenantId, "BBB222");
            Assert.Equal("Renault", v.Marca);
            Assert.True(string.IsNullOrEmpty(v.Color), "el alias en colision no debe secuestrar el valor canonico");
        }
        finally { await CleanupAsync(tenantId, "BBB222", null); }
    }

    [Fact]
    public async Task Mascotas_tipo_raza_nombre_round_trip()
    {
        var tenantId = await SeedTenantAsync("CP Mas RoundTrip");
        try
        {
            var wb = await GenerarAsync(tenantId);
            var mas = wb.Worksheet("MASCOTAS");
            foreach (var h in new[] { "NOMBRE", "TIPO MASCOTA", "RAZA" }) Assert.Contains(h, Encabezados(mas));

            LlenarFila(wb.Worksheet("UNIDADES PRIVADAS"), 5, "CP Mas RoundTrip",
                new(StringComparer.OrdinalIgnoreCase) { ["UNIDAD PRIVADA"] = "T1-105", ["COEFICIENTE"] = "1.00" });
            LlenarFila(mas, 5, "CP Mas RoundTrip", new(StringComparer.OrdinalIgnoreCase)
            {
                ["UNIDAD PRIVADA"] = "T1-105", ["NOMBRE"] = "Firulais", ["TIPO MASCOTA"] = "Gato", ["RAZA"] = "Criollo",
            });
            Assert.Empty((await ImportarAsync(tenantId, wb)).Errores);

            var m = await GetMascotaAsync(tenantId, "Firulais");
            Assert.Equal(TipoMascota.Gato, m.Tipo);
            Assert.Equal("Criollo", m.Raza);
        }
        finally { await CleanupAsync(tenantId, null, null); }
    }

    [Fact]
    public async Task Ocultar_marca_la_saca_de_la_plantilla_de_vehiculos()
    {
        var tenantId = await SeedTenantAsync("CP Veh Ocultar");
        try
        {
            await OcultarAsync(tenantId, "vehiculos", "marca");
            var veh = (await GenerarAsync(tenantId)).Worksheet("VEHICULOS");
            var enc = Encabezados(veh);
            Assert.DoesNotContain("MARCA", enc);
            Assert.Contains("PLACA", enc);    // estructural: sigue
            Assert.Contains("MODELO", enc);   // no se oculto
        }
        finally { await CleanupAsync(tenantId, null, null); }
    }

    // ===================== helpers =====================
    private async Task<XLWorkbook> GenerarAsync(Guid tenantId)
    {
        var (db, ctx) = BuildDb(tenantId);
        var svc = new UnidadesPlantillaService(db, ctx, new HttpContextAccessor());
        var (bytes, _) = await svc.GenerarPlantillaCargaAsync(CancellationToken.None);
        return new XLWorkbook(new MemoryStream(bytes));
    }

    private async Task<ResultadoCargaUnidades> ImportarAsync(Guid tenantId, XLWorkbook wb, bool reemplazar = false)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        var (db, ctx) = BuildDb(tenantId);
        var http = new HttpContextAccessor();
        var blob = new NoopBlobStorage();
        var dir = new Propia.Infrastructure.Directorio.DirectorioService(db, ctx, blob);
        var mi = new MiCopropiedadService(db, ctx, blob, new StubSeedUsuarioRolService(), dir);
        var porteria = new Propia.Infrastructure.Porteria.PorteriaService(db, ctx, http, new FakeNotificacionDispatcher());
        var svc = new UnidadesCargaImportService(db, ctx, http, mi, porteria, dir);
        return await svc.ImportarAsync(ms, CancellationToken.None, forzarTenantActual: false, reemplazarDependientes: reemplazar);
    }

    private async Task RenombrarAsync(Guid tenantId, string entidad, string clave, string alias)
    {
        await using var ctx = OwnerCtx();
        ctx.UnidadCamposConfig.Add(new UnidadCampoConfig
        { TenantId = tenantId, Entidad = entidad, CampoClave = clave, Oculto = false, Alias = alias });
        await ctx.SaveChangesAsync();
    }

    private async Task OcultarAsync(Guid tenantId, string entidad, string clave)
    {
        await using var ctx = OwnerCtx();
        ctx.UnidadCamposConfig.Add(new UnidadCampoConfig
        { TenantId = tenantId, Entidad = entidad, CampoClave = clave, Oculto = true });
        await ctx.SaveChangesAsync();
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
        throw new Xunit.Sdk.XunitException($"La hoja no tiene la columna '{encabezado}'.");
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

    private async Task<VehiculoAutorizado> GetVehiculoAsync(Guid tenantId, string placa)
    {
        await using var ctx = OwnerCtx();
        return await ctx.VehiculosAutorizados.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(v => v.TenantId == tenantId && v.Placa == placa);
    }

    private async Task<UnidadMascota> GetMascotaAsync(Guid tenantId, string nombre)
    {
        await using var ctx = OwnerCtx();
        return await ctx.UnidadMascotas.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(m => m.TenantId == tenantId && m.Nombre == nombre);
    }

    private async Task<Persona> GetPersonaAsync(string documento)
    {
        await using var ctx = OwnerCtx();
        return await ctx.Personas.IgnoreQueryFilters().AsNoTracking().FirstAsync(p => p.Documento == documento);
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
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.SinAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task CleanupAsync(Guid tenantId, string? placa, string? documento)
    {
        await using var ctx = OwnerCtx();
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_placas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM vehiculos_autorizados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_mascotas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_personas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_campos_config WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_coeficientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tipos_coeficiente WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        if (documento is not null)
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE documento = {documento}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }

    private sealed class NoopBlobStorage : IBlobStorage
    {
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct) => Task.FromResult($"/mem/{key}");
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/mem/{key}";
        public string? ResolveUrl(string? storedValueOrKey) => storedValueOrKey;
        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
