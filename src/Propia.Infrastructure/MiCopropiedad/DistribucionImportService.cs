using System.Globalization;
using ClosedXML.Excel;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Genera la plantilla de Distribucion y procesa la carga masiva. Reusa IMiCopropiedadService
/// (CrearUnidadAsync) para que la validacion, RLS y bitacora sean identicas a la creacion manual.
/// El orden de las columnas es fijo (documentado en la hoja Instrucciones). El codigo de la unidad
/// ES su Numero (texto libre): ya no hay Torre.
/// </summary>
public class DistribucionImportService : IDistribucionImportService
{
    private readonly IMiCopropiedadService _svc;
    public DistribucionImportService(IMiCopropiedadService svc) => _svc = svc;

    // Encabezados fijos (el import mapea por POSICION, no por texto). Las primeras 13 columnas
    // son la unidad; de la 14 en adelante van las personas y los inmuebles vinculados, todo en la
    // misma fila = 1 unidad con su gente. Un archivo LEGADO que aun traiga la columna "Torre" (en la
    // posicion 3) se detecta por el encabezado y su valor se hornea como prefijo del codigo.
    private static readonly string[] UnidadesHeaders = BuildUnidadesHeaders();

    private static string[] BuildUnidadesHeaders()
    {
        var h = new List<string>
        {
            "Número *", "Tipo *", "Piso", "Coeficiente (%)", "Área (m2)",
            "Habitaciones", "Baños", "Parqueaderos", "Estado", "Matrícula inmobiliaria",
            "Paga administración (Si/No)", "Cuota mensual", "Observaciones",
        };
        // Personas 1:1 con la unidad: 2 propietarios, 1 residente, 1 arrendatario.
        foreach (var p in new[] { "Prop.1", "Prop.2", "Residente", "Arrendatario" })
            h.AddRange(new[] { $"{p} Cédula", $"{p} Nombres", $"{p} Apellidos", $"{p} Email", $"{p} Teléfono" });
        // Grupo familiar: hasta 5, cada uno con su parentesco.
        for (int i = 1; i <= 5; i++)
            h.AddRange(new[] { $"Familiar {i} Cédula", $"Familiar {i} Nombres", $"Familiar {i} Apellidos", $"Familiar {i} Parentesco" });
        // Inmuebles vinculados: numero de la unidad asociada (parqueadero, deposito, etc.).
        h.AddRange(new[] { "Inmueble vinc.1 (número)", "Inmueble vinc.2 (número)", "Inmueble vinc.3 (número)" });
        return h.ToArray();
    }

    // Helpers para armar el ejemplo sin contar columnas a mano.
    private static object[] P(string ced, string nom, string ape, string email, string tel) => new object[] { ced, nom, ape, email, tel };
    private static object[] F(string ced, string nom, string ape, string par) => new object[] { ced, nom, ape, par };
    private static readonly object[] B5 = { "", "", "", "", "" };
    private static readonly object[] B4 = { "", "", "", "" };

