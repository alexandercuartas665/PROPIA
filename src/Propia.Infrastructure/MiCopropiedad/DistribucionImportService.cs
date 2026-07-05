using System.Globalization;
using ClosedXML.Excel;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Genera la plantilla de Distribucion y procesa la carga masiva. Reusa IMiCopropiedadService
/// (CrearTorreAsync / CrearUnidadAsync) para que la validacion, RLS y bitacora sean identicas a la
/// creacion manual. El orden de las columnas es fijo (documentado en la hoja Instrucciones).
/// </summary>
public class DistribucionImportService : IDistribucionImportService
{
    private readonly IMiCopropiedadService _svc;
    public DistribucionImportService(IMiCopropiedadService svc) => _svc = svc;

    // Encabezados fijos (el import mapea por POSICION, no por texto).
    private static readonly string[] UnidadesHeaders =
    {
        "Numero *", "Tipo *", "Torre", "Piso", "Coeficiente (%)", "Area (m2)",
        "Habitaciones", "Banos", "Parqueaderos", "Estado", "Matricula inmobiliaria",
        "Paga administracion (Si/No)", "Cuota mensual", "Observaciones"
    };
    private static readonly string[] TorresHeaders = { "Nombre *", "Cantidad de pisos", "Descripcion" };

    // =====================================================================================
    // PLANTILLA
    // =====================================================================================
    public byte[] GenerarPlantilla()
    {
        using var wb = new XLWorkbook();

        HojaInstrucciones(wb);
        HojaUnidades(wb);
        HojaTorres(wb);
        HojaCatalogos(wb);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void HojaInstrucciones(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Instrucciones");
        ws.Column(1).Width = 118;
        var lineas = new (string txt, bool bold, bool title)[]
        {
            ("PLANTILLA DE DISTRIBUCION - PROPIA", true, true),
            ("", false, false),
            ("Con esta plantilla cargas de una sola vez las TORRES y las UNIDADES privadas de la copropiedad.", false, false),
            ("El archivo ya trae un ejemplo completo (2 torres + 6 unidades). Reemplaza esas filas por tus datos reales.", false, false),
            ("", false, false),
            ("PASOS:", true, false),
            ("1. Llena primero la hoja 'Torres' (tabla de apoyo). Cada torre se identifica por su Nombre.", false, false),
            ("2. Llena la hoja 'Unidades'. La columna 'Torre' debe coincidir con un Nombre de la hoja 'Torres'", false, false),
            ("   (si escribes una torre que no existe, se crea automaticamente). Dejala vacia si la unidad no tiene torre.", false, false),
            ("3. Guarda el archivo y subelo en Mi Copropiedad > Distribucion > Importar.", false, false),
            ("", false, false),
            ("REGLAS:", true, false),
            ("- No cambies el ORDEN ni borres las columnas de encabezado; el importador las lee por posicion.", false, false),
            ("- Los campos marcados con * son obligatorios (Numero y Tipo en Unidades; Nombre en Torres).", false, false),
            ("- 'Tipo' debe ser uno de los valores validos (ver hoja 'Catalogos'): Apartamento, Local, Casa, Oficina, Bodega, Parqueadero, UtilCuarto.", false, false),
            ("- 'Coeficiente (%)' es el porcentaje de participacion de la unidad. La suma de todas deberia dar 100.", false, false),
            ("- Numeros decimales: acepta coma o punto (ej. 18,5 o 18.5). No uses separador de miles.", false, false),
            ("- 'Paga administracion': escribe Si o No (por defecto Si).", false, false),
            ("- El 'Numero' de la unidad debe ser unico en la copropiedad (ej. A101, B102, L-01).", false, false),
            ("", false, false),
            ("Si una fila tiene un error, esa fila se reporta y se omite, pero el resto SI se importa.", false, false),
        };
        int r = 1;
        foreach (var (txt, bold, title) in lineas)
        {
            var c = ws.Cell(r, 1);
            c.Value = txt;
            if (bold) c.Style.Font.Bold = true;
            if (title) { c.Style.Font.FontSize = 15; c.Style.Font.FontColor = XLColor.FromHtml("#5955D1"); }
            r++;
        }
    }

    private static void HojaUnidades(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Unidades");
        EscribirEncabezado(ws, UnidadesHeaders);

        // Ejemplo completo y cargable (coeficientes suman 100).
        object[][] ejemplo =
        {
            new object[] { "A101", "Apartamento", "Torre A", 1, 18, 72, 3, 2, 1, "Ocupado",    "MAT-A101", "Si", 350000, "" },
            new object[] { "A102", "Apartamento", "Torre A", 1, 18, 72, 3, 2, 1, "Ocupado",    "MAT-A102", "Si", 350000, "" },
            new object[] { "A201", "Apartamento", "Torre A", 2, 18, 80, 3, 2, 1, "Desocupado", "MAT-A201", "Si", 380000, "" },
            new object[] { "B101", "Apartamento", "Torre B", 1, 18, 72, 3, 2, 1, "Arrendado",  "MAT-B101", "Si", 350000, "" },
            new object[] { "B102", "Apartamento", "Torre B", 1, 18, 72, 3, 2, 1, "Ocupado",    "MAT-B102", "Si", 350000, "" },
            new object[] { "L-01", "Local",       "",        1, 10, 45, 0, 1, 0, "Arrendado",  "MAT-L01",  "Si", 600000, "Local comercial esquinero" },
        };
        int r = 2;
        foreach (var fila in ejemplo)
        {
            for (int col = 0; col < fila.Length; col++) ws.Cell(r, col + 1).Value = XLCellValue.FromObject(fila[col]);
            r++;
        }
        // Anchos fijos (NO usar AdjustToContents: en servidor headless dispara el motor de fuentes
        // de ClosedXML y tarda decenas de segundos).
        for (int col = 1; col <= UnidadesHeaders.Length; col++) ws.Column(col).Width = 16;
        ws.SheetView.FreezeRows(1);
    }

    private static void HojaTorres(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Torres");
        EscribirEncabezado(ws, TorresHeaders);
        object[][] ejemplo =
        {
            new object[] { "Torre A", 3, "Torre principal" },
            new object[] { "Torre B", 3, "Torre secundaria" },
        };
        int r = 2;
        foreach (var fila in ejemplo)
        {
            for (int col = 0; col < fila.Length; col++) ws.Cell(r, col + 1).Value = XLCellValue.FromObject(fila[col]);
            r++;
        }
        ws.Column(1).Width = 22; ws.Column(2).Width = 18; ws.Column(3).Width = 30;
        ws.SheetView.FreezeRows(1);
    }

    private static void HojaCatalogos(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Catalogos");
        ws.Cell(1, 1).Value = "Tipos de unidad validos (columna Tipo)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        int r = 2;
        foreach (var n in Enum.GetNames<TipoUnidad>()) ws.Cell(r++, 1).Value = n;

        ws.Cell(1, 3).Value = "Paga administracion";
        ws.Cell(1, 3).Style.Font.Bold = true;
        ws.Cell(2, 3).Value = "Si";
        ws.Cell(3, 3).Value = "No";

        ws.Cell(1, 5).Value = "Estados sugeridos (texto libre)";
        ws.Cell(1, 5).Style.Font.Bold = true;
        var estados = new[] { "Ocupado", "Desocupado", "Arrendado", "En obra", "Disponible" };
        r = 2;
        foreach (var e in estados) ws.Cell(r++, 5).Value = e;

        ws.Column(1).Width = 22; ws.Column(3).Width = 20; ws.Column(5).Width = 30;
    }

    private static void EscribirEncabezado(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#5955D1");
        }
    }

