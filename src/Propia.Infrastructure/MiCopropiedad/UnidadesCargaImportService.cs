using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Application.Porteria;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Importa la plantilla multi-hoja y carga en VARIAS copropiedades del cliente. Reusa
/// IMiCopropiedadService / IPorteriaService (validacion, RLS, bitacora). Cambia el contexto de
/// tenant por copropiedad para que cada escritura caiga en la copropiedad correcta.
/// </summary>
public sealed class UnidadesCargaImportService : IUnidadesCargaImportService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly IMiCopropiedadService _mi;
    private readonly IPorteriaService _porteria;

    public UnidadesCargaImportService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http,
        IMiCopropiedadService mi, IPorteriaService porteria)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
        _mi = mi;
        _porteria = porteria;
    }

    public async Task<ResultadoCargaUnidades> ImportarAsync(Stream contenidoXlsx, CancellationToken ct)
    {
        var errores = new List<CargaUnidadesError>();
        using var wb = new XLWorkbook(contenidoXlsx);

        var copros = await CopropiedadesDelClienteAsync(ct);   // nombreLower -> tenantId
        var tenantOriginal = _tenant.CurrentTenantId;

        var unidades = LeerHoja(wb, "UNIDADES PRIVADAS");
        var personas = LeerHoja(wb, "PERSONAS");
        var vehiculos = LeerHoja(wb, "VEHICULOS");
        var mascotas = LeerHoja(wb, "MASCOTAS");
        var zonas = LeerHoja(wb, "ZONAS COMUNES");
        var equipos = LeerHoja(wb, "EQUIPOS");

        var nombresCopro = unidades.Concat(personas).Concat(vehiculos).Concat(mascotas).Concat(zonas).Concat(equipos)
            .Select(r => Val(r.Row, "COPROPIEDAD"))
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        int nCopro = 0, nUni = 0, nAnexo = 0, nPer = 0, nVeh = 0, nMas = 0, nZon = 0, nEqu = 0;

        foreach (var nombre in nombresCopro)
        {
            if (!copros.TryGetValue(nombre.ToLowerInvariant(), out var tid))
            {
                errores.Add(new("GENERAL", 0, $"Copropiedad '{nombre}' no existe o no la administras."));
                continue;
            }
            await SetTenantSqlAsync(tid, ct);   // fija tenant en EF y en la sesion SQL (RLS)
            nCopro++;

            var numeroToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var anexosPend = new List<(int Fila, string Principal, string Asociada)>();

            // ---- Unidades ----
            foreach (var (row, fila) in unidades.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var numero = Val(row, "UNIDAD PRIVADA").Trim();
                    if (numero.Length == 0) { errores.Add(new("UNIDADES PRIVADAS", fila, "Falta UNIDAD PRIVADA")); continue; }
                    var req = new CrearUnidadRequest(
                        numero, ParseEnum(Val(row, "TIPO"), TipoUnidad.Apartamento), null, null,
                        ParseDecimal(Val(row, "COEFICIENTE")), null, null, null, null, null, null,
                        NullIfEmpty(Val(row, "MATRICULA")), true, null, NullIfEmpty(Val(row, "REF PAGO")));
                    var creada = await _mi.CrearUnidadAsync(req, ct);
                    numeroToId[numero] = creada.Id;
                    nUni++;

                    var agr = Val(row, "AGRUPACION").Trim();
                    var principal = Val(row, "PRINCIPAL").Trim();
                    if (agr.StartsWith("3") && principal.Length > 0)
                        anexosPend.Add((fila, principal, numero));
                }
                catch (Exception ex) { errores.Add(new("UNIDADES PRIVADAS", fila, Fallo(ex))); }
            }

            // ---- Anexos (2a pasada: ya existen ambas unidades) ----
            foreach (var (fila, principal, asociada) in anexosPend)
            {
                try
                {
                    var pid = numeroToId.GetValueOrDefault(principal);
                    if (pid == Guid.Empty) pid = await BuscarUnidadIdAsync(principal, ct) ?? Guid.Empty;
                    var aid = numeroToId.GetValueOrDefault(asociada);
                    if (pid == Guid.Empty || aid == Guid.Empty)
                    {
                        errores.Add(new("UNIDADES PRIVADAS", fila, $"Anexo: no se encontro la principal '{principal}'.")); continue;
                    }
                    await _mi.CrearVinculoAsync(pid, new CrearVinculoUnidadRequest(aid, false), ct);
                    nAnexo++;
                }
                catch (Exception ex) { errores.Add(new("UNIDADES PRIVADAS", fila, Fallo(ex))); }
            }

            // ---- Personas ----
            foreach (var (row, fila) in personas.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var uid = await ResolverUnidadAsync(Val(row, "UNIDAD PRIVADA"), numeroToId, ct);
                    if (uid == Guid.Empty) { errores.Add(new("PERSONAS", fila, $"Unidad '{Val(row, "UNIDAD PRIVADA")}' no encontrada")); continue; }
                    var doc = Val(row, "IDENTIFICACION").Trim();
                    if (doc.Length == 0) { errores.Add(new("PERSONAS", fila, "Falta IDENTIFICACION")); continue; }
                    var (nombres, apellidos) = SplitNombre(Val(row, "NOMBRE"));
                    var req = new AgregarPersonaUnidadRequest(
                        doc, nombres, apellidos, NullIfEmpty(Val(row, "EMAIL")), NullIfEmpty(Val(row, "TELEFONO")),
                        ParseEnum(Val(row, "TIPO RESIDENTE"), RolUnidadPersona.Propietario));
                    await _mi.AgregarPersonaUnidadAsync(uid, req, ct);
                    nPer++;
                }
                catch (Exception ex) { errores.Add(new("PERSONAS", fila, Fallo(ex))); }
            }

            // ---- Vehiculos ----
            foreach (var (row, fila) in vehiculos.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var uid = await ResolverUnidadAsync(Val(row, "UNIDAD PRIVADA"), numeroToId, ct);
                    if (uid == Guid.Empty) { errores.Add(new("VEHICULOS", fila, "Unidad no encontrada")); continue; }
                    var placa = Val(row, "PLACA").Trim();
                    if (placa.Length == 0) { errores.Add(new("VEHICULOS", fila, "Falta PLACA")); continue; }
                    await _porteria.CrearVehiculoAutorizadoAsync(new CrearVehiculoRequest(
                        uid, placa, ParseEnum(Val(row, "TIPO DE VEHICULO"), TipoVehiculo.Automovil),
                        NullIfEmpty(Val(row, "MARCA")), NullIfEmpty(Val(row, "MODELO")), NullIfEmpty(Val(row, "COLOR")), null), ct);
                    nVeh++;
                }
                catch (Exception ex) { errores.Add(new("VEHICULOS", fila, Fallo(ex))); }
            }

            // ---- Mascotas ----
            foreach (var (row, fila) in mascotas.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var uid = await ResolverUnidadAsync(Val(row, "UNIDAD PRIVADA"), numeroToId, ct);
                    if (uid == Guid.Empty) { errores.Add(new("MASCOTAS", fila, "Unidad no encontrada")); continue; }
                    var nom = NullIfEmpty(Val(row, "NOMBRE")) ?? "Mascota";
                    await _mi.AgregarMascotaUnidadAsync(uid, new CrearUnidadMascotaRequest(
                        nom, ParseEnum(Val(row, "TIPO MASCOTA"), TipoMascota.Perro), NullIfEmpty(Val(row, "RAZA"))), ct);
                    nMas++;
                }
                catch (Exception ex) { errores.Add(new("MASCOTAS", fila, Fallo(ex))); }
            }

            // ---- Zonas comunes ----
            foreach (var (row, fila) in zonas.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var nom = Val(row, "NOMBRE").Trim();
                    if (nom.Length == 0) { errores.Add(new("ZONAS COMUNES", fila, "Falta NOMBRE")); continue; }
                    var req = new CrearZonaComunRequest(
                        nom, ParseEnum(Val(row, "CATEGORIA"), CategoriaZonaComun.Otros),
                        NullIfEmpty(Val(row, "DESCRIPCION")), ParseSiNo(Val(row, "RESERVABLE")),
                        ParseDecimalNull(Val(row, "TARIFA RESERVA")), ParseIntNull(Val(row, "AFORO")),
                        null, NullIfEmpty(Val(row, "REGLAS DE USO")));
                    var creada = await _mi.CrearZonaComunAsync(req, ct);
                    var est = ParseEnum(Val(row, "ESTADO"), EstadoZonaComunMantenimiento.Activa);
                    if (est != EstadoZonaComunMantenimiento.Activa)
                        await _mi.CambiarEstadoZonaAsync(creada.Id, new CambiarEstadoZonaRequest(est), ct);
                    nZon++;
                }
                catch (Exception ex) { errores.Add(new("ZONAS COMUNES", fila, Fallo(ex))); }
            }

            // ---- Equipos y activos ----
            foreach (var (row, fila) in equipos.Where(r => Eq(Val(r.Row, "COPROPIEDAD"), nombre)))
            {
                try
                {
                    var nom = Val(row, "NOMBRE").Trim();
                    if (nom.Length == 0) { errores.Add(new("EQUIPOS", fila, "Falta NOMBRE")); continue; }
                    var cat = ParseEnum(Val(row, "CATEGORIA"), CategoriaEquipo.Otros);
                    var tipo = ParseEnum(Val(row, "TIPO"), TipoElemento.Equipo);
                    var cant = Math.Max(1, ParseIntNull(Val(row, "CANTIDAD")) ?? 1);
                    var reservable = ParseSiNo(Val(row, "RESERVABLE"));
                    var creado = await _mi.CrearEquipoAsync(new CrearEquipoActivoRequest(nom, cat, tipo, cant, reservable), ct);

                    // Ficha completa (solo si viene algun dato adicional).
                    var modelo = NullIfEmpty(Val(row, "MODELO"));
                    var serie = NullIfEmpty(Val(row, "NUMERO DE SERIE"));
                    var ubic = NullIfEmpty(Val(row, "UBICACION"));
                    var obs = NullIfEmpty(Val(row, "OBSERVACIONES"));
                    var vida = ParseIntNull(Val(row, "VIDA UTIL"));
                    var valor = ParseDecimalNull(Val(row, "VALOR ADQUISICION"));
                    var prov = NullIfEmpty(Val(row, "PROVEEDOR"));
                    var fact = NullIfEmpty(Val(row, "NUMERO FACTURA"));
                    if (modelo != null || serie != null || ubic != null || obs != null || vida != null || valor != null || prov != null || fact != null)
                        await _mi.ActualizarEquipoAsync(creado.Id, new ActualizarEquipoActivoRequest(
                            nom, cat, tipo, cant, reservable, modelo, serie, null, null, ubic, obs, vida, null, valor, prov, fact), ct);

                    var est = ParseEnum(Val(row, "ESTADO"), EstadoEquipoActivo.Operativo);
                    if (est != EstadoEquipoActivo.Operativo)
                        await _mi.CambiarEstadoEquipoAsync(creado.Id, new CambiarEstadoEquipoRequest(est), ct);
                    nEqu++;
                }
                catch (Exception ex) { errores.Add(new("EQUIPOS", fila, Fallo(ex))); }
            }
        }

        // Restaura el contexto de tenant original de la sesion.
        if (tenantOriginal is { } to) await SetTenantSqlAsync(to, ct);
        return new ResultadoCargaUnidades(nCopro, nUni, nAnexo, nPer, nVeh, nMas, nZon, nEqu, errores);
    }

    // Fija el tenant en EF (ITenantContext, para HasQueryFilter + TenantId al guardar) y en la
    // sesion SQL (app.tenant_id, para que RLS acepte los INSERT del tenant destino).
    private async Task SetTenantSqlAsync(Guid tenantId, CancellationToken ct)
    {
        _tenant.SetTenant(tenantId);
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter(); p.ParameterName = "@t"; p.Value = tenantId.ToString(); cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ===================== Helpers =====================
    private async Task<Guid> ResolverUnidadAsync(string numero, Dictionary<string, Guid> mapa, CancellationToken ct)
    {
        numero = numero.Trim();
        if (numero.Length == 0) return Guid.Empty;
        if (mapa.TryGetValue(numero, out var id)) return id;
        return await BuscarUnidadIdAsync(numero, ct) ?? Guid.Empty;
    }

    private async Task<Guid?> BuscarUnidadIdAsync(string numero, CancellationToken ct)
    {
        var u = await _db.UnidadesPrivadas.AsNoTracking()
            .Where(x => x.Numero == numero).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return u;
    }

    private static List<(Dictionary<string, string> Row, int Fila)> LeerHoja(XLWorkbook wb, string nombre)
    {
        var res = new List<(Dictionary<string, string>, int)>();
        if (!wb.TryGetWorksheet(nombre, out var ws)) return res;
        var headers = new Dictionary<int, string>();
        foreach (var cell in ws.Row(2).CellsUsed())   // fila 1 = banner, fila 2 = encabezados
            headers[cell.Address.ColumnNumber] = cell.GetString().Trim().ToUpperInvariant();
        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = 4; r <= last; r++)   // fila 3 = ayuda, datos desde la 4
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var any = false;
            foreach (var (col, h) in headers)
            {
                var v = ws.Cell(r, col).GetString().Trim();
                dict[h] = v;
                if (v.Length > 0) any = true;
            }
            if (!any) continue;
            // Ignora la fila de ejemplo de la plantilla (COPROPIEDAD = "EJEMPLO (borrar fila)").
            var cop = dict.TryGetValue("COPROPIEDAD", out var cc) ? cc.TrimStart() : "";
            if (cop.StartsWith("EJEMPLO", StringComparison.OrdinalIgnoreCase)) continue;
            res.Add((dict, r));
        }
        return res;
    }

    private static string Val(Dictionary<string, string> row, string header)
        => row.TryGetValue(header, out var v) ? v : "";

    private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static decimal ParseDecimal(string s)
    {
        s = (s ?? "").Trim().Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static decimal? ParseDecimalNull(string s)
    {
        s = (s ?? "").Trim().Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
    }

    private static int? ParseIntNull(string s)
    {
        s = (s ?? "").Trim();
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
    }

    // "Si"/"No" (o Si/1/true) -> bool. Reservable en zonas/equipos.
    private static bool ParseSiNo(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s is "si" or "sí" or "1" or "true" or "x" or "verdadero";
    }

    private static (string Nombres, string Apellidos) SplitNombre(string nombre)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length == 0) return ("Sin nombre", "");
        var parts = nombre.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }

    private static TEnum ParseEnum<TEnum>(string s, TEnum fallback) where TEnum : struct, Enum
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return fallback;
        foreach (var name in Enum.GetNames<TEnum>())
            if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return Enum.Parse<TEnum>(name);
        return fallback;
    }

    // Registra el fallo de una fila: descarta la entidad fallida del ChangeTracker para NO contaminar
    // las filas siguientes de la misma copropiedad (un SaveChanges fallido deja la entidad "pegada"),
    // y devuelve un motivo legible.
    private string Fallo(Exception ex)
    {
        try { _db.ChangeTracker.Clear(); } catch { /* best-effort */ }
        return Msg(ex);
    }

    // Traduce la excepcion a un motivo legible (recorre inner exceptions por errores de BD comunes).
    private static string Msg(Exception ex)
    {
        if (ex is InvalidOperationException) return ex.Message;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var m = e.Message ?? string.Empty;
            if (m.Contains("IX_personas_email")) return "Ya existe otra persona con ese EMAIL (debe ser unico).";
            if (m.Contains("IX_personas_documento") || m.Contains("_documento")) return "Ya existe otra persona con ese DOCUMENTO.";
            if (m.Contains("vehiculos") && m.Contains("placa")) return "Ya existe un vehiculo con esa PLACA.";
            if (m.Contains("duplicate key")) return "Registro duplicado (ya existe).";
            if (m.Contains("foreign key") || m.Contains("violates foreign key")) return "Referencia invalida (un dato relacionado no existe).";
        }
        return "No se pudo procesar: " + (ex.InnerException?.Message ?? ex.Message);
    }

    private async Task<Dictionary<string, Guid>> CopropiedadesDelClienteAsync(CancellationToken ct)
    {
        var personaId = Guid.TryParse(_http.HttpContext?.User?.FindFirst("persona_id")?.Value, out var pid) ? pid : (Guid?)null;
        var ids = new List<Guid>();
        if (personaId is not null)
        {
            var conn = _db.Database.GetDbConnection();
            var abierta = conn.State != System.Data.ConnectionState.Open;
            if (abierta) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT tenant_id FROM get_tenants_for_persona(@p)";
                var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = personaId.Value; cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
            }
            finally { if (abierta) await conn.CloseAsync(); }
        }
        if (ids.Count == 0 && _tenant.CurrentTenantId is { } curr) ids.Add(curr);

        var lista = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Nombre }).ToListAsync(ct);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in lista) map[t.Nombre.Trim().ToLowerInvariant()] = t.Id;
        return map;
    }
}