    // =====================================================================================
    // PLANTILLA
    // =====================================================================================
    public byte[] GenerarPlantilla()
    {
        using var wb = new XLWorkbook();

        HojaInstrucciones(wb);
        HojaUnidades(wb);
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
            ("PLANTILLA DE DISTRIBUCIÓN - PROPIA", true, true),
            ("", false, false),
            ("Con esta plantilla cargas de una sola vez las UNIDADES privadas de la copropiedad.", false, false),
            ("El archivo ya trae un ejemplo completo (8 unidades con sus propietarios, residentes, arrendatarios, familiares e inmuebles vinculados). Reemplaza esas filas por tus datos reales.", false, false),
            ("Todas las personas del ejemplo se crean en el Directorio al importar. Cada unidad es UNA fila con toda su gente en columnas.", false, false),
            ("", false, false),
            ("PASOS:", true, false),
            ("1. Llena la hoja 'Unidades'. El 'Número' es el código de la unidad (texto libre) y debe ser único.", false, false),
            ("2. Guarda el archivo y súbelo en Mi Copropiedad > Distribución > Importar.", false, false),
            ("", false, false),
            ("REGLAS:", true, false),
            ("- No cambies el ORDEN ni borres las columnas de encabezado; el importador las lee por posición.", false, false),
            ("- Los campos marcados con * son obligatorios (Número y Tipo en Unidades).", false, false),
            ("- 'Tipo' debe ser uno de los valores válidos (ver hoja 'Catalogos'): Apartamento, Local, Casa, Oficina, Bodega, Parqueadero, UtilCuarto.", false, false),
            ("- 'Coeficiente (%)' es el porcentaje de participación de la unidad. La suma de todas debería dar 100.", false, false),
            ("- Parqueaderos y Cuartos útiles TAMBIÉN pueden tener coeficiente. Si 'Paga administración' = Si, su coeficiente cuenta dentro del 100% (como en el ejemplo P-01 y CU-01).", false, false),
            ("- Números decimales: acepta coma o punto (ej. 18,5 o 18.5). No uses separador de miles.", false, false),
            ("- 'Paga administración': escribe Si o No (por defecto Si).", false, false),
            ("- El 'Número' de la unidad debe ser único en la copropiedad (ej. A101, B102, L-01).", false, false),
            ("", false, false),
            ("PERSONAS (se crean en el Directorio automáticamente):", true, false),
            ("- En la misma fila de cada unidad puedes cargar: Propietario 1, Propietario 2, Residente, Arrendatario y hasta 5 Familiares.", false, false),
            ("- De cada persona basta Cédula + Nombres + Apellidos; Email y Teléfono son opcionales. Si la cédula ya existe, se reutiliza (no se duplica).", false, false),
            ("- 'Residente' es quien habita sin ser dueño; 'Arrendatario' es el inquilino; cada 'Familiar' lleva su Parentesco (ver hoja Catalogos).", false, false),
            ("- Si un dueño o residente es una EMPRESA, deja la persona en blanco aquí y vincúlala luego desde el Directorio (persona jurídica).", false, false),
            ("", false, false),
            ("INMUEBLES VINCULADOS:", true, false),
            ("- En 'Inmueble vinc.1/2/3' escribe el NÚMERO de otra unidad ya listada (ej. el parqueadero P-01 o el cuarto útil CU-01) para asociarla a esta.", false, false),
            ("- La unidad asociada debe existir en la hoja (se vincula al final, cuando todas las unidades ya están creadas).", false, false),
            ("", false, false),
            ("Si una fila tiene un error, esa fila se reporta y se omite, pero el resto SI se importa. Un error en una persona no tumba la unidad.", false, false),
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

        // Ejemplo completo y cargable (coeficientes suman 100). Cada unidad trae en su MISMA fila
        // sus propietarios, residente, arrendatario, grupo familiar e inmuebles vinculados. Todas
        // las personas caen al Directorio al importar (dedup por cedula). P-01 y CU-01 no llevan
        // gente: son los inmuebles que A101 vincula.
        var ejemplo = new List<object[]>
        {
            // A101: habitada por sus 2 duenos, con 2 familiares y 2 inmuebles vinculados (P-01, CU-01).
            new object[] { "A101", "Apartamento", 1, 16, 72, 3, 2, 1, "Ocupado", "MAT-A101", "Si", 350000, "" }
                .Concat(P("1090111", "Alex", "Cuartas", "alex@demo.com", "3001112233"))
                .Concat(P("52233444", "Maria", "Gomez", "maria@demo.com", "3004445566"))
                .Concat(B5) // residente (viven los duenos)
                .Concat(B5) // arrendatario
                .Concat(F("1010101", "Ana", "Cuartas", "Hija"))
                .Concat(F("1010102", "Leo", "Cuartas", "Hijo"))
                .Concat(B4).Concat(B4).Concat(B4)          // familiares 3-5
                .Concat(new object[] { "P-01", "CU-01", "" })  // inmuebles vinculados
                .ToArray(),
            // A102: arrendada. 1 dueno + arrendatario que la habita.
            new object[] { "A102", "Apartamento", 1, 16, 72, 3, 2, 1, "Arrendado", "MAT-A102", "Si", 350000, "" }
                .Concat(P("70012345", "Jorge", "Rios", "jorge@demo.com", "3007778899"))
                .Concat(B5)  // prop 2
                .Concat(B5)  // residente
                .Concat(P("1122334", "Camila", "Soto", "camila@demo.com", "3010001122"))  // arrendatario
                .ToArray(),
            // A201: por ahora solo un dueno registrado.
            new object[] { "A201", "Apartamento", 2, 16, 80, 3, 2, 1, "Desocupado", "MAT-A201", "Si", 380000, "" }
                .Concat(P("43112233", "Lucia", "Marin", "lucia@demo.com", "3020003344"))
                .ToArray(),
            // B101: dueno + residente (un tercero que habita, no dueno).
            new object[] { "B101", "Apartamento", 1, 16, 72, 3, 2, 1, "Ocupado", "MAT-B101", "Si", 350000, "" }
                .Concat(P("80045566", "Pedro", "Navarro", "pedro@demo.com", "3030005566"))
                .Concat(B5)  // prop 2
                .Concat(P("1133557", "Sara", "Navarro", "sara@demo.com", "3033335555"))  // residente
                .ToArray(),
            new object[] { "B102", "Apartamento", 1, 16, 72, 3, 2, 1, "Ocupado", "MAT-B102", "Si", 350000, "" }
                .Concat(P("52999888", "Diana", "Pena", "diana@demo.com", "3040007788"))
                .ToArray(),
            // L-01: local arrendado (dueno + arrendatario).
            new object[] { "L-01", "Local", 1, 10, 45, 0, 1, 0, "Arrendado", "MAT-L01", "Si", 600000, "Local comercial esquinero" }
                .Concat(P("79088777", "Ricardo", "Vega", "ricardo@demo.com", "3050009900"))
                .Concat(B5).Concat(B5)  // prop 2 + residente
                .Concat(P("15577889", "Raul", "Prieto", "raul@demo.com", "3055551212"))  // arrendatario
                .ToArray(),
            // P-01 y CU-01: sin personas; son los inmuebles que A101 vincula.
            new object[] { "P-01",  "Parqueadero", 1, 5, 12, 0, 0, 0, "Disponible", "MAT-P01",  "Si", "", "Parqueadero con coeficiente (cuenta en el 100%)" },
            new object[] { "CU-01", "UtilCuarto",  1, 5,  6, 0, 0, 0, "Disponible", "MAT-CU01", "Si", "", "Cuarto util con coeficiente (cuenta en el 100%)" },
        };
        int r = 2;
        foreach (var fila in ejemplo)
        {
            // La fila puede venir mas corta que el encabezado (celdas finales vacias) o traer huecos:
            // se rellenan en blanco para no romper XLCellValue.FromObject (no acepta null).
            for (int col = 0; col < UnidadesHeaders.Length; col++)
            {
                var val = col < fila.Length ? (fila[col] ?? "") : "";
                ws.Cell(r, col + 1).Value = XLCellValue.FromObject(val);
            }
            r++;
        }
        // Anchos fijos (NO usar AdjustToContents: en servidor headless dispara el motor de fuentes
        // de ClosedXML y tarda decenas de segundos).
        for (int col = 1; col <= UnidadesHeaders.Length; col++) ws.Column(col).Width = 16;
        ws.SheetView.FreezeRows(1);
    }