    // =====================================================================================
    // IMPORT
    // =====================================================================================
    public async Task<ImportarDistribucionResultado> ImportarAsync(Stream contenidoXlsx, CancellationToken ct)
    {
        var errores = new List<ImportarErrorFila>();
        int torresCreadas = 0, unidadesCreadas = 0;

        using var wb = new XLWorkbook(contenidoXlsx);

        // Mapa Nombre(insensible a mayus/minus) -> torreId. Se siembra con las torres ya existentes.
        var torres = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in await _svc.ListTorresAsync(ct))
            torres[t.Nombre.Trim()] = t.Id;

        // ---- Torres ----
        if (wb.TryGetWorksheet("Torres", out var wsT))
        {
            foreach (var row in wsT.RowsUsed().Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                var nombre = Str(row.Cell(1));
                if (nombre is null) continue;
                if (torres.ContainsKey(nombre)) continue; // ya existe (o repetida en el archivo)
                try
                {
                    var dto = await _svc.CrearTorreAsync(new CrearTorreRequest(nombre, Int(row.Cell(2)), Str(row.Cell(3))), ct);
                    torres[dto.Nombre.Trim()] = dto.Id;
                    torresCreadas++;
                }
                catch (Exception ex) { errores.Add(new("Torres", row.RowNumber(), Msg(ex))); }
            }
        }

