using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Directorio;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Carga masiva por plantilla .xlsx (Zonas, Equipos, Directorio). Reusa IMiCopropiedadService /
/// IDirectorioService para conservar RLS, validacion y bitacora. Las columnas se mapean por POSICION
/// (documentado en la hoja Instrucciones). Los campos dinamicos (EAV) de zonas/equipos se agregan como
/// columnas extra al final. La carga hace UPSERT (crea o actualiza) y no aborta ante un error de fila.
/// Espejo de DistribucionImportService.
/// </summary>
public class PlantillasService : IPlantillasService
{
    private readonly IMiCopropiedadService _mc;
    private readonly IDirectorioService _dir;
    private readonly PropiaDbContext _db;

    public PlantillasService(IMiCopropiedadService mc, IDirectorioService dir, PropiaDbContext db)
    {
        _mc = mc; _dir = dir; _db = db;
    }

    // Paleta PROPIA (consistente con la plantilla de unidades y la app).
    private static readonly string HeaderColor = "#6D4FE3";   // brand (morado)
    private static readonly XLColor Ink = XLColor.FromHtml("#1B2A3A");
    private static readonly XLColor Brand = XLColor.FromHtml("#6D4FE3");
    private static readonly XLColor BrandText = XLColor.FromHtml("#4B2BB0");

    // =====================================================================================
    // ZONAS COMUNES
    // =====================================================================================
    private static readonly string[] ZonasFijas =
    {
        "Nombre *", "Categoria", "Reservable (Si/No)", "Aforo (personas)", "Estado",
        "Descripcion", "Tarifa reserva", "Reglas de uso",
    };

    public async Task<byte[]> GenerarPlantillaZonasAsync(CancellationToken ct)
    {
        var dyn = await _db.ZonaCamposPersonalizados.Select(c => c.Label)
            .Distinct().OrderBy(l => l).ToListAsync(ct);
        var headers = ZonasFijas.Concat(dyn).ToArray();

        using var wb = new XLWorkbook();
        Instrucciones(wb, "PLANTILLA DE ZONAS COMUNES - PROPIA", new[]
        {
            "Con esta plantilla cargas de una sola vez las ZONAS COMUNES de la copropiedad.",
            "Cada fila es una zona. Si una zona con el mismo Nombre ya existe, se ACTUALIZA; si no, se crea (upsert).",
            "",
            "REGLAS:",
            "- No cambies el ORDEN ni borres las columnas de encabezado; se leen por posicion.",
            "- 'Nombre *' es obligatorio. 'Reservable' escribe Si o No.",
            "- 'Categoria' y 'Estado' deben ser un valor valido (ver hoja 'Catalogos').",
            "- Numeros: acepta coma o punto (ej. 50000 o 50.000). No uses separador de miles con coma.",
            "- Las ultimas columnas son tus CAMPOS DINAMICOS (personalizados) de zonas: su valor se guarda por zona.",
            "  Puedes agregar mas columnas de campos dinamicos al final; el encabezado es el nombre del campo.",
        });
        var ws = wb.AddWorksheet("Zonas");
        EscribirEncabezado(ws, headers);
        // Fila de ejemplo.
        object[] ej = { "Salon Social", "Social", "Si", 80, "Activa", "Salon para eventos", 50000, "Reservar con 3 dias" };
        for (int i = 0; i < ej.Length; i++) ws.Cell(2, i + 1).Value = XLCellValue.FromObject(ej[i]);
        AnchosYFreeze(ws, headers.Length);
        Catalogos(wb, ("Categoria (zonas)", Enum.GetNames<CategoriaZonaComun>()), ("Estado (zonas)", Enum.GetNames<EstadoZonaComunMantenimiento>()), ("Reservable", new[] { "Si", "No" }));
        return Bytes(wb);
    }

    public async Task<ImportarPlantillaResultado> ImportarZonasAsync(Stream xlsx, CancellationToken ct)
    {
        var errores = new List<ImportarErrorFila>();
        int creados = 0, actualizados = 0, campos = 0;
        using var wb = new XLWorkbook(xlsx);
        if (!wb.TryGetWorksheet("Zonas", out var ws))
            return new(0, 0, 0, 1, new[] { new ImportarErrorFila("Zonas", 0, "No se encontro la hoja 'Zonas'.") });

        var existentes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in await _mc.ListZonasComunesAsync(ct)) existentes[z.Nombre.Trim()] = z.Id;