    private static void HojaCatalogos(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Catalogos");
        ws.Cell(1, 1).Value = "Tipos de unidad validos (columna Tipo)";
        ws.Cell(1, 1).Style.Font.Bold = true;
        int r = 2;
        foreach (var n in Enum.GetNames<TipoUnidad>()) ws.Cell(r++, 1).Value = n;

        ws.Cell(1, 3).Value = "Paga administración";
        ws.Cell(1, 3).Style.Font.Bold = true;
        ws.Cell(2, 3).Value = "Si";
        ws.Cell(3, 3).Value = "No";

        ws.Cell(1, 5).Value = "Estados sugeridos (texto libre)";
        ws.Cell(1, 5).Style.Font.Bold = true;
        var estados = new[] { "Ocupado", "Desocupado", "Arrendado", "En obra", "Disponible" };
        r = 2;
        foreach (var e in estados) ws.Cell(r++, 5).Value = e;

        ws.Cell(1, 7).Value = "Roles de persona (columnas de personas)";
        ws.Cell(1, 7).Style.Font.Bold = true;
        var roles = new[] { "Propietario", "Residente", "Arrendatario", "Familiar" };
        r = 2;
        foreach (var x in roles) ws.Cell(r++, 7).Value = x;

        ws.Cell(1, 9).Value = "Parentescos sugeridos (Familiar)";
        ws.Cell(1, 9).Style.Font.Bold = true;
        var parentescos = new[] { "Conyuge", "Hijo", "Hija", "Padre", "Madre", "Hermano", "Otro" };
        r = 2;
        foreach (var x in parentescos) ws.Cell(r++, 9).Value = x;

        ws.Column(1).Width = 22; ws.Column(3).Width = 20; ws.Column(5).Width = 30;
        ws.Column(7).Width = 26; ws.Column(9).Width = 26;
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
        int unidadesCreadas = 0, personasVinculadas = 0, vinculosCreados = 0;

        using var wb = new XLWorkbook(contenidoXlsx);

        // Mapa Numero -> unidadId (para resolver los inmuebles vinculados al final). Se siembra
        // con las unidades ya existentes por si una fila vincula una unidad que ya estaba.
        var unidades = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in await _svc.ListUnidadesAsync(ct))
            unidades[u.Numero.Trim()] = u.Id;