        // ---- Unidades ----
        if (wb.TryGetWorksheet("Unidades", out var wsU))
        {
            foreach (var row in wsU.RowsUsed().Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                var numero = Str(row.Cell(1));
                var tipoStr = Str(row.Cell(2));
                if (numero is null && tipoStr is null) continue; // fila vacia
                int fila = row.RowNumber();

                if (numero is null) { errores.Add(new("Unidades", fila, "Falta el Numero de la unidad.")); continue; }
                var tipo = ParseTipo(tipoStr);
                if (tipo is null)
                {
                    errores.Add(new("Unidades", fila, $"Tipo invalido: '{tipoStr}'. Validos: {string.Join(", ", Enum.GetNames<TipoUnidad>())}."));
                    continue;
                }

                Guid? torreId = null;
                var torreNombre = Str(row.Cell(3));
                if (torreNombre is not null)
                {
                    if (!torres.TryGetValue(torreNombre, out var tid))
                    {
                        try
                        {
                            var d = await _svc.CrearTorreAsync(new CrearTorreRequest(torreNombre, null, null), ct);
                            tid = d.Id; torres[d.Nombre.Trim()] = d.Id; torresCreadas++;
                        }
                        catch (Exception ex) { errores.Add(new("Unidades", fila, $"No se pudo crear la torre '{torreNombre}': {Msg(ex)}")); continue; }
                    }
                    torreId = tid;
                }

                var req = new CrearUnidadRequest(
                    numero, tipo.Value, torreId, Int(row.Cell(4)),
                    Dec(row.Cell(5)) ?? 0m, Dec(row.Cell(6)),
                    Int(row.Cell(7)), Int(row.Cell(8)), Int(row.Cell(9)),
                    Str(row.Cell(10)), Str(row.Cell(14)),
                    Str(row.Cell(11)), Bool(row.Cell(12), true), Dec(row.Cell(13)));

                try { await _svc.CrearUnidadAsync(req, ct); unidadesCreadas++; }
                catch (Exception ex) { errores.Add(new("Unidades", fila, Msg(ex))); }
            }
        }

        return new ImportarDistribucionResultado(torresCreadas, unidadesCreadas, errores.Count, errores);
    }

    // ---- helpers de lectura de celdas ----
    private static string? Str(IXLCell c)
    {
        var v = c.GetString()?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static int? Int(IXLCell c)
    {
        var s = Str(c);
        if (s is null) return null;
        var d = Dec(s);
        return d is null ? null : (int)Math.Round(d.Value);
    }

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

    private static TipoUnidad? ParseTipo(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return Enum.TryParse<TipoUnidad>(s.Trim(), true, out var t) && Enum.IsDefined(t) ? t : null;
    }

    private static string Msg(Exception ex) => ex is InvalidOperationException ? ex.Message : ex.Message;
}