        var dynLabels = LeerEncabezadosDinamicos(ws, ZonasFijas.Length);

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var nombre = Str(row.Cell(1));
            if (nombre is null) continue;
            try
            {
                var categoria = ParseEnum<CategoriaZonaComun>(Str(row.Cell(2))) ?? CategoriaZonaComun.Social;
                var reservable = Bool(row.Cell(3), false);
                var aforo = Int(row.Cell(4));
                var estado = ParseEnum<EstadoZonaComunMantenimiento>(Str(row.Cell(5))) ?? EstadoZonaComunMantenimiento.Activa;
                var descripcion = Str(row.Cell(6));
                var tarifa = Dec(row.Cell(7));
                var reglas = Str(row.Cell(8));

                Guid zonaId;
                if (existentes.TryGetValue(nombre, out var idExist))
                {
                    zonaId = idExist;
                    await _mc.ActualizarZonaComunAsync(zonaId, new ActualizarZonaComunRequest(nombre, categoria, descripcion, tarifa, reglas, estado), ct);
                    // Reservable/aforo van por la ficha; conservamos la config de mantenimiento actual.
                    var ficha = await _mc.GetZonaFichaAsync(zonaId, null, ct);
                    if (ficha is not null)
                        await _mc.GuardarZonaFichaAsync(zonaId, new GuardarZonaFichaRequest(
                            ficha.ImagenUrl, ficha.MantenimientoTipo, ficha.MantenimientoContrato,
                            ficha.MantenimientoFrecuencia, ficha.MantenimientoDiaMes, reservable, aforo), ct);
                    actualizados++;
                }
                else
                {
                    var dto = await _mc.CrearZonaComunAsync(new CrearZonaComunRequest(nombre, categoria, descripcion, reservable, tarifa, aforo, null, reglas), ct);
                    zonaId = dto.Id;
                    existentes[nombre] = zonaId;
                    if (estado != EstadoZonaComunMantenimiento.Activa)
                        await _mc.CambiarEstadoZonaAsync(zonaId, new CambiarEstadoZonaRequest(estado), ct);
                    creados++;
                }

                campos += await UpsertCamposZonaAsync(zonaId, dynLabels, row, ct);
            }
            catch (Exception ex) { errores.Add(new("Zonas", row.RowNumber(), Msg(ex))); }
        }
        return new(creados, actualizados, campos, errores.Count, errores);
    }

    private async Task<int> UpsertCamposZonaAsync(Guid zonaId, List<(int col, string label)> dyn, IXLRow row, CancellationToken ct)
    {
        if (dyn.Count == 0) return 0;
        int n = 0;
        var actuales = await _db.ZonaCamposPersonalizados.Where(c => c.ZonaComunId == zonaId).ToListAsync(ct);
        foreach (var (col, label) in dyn)
        {
            var valor = Str(row.Cell(col));
            if (valor is null) continue;
            var ex = actuales.FirstOrDefault(c => string.Equals(c.Label, label, StringComparison.OrdinalIgnoreCase));
            if (ex is not null) ex.Valor = valor;
            else _db.ZonaCamposPersonalizados.Add(new ZonaCampoPersonalizado { ZonaComunId = zonaId, Label = label, Valor = valor });
            n++;
        }
        if (n > 0) await _db.SaveChangesAsync(ct);
        return n;
    }

    // =====================================================================================
    // EQUIPOS Y ACTIVOS
    // =====================================================================================
    private static readonly string[] EquiposFijas =
    {
        "Nombre *", "Categoria", "Tipo (Equipo/Activo)", "Cantidad", "Reservable (Si/No)",
        "Modelo", "Numero de serie", "Ubicacion", "Estado", "Observaciones",
        "Vida util (anios)", "Valor adquisicion", "Proveedor", "Numero factura",
    };

    public async Task<byte[]> GenerarPlantillaEquiposAsync(CancellationToken ct)
    {
        var dyn = await _db.EquipoCamposPersonalizados.Select(c => c.Label)
            .Distinct().OrderBy(l => l).ToListAsync(ct);
        var headers = EquiposFijas.Concat(dyn).ToArray();

        using var wb = new XLWorkbook();
        Instrucciones(wb, "PLANTILLA DE EQUIPOS Y ACTIVOS - PROPIA", new[]
        {
            "Con esta plantilla cargas de una sola vez los EQUIPOS y ACTIVOS de la copropiedad.",
            "Cada fila es un equipo/activo. Si ya existe uno con el mismo Nombre, se ACTUALIZA; si no, se crea (upsert).",
            "",
            "REGLAS:",
            "- No cambies el ORDEN ni borres las columnas de encabezado; se leen por posicion.",
            "- 'Nombre *' es obligatorio. 'Tipo' = Equipo o Activo. 'Reservable' = Si o No.",
            "- 'Categoria' y 'Estado' deben ser un valor valido (ver hoja 'Catalogos').",
            "- 'Cantidad' aplica solo a Activo (para Equipo se fuerza a 1).",
            "- Las ultimas columnas son tus CAMPOS DINAMICOS (personalizados) de equipos.",
        });
        var ws = wb.AddWorksheet("Equipos");
        EscribirEncabezado(ws, headers);
        object[] ej = { "Bomba de agua principal", "Bombeo", "Equipo", 1, "No", "BX-200", "SER-123", "Cuarto de bombas", "Operativo", "Revision mensual", 10, 5000000, "HidroServicios", "FAC-001" };
        for (int i = 0; i < ej.Length; i++) ws.Cell(2, i + 1).Value = XLCellValue.FromObject(ej[i]);
        AnchosYFreeze(ws, headers.Length);
        Catalogos(wb, ("Categoria (equipos)", Enum.GetNames<CategoriaEquipo>()), ("Tipo", Enum.GetNames<TipoElemento>()), ("Estado (equipos)", Enum.GetNames<EstadoEquipoActivo>()), ("Reservable", new[] { "Si", "No" }));
        return Bytes(wb);
    }

    public async Task<ImportarPlantillaResultado> ImportarEquiposAsync(Stream xlsx, CancellationToken ct)
    {
        var errores = new List<ImportarErrorFila>();
        int creados = 0, actualizados = 0, campos = 0;
        using var wb = new XLWorkbook(xlsx);
        if (!wb.TryGetWorksheet("Equipos", out var ws))
            return new(0, 0, 0, 1, new[] { new ImportarErrorFila("Equipos", 0, "No se encontro la hoja 'Equipos'.") });

        var existentes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in await _mc.ListEquiposAsync(ct)) existentes[e.Nombre.Trim()] = e.Id;

        var dynLabels = LeerEncabezadosDinamicos(ws, EquiposFijas.Length);

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var nombre = Str(row.Cell(1));
            if (nombre is null) continue;
            try
            {
                var categoria = ParseEnum<CategoriaEquipo>(Str(row.Cell(2))) ?? CategoriaEquipo.Otros;
                var tipo = ParseEnum<TipoElemento>(Str(row.Cell(3))) ?? TipoElemento.Equipo;
                var cantidad = Int(row.Cell(4)) ?? 1;
                if (tipo == TipoElemento.Equipo) cantidad = 1;
                var reservable = Bool(row.Cell(5), false);
                var modelo = Str(row.Cell(6));
                var serie = Str(row.Cell(7));
                var ubicacion = Str(row.Cell(8));
                var estado = ParseEnum<EstadoEquipoActivo>(Str(row.Cell(9))) ?? EstadoEquipoActivo.Operativo;
                var observaciones = Str(row.Cell(10));
                var vidaUtil = Int(row.Cell(11));
                var valorAdq = Dec(row.Cell(12));
                var proveedor = Str(row.Cell(13));
                var numFactura = Str(row.Cell(14));

                Guid equipoId;
                if (existentes.TryGetValue(nombre, out var idExist)) { equipoId = idExist; actualizados++; }
                else
                {
                    var dto = await _mc.CrearEquipoAsync(new CrearEquipoActivoRequest(nombre, categoria, tipo, cantidad, reservable), ct);
                    equipoId = dto.Id;
                    existentes[nombre] = equipoId;
                    creados++;
                }
                // Ficha tecnica completa (crear o actualizar).
                await _mc.ActualizarEquipoAsync(equipoId, new ActualizarEquipoActivoRequest(
                    nombre, categoria, tipo, cantidad, reservable, modelo, serie, null, null,
                    ubicacion, observaciones, vidaUtil, null, valorAdq, proveedor, numFactura), ct);
                if (estado != EstadoEquipoActivo.Operativo)
                    await _mc.CambiarEstadoEquipoAsync(equipoId, new CambiarEstadoEquipoRequest(estado), ct);

                campos += await UpsertCamposEquipoAsync(equipoId, dynLabels, row, ct);
            }
            catch (Exception ex) { errores.Add(new("Equipos", row.RowNumber(), Msg(ex))); }
        }
        return new(creados, actualizados, campos, errores.Count, errores);
    }

    private async Task<int> UpsertCamposEquipoAsync(Guid equipoId, List<(int col, string label)> dyn, IXLRow row, CancellationToken ct)
    {
        if (dyn.Count == 0) return 0;
        int n = 0;
        var actuales = await _db.EquipoCamposPersonalizados.Where(c => c.EquipoActivoId == equipoId).ToListAsync(ct);
        foreach (var (col, label) in dyn)
        {
            var valor = Str(row.Cell(col));
            if (valor is null) continue;
            var ex = actuales.FirstOrDefault(c => string.Equals(c.Label, label, StringComparison.OrdinalIgnoreCase));
            if (ex is not null) ex.Valor = valor;
            else _db.EquipoCamposPersonalizados.Add(new EquipoCampoPersonalizado { EquipoActivoId = equipoId, Label = label, Valor = valor });
            n++;
        }
        if (n > 0) await _db.SaveChangesAsync(ct);
        return n;
    }

    // =====================================================================================
    // DIRECTORIO (Personas)
    // =====================================================================================
    private static readonly string[] DirectorioFijas =
    {
        "Tipo documento *", "Documento *", "Nombres *", "Apellidos *", "Email", "Telefono", "Genero",
    };

    public Task<byte[]> GenerarPlantillaDirectorioAsync(CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        Instrucciones(wb, "PLANTILLA DE DIRECTORIO (PERSONAS) - PROPIA", new[]
        {
            "Con esta plantilla cargas PERSONAS al Directorio y quedan vinculadas a esta copropiedad.",
            "Cada fila es una persona. La llave es Tipo documento + Documento: si ya existe, se ACTUALIZA; si no, se crea (upsert).",
            "",
            "REGLAS:",
            "- No cambies el ORDEN ni borres las columnas de encabezado; se leen por posicion.",
            "- Obligatorios: Tipo documento, Documento, Nombres, Apellidos.",
            "- 'Tipo documento' y 'Genero' deben ser un valor valido (ver hoja 'Catalogos').",
            "- Las empresas (personas juridicas) se cargan aparte desde el Directorio.",
        });
        var ws = wb.AddWorksheet("Personas");
        EscribirEncabezado(ws, DirectorioFijas);
        object[] ej = { "CC", "1090111", "Alex", "Cuartas", "alex@demo.com", "3001112233", "Masculino" };
        for (int i = 0; i < ej.Length; i++) ws.Cell(2, i + 1).Value = XLCellValue.FromObject(ej[i]);
        AnchosYFreeze(ws, DirectorioFijas.Length);
        Catalogos(wb, ("Tipo documento", Enum.GetNames<TipoDocumento>()), ("Genero", Enum.GetNames<GeneroPersona>()));
        return Task.FromResult(Bytes(wb));
    }

    public async Task<ImportarPlantillaResultado> ImportarDirectorioAsync(Stream xlsx, CancellationToken ct)
    {
        var errores = new List<ImportarErrorFila>();
        int creados = 0, actualizados = 0;
        using var wb = new XLWorkbook(xlsx);
        if (!wb.TryGetWorksheet("Personas", out var ws))
            return new(0, 0, 0, 1, new[] { new ImportarErrorFila("Personas", 0, "No se encontro la hoja 'Personas'.") });

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var doc = Str(row.Cell(2));
            var nombres = Str(row.Cell(3));
            var apellidos = Str(row.Cell(4));
            if (doc is null || nombres is null || apellidos is null) continue;
            try
            {
                var tipoDoc = ParseEnum<TipoDocumento>(Str(row.Cell(1))) ?? TipoDocumento.CC;
                var email = Str(row.Cell(5));
                var telefono = Str(row.Cell(6));
                var genero = ParseEnum<GeneroPersona>(Str(row.Cell(7)));

                var existe = await _dir.BuscarPersonaPorDocumentoAsync(new BuscarPorDocumentoRequest(tipoDoc, doc), ct);
                if (existe is not null)
                {
                    await _dir.ActualizarPersonaAsync(existe.Id, new ActualizarPersonaRequest(
                        nombres, apellidos, email, telefono, existe.FotoUrl, existe.FechaNacimiento, genero ?? existe.Genero), ct);
                    actualizados++;
                }
                else
                {
                    await _dir.CrearPersonaAsync(new CrearPersonaRequest(tipoDoc, doc, nombres, apellidos, email, telefono, null, genero), ct);
                    creados++;
                }
            }
            catch (Exception ex) { errores.Add(new("Personas", row.RowNumber(), Msg(ex))); }
        }
        return new(creados, actualizados, 0, errores.Count, errores);
    }

    // =====================================================================================
    // HELPERS
    // =====================================================================================
    private static byte[] Bytes(XLWorkbook wb) { using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray(); }

    private static void EscribirEncabezado(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = Ink;                 // slate PROPIA
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            c.Style.Border.BottomBorderColor = Brand;           // acento morado
        }
        ws.Row(1).Height = 22;
    }

    private static void AnchosYFreeze(IXLWorksheet ws, int cols)
    {
        // Ancho fijo (NO AdjustToContents: lento en servidor headless).
        for (int col = 1; col <= cols; col++) ws.Column(col).Width = 18;
        ws.SheetView.FreezeRows(1);
    }

    private static void Instrucciones(XLWorkbook wb, string titulo, string[] lineas)
    {
        var ws = wb.AddWorksheet("Instrucciones");
        ws.Column(1).Width = 118;
        var c0 = ws.Cell(1, 1); c0.Value = "PROPIA   |   " + titulo;
        c0.Style.Font.Bold = true; c0.Style.Font.FontSize = 14; c0.Style.Font.FontColor = XLColor.White;
        c0.Style.Fill.BackgroundColor = Brand;
        c0.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        c0.Style.Alignment.Indent = 1;
        ws.Row(1).Height = 26;
        int r = 3;
        foreach (var l in lineas)
        {
            var c = ws.Cell(r++, 1); c.Value = l;
            if (l.EndsWith(":")) c.Style.Font.Bold = true;
        }
    }

    private static void Catalogos(XLWorkbook wb, params (string titulo, string[] valores)[] cols)
    {
        var ws = wb.AddWorksheet("Catalogos");
        int colIdx = 1;
        foreach (var (titulo, valores) in cols)
        {
            var h = ws.Cell(1, colIdx); h.Value = titulo; h.Style.Font.Bold = true;
            h.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1ECFD"); h.Style.Font.FontColor = BrandText;
            int r = 2;
            foreach (var v in valores) ws.Cell(r++, colIdx).Value = v;
            ws.Column(colIdx).Width = 24;
            colIdx += 2;
        }
    }

    // Lee los encabezados de las columnas dinamicas (a partir de la columna fija+1). Devuelve (colIndex1based, label).
    private static List<(int col, string label)> LeerEncabezadosDinamicos(IXLWorksheet ws, int fijas)
    {
        var res = new List<(int, string)>();
        int col = fijas + 1;
        while (true)
        {
            var label = Str(ws.Cell(1, col));
            if (label is null) break;
            res.Add((col, label));
            col++;
        }
        return res;
    }

    private static T? ParseEnum<T>(string? s) where T : struct, Enum
        => string.IsNullOrWhiteSpace(s) ? null
           : (Enum.TryParse<T>(s.Trim(), true, out var v) && Enum.IsDefined(v) ? v : (T?)null);

    private static string? Str(IXLCell c) { var v = c.GetString()?.Trim(); return string.IsNullOrEmpty(v) ? null : v; }

    private static int? Int(IXLCell c) { var d = Dec(c); return d is null ? null : (int)Math.Round(d.Value); }

    private static decimal? Dec(IXLCell c) => Dec(Str(c));

    private static decimal? Dec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Replace("$", "").Replace("%", "").Replace(" ", "");
        if (s.Contains('.') && s.Contains(',')) s = s.Replace(".", "").Replace(",", ".");
        else s = s.Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static bool Bool(IXLCell c, bool def)
    {
        var s = Str(c)?.ToLowerInvariant();
        if (s is null) return def;
        return s is not ("no" or "false" or "0" or "n");
    }

    private static string Msg(Exception ex) => ex.Message;
}