        // Vinculos entre unidades: se difieren hasta que TODAS las unidades esten creadas
        // (la unidad asociada puede aparecer en una fila posterior del archivo).
        var vinculosPend = new List<(int fila, string principal, string asociada)>();

        // ---- Unidades ----
        if (wb.TryGetWorksheet("Unidades", out var wsU))
        {
            // Compatibilidad con archivos LEGADOS: si la plantilla trae todavia la columna "Torre"
            // en la posicion 3, se detecta por el encabezado y todas las columnas siguientes van
            // corridas en 1. El valor de esa columna, si viene, se hornea como prefijo del codigo.
            var cab3 = Str(wsU.Cell(1, 3));
            var tieneTorre = cab3 is not null && cab3.Trim().ToLowerInvariant().StartsWith("torre");
            int off = tieneTorre ? 1 : 0;

            foreach (var row in wsU.RowsUsed().Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                var numero = Str(row.Cell(1));
                var tipoStr = Str(row.Cell(2));
                if (numero is null && tipoStr is null) continue; // fila vacia
                int fila = row.RowNumber();

                if (numero is null) { errores.Add(new("Unidades", fila, "Falta el Número de la unidad.")); continue; }
                var tipo = ParseTipo(tipoStr);
                if (tipo is null)
                {
                    errores.Add(new("Unidades", fila, $"Tipo inválido: '{tipoStr}'. Válidos: {string.Join(", ", Enum.GetNames<TipoUnidad>())}."));
                    continue;
                }

                // Legado: la columna Torre (pos 3) se hornea como prefijo del codigo ("<torre>-<numero>").
                if (tieneTorre)
                {
                    var torreNombre = Str(row.Cell(3));
                    if (torreNombre is not null) numero = $"{torreNombre}-{numero}";
                }

                var req = new CrearUnidadRequest(
                    numero, tipo.Value, Int(row.Cell(3 + off)),
                    Dec(row.Cell(4 + off)) ?? 0m, Dec(row.Cell(5 + off)),
                    Int(row.Cell(6 + off)), Int(row.Cell(7 + off)), Int(row.Cell(8 + off)),
                    Str(row.Cell(9 + off)), Str(row.Cell(13 + off)),
                    Str(row.Cell(10 + off)), Bool(row.Cell(11 + off), true), Dec(row.Cell(12 + off)));

                UnidadDto creada;
                try { creada = await _svc.CrearUnidadAsync(req, ct); }
                catch (Exception ex) { errores.Add(new("Unidades", fila, Msg(ex))); continue; }
                unidadesCreadas++;
                unidades[creada.Numero.Trim()] = creada.Id;

                // Personas 1:1 (bloques de 5 columnas): Prop1 (14), Prop2 (19), Residente (24),
                // Arrendatario (29). Cada una se crea en el Directorio (dedup por cedula) y se
                // vincula a la unidad con su rol. Un error de persona NO tumba la unidad.
                if (await AgregarPersonaDesdeFila(creada.Id, row, 14 + off, RolUnidadPersona.Propietario, false, fila, errores, ct)) personasVinculadas++;
                if (await AgregarPersonaDesdeFila(creada.Id, row, 19 + off, RolUnidadPersona.Propietario, false, fila, errores, ct)) personasVinculadas++;
                if (await AgregarPersonaDesdeFila(creada.Id, row, 24 + off, RolUnidadPersona.Residente, false, fila, errores, ct)) personasVinculadas++;
                if (await AgregarPersonaDesdeFila(creada.Id, row, 29 + off, RolUnidadPersona.Arrendatario, false, fila, errores, ct)) personasVinculadas++;
                // Grupo familiar: bloques de 4 columnas (cedula, nombres, apellidos, parentesco) desde la 34.
                for (int fCol = 34 + off; fCol <= 50 + off; fCol += 4)
                    if (await AgregarPersonaDesdeFila(creada.Id, row, fCol, RolUnidadPersona.Familiar, true, fila, errores, ct)) personasVinculadas++;

                // Inmuebles vinculados (cols 54, 55, 56): numero de la unidad asociada. Se difiere.
                foreach (var vCol in new[] { 54 + off, 55 + off, 56 + off })
                {
                    var asociada = Str(row.Cell(vCol));
                    if (asociada is not null) vinculosPend.Add((fila, numero, asociada));
                }
            }
        }

        // ---- Inmuebles vinculados (al final, con todas las unidades ya creadas) ----
        foreach (var (fila, principal, asociada) in vinculosPend)
        {
            ct.ThrowIfCancellationRequested();
            if (!unidades.TryGetValue(principal, out var pid))
            { errores.Add(new("Unidades", fila, $"Inmueble vinculado: la unidad principal '{principal}' no existe.")); continue; }
            if (!unidades.TryGetValue(asociada, out var aid))
            { errores.Add(new("Unidades", fila, $"Inmueble vinculado: la unidad asociada '{asociada}' no existe.")); continue; }
            if (pid == aid)
            { errores.Add(new("Unidades", fila, $"Inmueble vinculado: '{principal}' no puede vincularse a si misma.")); continue; }
            try { await _svc.CrearVinculoAsync(pid, new CrearVinculoUnidadRequest(aid, false), ct); vinculosCreados++; }
            catch (Exception ex) { errores.Add(new("Unidades", fila, $"Inmueble vinculado {principal}->{asociada}: {Msg(ex)}")); }
        }

        return new ImportarDistribucionResultado(unidadesCreadas, errores.Count, errores, personasVinculadas, vinculosCreados);
    }

    /// <summary>
    /// Crea (o reutiliza por cedula) una persona y la vincula a la unidad con el rol dado.
    /// Bloque de 5 columnas (cedula, nombres, apellidos, email, telefono) para propietario/
    /// residente/arrendatario; bloque de 4 (cedula, nombres, apellidos, parentesco) para familiar.
    /// Devuelve true si se vinculo una persona. Sin cedula, no hace nada (celda vacia).
    /// </summary>
    private async Task<bool> AgregarPersonaDesdeFila(
        Guid unidadId, IXLRow row, int baseCol, RolUnidadPersona rol, bool esFamiliar,
        int fila, List<ImportarErrorFila> errores, CancellationToken ct)
    {
        var ced = Str(row.Cell(baseCol));
        var nom = Str(row.Cell(baseCol + 1));
        var ape = Str(row.Cell(baseCol + 2));
        if (ced is null)
        {
            // Si hay nombre pero no cedula, avisamos (probable olvido); si esta todo vacio, se ignora.
            if (nom is not null || ape is not null)
                errores.Add(new("Unidades", fila, $"{rol}: falta la cedula (hay nombre pero no documento)."));
            return false;
        }
        try
        {
            var req = esFamiliar
                ? new AgregarPersonaUnidadRequest(ced, nom ?? "", ape ?? "", null, null, RolUnidadPersona.Familiar, true, Str(row.Cell(baseCol + 3)))
                : new AgregarPersonaUnidadRequest(ced, nom ?? "", ape ?? "", Str(row.Cell(baseCol + 3)), Str(row.Cell(baseCol + 4)), rol);
            await _svc.AgregarPersonaUnidadAsync(unidadId, req, ct);
            return true;
        }
        catch (Exception ex) { errores.Add(new("Unidades", fila, $"{rol} (cedula {ced}): {Msg(ex)}")); return false; }
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
