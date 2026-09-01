using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Genera la plantilla Excel de carga masiva de unidades privadas (y personas/vehiculos/mascotas/
/// terceros). Trae los IDs de las copropiedades del cliente y catalogos como datos de referencia,
/// y aplica listas desplegables (validacion de datos) para forzar valores validos del sistema.
/// </summary>
public sealed class UnidadesPlantillaService : IUnidadesPlantillaService
{
    private const int DataStart = 4;      // fila 1 = banner, fila 2 = encabezado, fila 3 = ayuda, datos desde la 4
    private const int MaxRows = 1000;     // hasta donde se aplican los dropdowns

    /// <summary>Opcion especial de la columna COPROPIEDAD en la hoja TERCEROS: el tercero queda visible
    /// en TODAS las copropiedades del cliente. La reusa el importador para saber que debe crear el
    /// vinculo en cada copropiedad.</summary>
    public const string TodasLasCopropiedades = "Todas las copropiedades";

    // Paleta PROPIA (para que la plantilla se vea como salida del sistema).
    private static readonly XLColor Brand = XLColor.FromHtml("#6D4FE3");
    private static readonly XLColor Ink = XLColor.FromHtml("#1B2A3A");
    private static readonly XLColor Soft = XLColor.FromHtml("#F1ECFD");
    private static readonly XLColor BrandText = XLColor.FromHtml("#4B2BB0");

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextAccessor _http;

    public UnidadesPlantillaService(PropiaDbContext db, ITenantContext tenant, IHttpContextAccessor http)
    {
        _db = db;
        _tenant = tenant;
        _http = http;
    }

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaCargaAsync(CancellationToken ct)
    {
        var copros = await CopropiedadesDelClienteAsync(ct);
        var roles = await _db.RolesCopropiedad.AsNoTracking()
            .Where(r => r.Activo).OrderBy(r => r.Nombre).Select(r => r.Nombre).ToListAsync(ct);
        var camposUnidad = await _db.UnidadCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);

        using var wb = new XLWorkbook();

        // Listas para los desplegables, INLINE (sin hoja de referencia "DATOS DE CARGA").
        // Si una lista no cabe inline (demasiados valores o con comas), esa columna queda
        // libre (sin dropdown) en vez de romper la validacion.
        var coproList = InlineList(copros.Select(c => c.Nombre));
        var rolesList = InlineList(roles);
        // Terceros: la lista arranca con "Todas las copropiedades" (visible en todas) + las del cliente.
        var coproTercerosList = InlineList(new[] { TodasLasCopropiedades }.Concat(copros.Select(c => c.Nombre)));

        // ---- Hojas de datos ----
        HojaUnidades(wb, coproList, camposUnidad);
        HojaPersonas(wb, coproList, rolesList);
        HojaVehiculos(wb, coproList);
        HojaMascotas(wb, coproList);
        HojaTerceros(wb, coproTercerosList);
        HojaZonasComunes(wb, coproList);
        HojaEquipos(wb, coproList);

        wb.Properties.Author = "PROPIA";
        wb.Properties.Company = "A&D GROUP S.A.S";
        wb.Properties.Title = "Plantilla de carga - Unidades privadas";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), "Plantilla carga unidades privadas.xlsx");
    }

    // Lista de validacion INLINE (formula "a,b,c") si cabe en Excel y no hay comas en los
    // valores; de lo contrario null (esa columna queda sin dropdown). Sin hoja de referencia.
    private static string? InlineList(IEnumerable<string> valores)
    {
        var vals = valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        if (vals.Count == 0) return null;
        var inline = string.Join(",", vals);
        if (vals.Any(v => v.Contains(',')) || inline.Length > 250) return null;
        return "\"" + inline + "\"";
    }

    // ===================== Hojas de datos =====================
    private static void HojaUnidades(XLWorkbook wb, string? coproRange, List<string> camposUnidad)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad con guion TORRE-NUMERO. Ej: Apartamento A1-101 (A1=Torre, 101=Apto); Parqueadero P1-15; Deposito D1-02"),
            ("TIPO", "Elige de la lista"),
            ("AGRUPACION", "1=Individual, 2=Principal, 3=Anexo"),
            ("PRINCIPAL", "Si es Anexo (3): codigo de la unidad principal"),
            ("MATRICULA", "Matricula inmobiliaria"),
            ("COEFICIENTE", "Porcentaje. Max 5 decimales (1,25)"),
            ("REF PAGO", "Referencia de pago (alfanumerica)"),
        };
        foreach (var lbl in camposUnidad) cols.Add(($"[{lbl}]", "Campo dinamico de la copropiedad"));

        var ws = Encabezado(wb, "UNIDADES PRIVADAS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, EnumCsv<TipoUnidad>());   // todos los tipos de unidad (auto desde el enum)
        DropdownInline(ws, 4, "1,2,3");
        Ejemplo(ws, EjemploCopro, "A1-203", "Apartamento", "2", "", "", "1.25", "");
        Ajustar(ws, cols.Count);
    }

    private static void HojaPersonas(XLWorkbook wb, string? coproRange, string? rolesRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad (debe existir en la hoja UNIDADES)"),
            ("TIPO RESIDENTE", "Elige de la lista"),
            ("TIPO ID", "Elige de la lista"),
            ("NOMBRE", "Nombre completo (o razon social si NIT)"),
            ("IDENTIFICACION", "Documento/NIT"),
            ("EMAIL", ""),
            ("TELEFONO", ""),
            ("SEXO", "M o F"),
            ("FECHA NACIMIENTO", "AAAA-MM-DD"),
            ("PROFESION", ""),
            ("ROLL", "Rol del sistema (opcional; crea usuario)"),
        };
        var ws = Encabezado(wb, "PERSONAS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, "Propietario,Residente,Familiar,Arrendatario,Apoderado");
        DropdownInline(ws, 4, "CC,CE,Pasaporte,NIT,Otro");
        DropdownInline(ws, 9, "M,F");
        Dropdown(ws, 12, rolesRange);
        Ejemplo(ws, EjemploCopro, "A1-203", "Propietario", "CC", "Juan Perez", "123456789", "juan@correo.com", "3001234567", "M", "1985-04-12", "Ingeniero", "");
        Ajustar(ws, cols.Count);
    }

    private static void HojaVehiculos(XLWorkbook wb, string? coproRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad"),
            ("TIPO DE VEHICULO", "Elige de la lista"),
            ("MARCA", ""), ("MODELO", ""), ("COLOR", ""), ("PLACA", ""),
        };
        var ws = Encabezado(wb, "VEHICULOS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, "Automovil,Moto,Bicicleta,Camioneta,Otro");
        Ejemplo(ws, EjemploCopro, "A1-203", "Automovil", "Mazda", "2022", "Gris", "ABC123");
        Ajustar(ws, cols.Count);
    }

    private static void HojaMascotas(XLWorkbook wb, string? coproRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad"),
            ("TIPO MASCOTA", "Elige de la lista"),
            ("RAZA", ""), ("NOMBRE", ""),
        };
        var ws = Encabezado(wb, "MASCOTAS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, "Perro,Gato,Ave,Otro");
        Ejemplo(ws, EjemploCopro, "A1-203", "Perro", "Labrador", "Rocky");
        Ajustar(ws, cols.Count);
    }

    // Un tercero NO se relaciona con una unidad; solo con la copropiedad. Con "Todas las copropiedades"
    // el tercero queda visible en TODAS las copropiedades del cliente (se crea global + un vinculo en cada
    // una). Por eso no hay columnas ALCANCE ni UNIDAD PRIVADA.
    private static void HojaTerceros(XLWorkbook wb, string? coproTercerosRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista. 'Todas las copropiedades' = visible en todas."),
            ("TIPO ID", "Elige de la lista"),
            ("NOMBRE", "Nombre completo / razon social"),
            ("IDENTIFICACION", "Documento/NIT"),
            ("EMAIL", ""), ("TELEFONO", ""),
        };
        var ws = Encabezado(wb, "TERCEROS", cols);
        Dropdown(ws, 1, coproTercerosRange);
        DropdownInline(ws, 2, "CC,CE,Pasaporte,NIT,Otro");
        // El ejemplo usa EjemploCopro para que el importador lo omita; la ayuda ya explica "Todas...".
        Ejemplo(ws, EjemploCopro, "CC", "Maria Lopez", "987654321", "maria@correo.com", "3009876543");
        Ajustar(ws, cols.Count);
    }

    // ===================== Hojas nuevas: Zonas comunes y Equipos =====================
    private static void HojaZonasComunes(XLWorkbook wb, string? coproRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("NOMBRE", "Nombre de la zona (obligatorio)"),
            ("CATEGORIA", "Elige de la lista"),
            ("RESERVABLE", "Si / No"),
            ("AFORO", "Capacidad en personas (numero)"),
            ("ESTADO", "Elige de la lista"),
            ("DESCRIPCION", ""),
            ("TARIFA RESERVA", "Valor de la reserva (numero)"),
            ("REGLAS DE USO", ""),
        };
        var ws = Encabezado(wb, "ZONAS COMUNES", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, EnumCsv<CategoriaZonaComun>());
        DropdownInline(ws, 4, "Si,No");
        DropdownInline(ws, 6, EnumCsv<EstadoZonaComunMantenimiento>());
        Ejemplo(ws, EjemploCopro, "Salon Social", "Social", "Si", "80", "Activa", "Salon para eventos", "50000", "Reservar con 3 dias");
        Ajustar(ws, cols.Count);
    }

    private static void HojaEquipos(XLWorkbook wb, string? coproRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("NOMBRE", "Nombre del equipo/activo (obligatorio)"),
            ("CATEGORIA", "Elige de la lista"),
            ("TIPO", "Equipo / Activo"),
            ("CANTIDAD", "Numero (>=1)"),
            ("RESERVABLE", "Si / No"),
            ("MODELO", ""),
            ("NUMERO DE SERIE", ""),
            ("UBICACION", ""),
            ("ESTADO", "Elige de la lista"),
            ("OBSERVACIONES", ""),
            ("VIDA UTIL", "Anios (numero)"),
            ("VALOR ADQUISICION", "Numero"),
            ("PROVEEDOR", ""),
            ("NUMERO FACTURA", ""),
        };
        var ws = Encabezado(wb, "EQUIPOS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, EnumCsv<CategoriaEquipo>());
        DropdownInline(ws, 4, EnumCsv<TipoElemento>());
        DropdownInline(ws, 6, "Si,No");
        DropdownInline(ws, 10, EnumCsv<EstadoEquipoActivo>());
        Ejemplo(ws, EjemploCopro, "Bomba de agua principal", "Bombeo", "Equipo", "1", "No", "BX-200", "SER-123",
            "Cuarto de bombas", "Operativo", "Revision mensual", "10", "5000000", "HidroServicios", "FAC-001");
        Ajustar(ws, cols.Count);
    }

    // ===================== Helpers de formato =====================
    private static IXLWorksheet Encabezado(XLWorkbook wb, string nombre, List<(string H, string Ayuda)> cols)
    {
        var ws = wb.AddWorksheet(nombre);
        var n = cols.Count;

        // Fila 1: banner de marca PROPIA.
        var banner = ws.Range(1, 1, 1, n).Merge();
        banner.Value = $"PROPIA   |   Carga masiva   |   {nombre}";
        banner.Style.Fill.BackgroundColor = Brand;
        banner.Style.Font.FontColor = XLColor.White;
        banner.Style.Font.Bold = true;
        banner.Style.Font.FontSize = 13;
        banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        banner.Style.Alignment.Indent = 1;
        ws.Row(1).Height = 26;

        // Fila 2: encabezados. Fila 3: ayuda.
        for (var i = 0; i < n; i++)
        {
            var c = ws.Cell(2, i + 1);
            c.Value = cols[i].H;
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = Ink;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            c.Style.Border.BottomBorderColor = Brand;

            var a = ws.Cell(3, i + 1);
            a.Value = cols[i].Ayuda;
            a.Style.Font.FontColor = BrandText;
            a.Style.Font.Italic = true;
            a.Style.Font.FontSize = 9;
            a.Style.Fill.BackgroundColor = Soft;
            // Ajuste de texto + alineacion arriba: la ayuda multilinea cabe en una fila de altura
            // FIJA (igual que el archivo guia) en vez de estirar la fila a un tamano enorme.
            a.Style.Alignment.WrapText = true;
            a.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }
        ws.Row(2).Height = 20;
        ws.Row(3).Height = 97.2;   // altura fija de la fila de informacion (guia: ht=97.2)
        ws.SheetView.FreezeRows(3);
        return ws;
    }

    private static void Dropdown(IXLWorksheet ws, int col, string? listFormula)
        => AplicarLista(ws, col, listFormula);

    private static void DropdownInline(IXLWorksheet ws, int col, string csv)
        => AplicarLista(ws, col, "\"" + csv + "\"");

    private static void AplicarLista(IXLWorksheet ws, int col, string? listFormula)
    {
        if (string.IsNullOrEmpty(listFormula)) return;   // sin lista -> columna libre (sin dropdown)
        var dv = ws.Range(DataStart, col, MaxRows, col).CreateDataValidation();
        dv.List(listFormula, true);
        dv.IgnoreBlanks = true;
        // Rechaza valores fuera de la lista y muestra ayuda al seleccionar la celda.
        dv.ErrorStyle = XLErrorStyle.Stop;
        dv.ShowErrorMessage = true;
        dv.ErrorTitle = "Valor no valido";
        dv.ErrorMessage = "Elige un valor de la lista desplegable.";
        dv.ShowInputMessage = true;
        dv.InputTitle = "Lista";
        dv.InputMessage = "Haz clic en la flecha y elige de la lista.";
    }

    private static void Ajustar(IXLWorksheet ws, int nCols)
    {
        for (var i = 1; i <= nCols; i++) ws.Column(i).Width = 18;
    }

    // Sentinel de la columna COPROPIEDAD en la fila de ejemplo: el importador ignora toda fila
    // cuya COPROPIEDAD empiece por "EJEMPLO". Asi la fila 4 sirve de guia y no se carga.
    private const string EjemploCopro = "EJEMPLO (borrar fila)";

    // Escribe la fila de ejemplo (fila 4) en gris/italica para que se lea como muestra.
    private static void Ejemplo(IXLWorksheet ws, params string[] valores)
    {
        var muted = XLColor.FromHtml("#9AA7B4");
        for (var i = 0; i < valores.Length; i++)
        {
            if (string.IsNullOrEmpty(valores[i])) continue;
            var c = ws.Cell(DataStart, i + 1);
            c.Value = valores[i];
            c.Style.Font.Italic = true;
            c.Style.Font.FontColor = muted;
        }
    }

    // CSV de los nombres de un enum, para las listas desplegables (coinciden con lo que parsea el importador).
    private static string EnumCsv<TEnum>() where TEnum : struct, Enum
        => string.Join(",", Enum.GetNames<TEnum>());

    // ===================== Referencia: copropiedades del cliente =====================
    private async Task<List<(Guid Id, string Nombre, string? Codigo)>> CopropiedadesDelClienteAsync(CancellationToken ct)
    {
        // Las copropiedades que administra la persona actual dentro de su organizacion
        // (via get_tenants_for_persona, SECURITY DEFINER). Nunca de otros tenants-cliente.
        var personaId = Guid.TryParse(_http.HttpContext?.User?.FindFirst("persona_id")?.Value, out var pid) ? pid : (Guid?)null;
        var ids = new List<Guid>();
        if (personaId is not null)
        {
            var conn = _db.Database.GetDbConnection();
            var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
            if (abiertaAqui) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT tenant_id FROM get_tenants_for_persona(@p)";
                var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = personaId.Value; cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
            }
            finally { if (abiertaAqui) await conn.CloseAsync(); }
        }
        if (ids.Count == 0 && _tenant.CurrentTenantId is { } curr) ids.Add(curr);

        var lista = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .OrderBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Nombre, t.CodigoCorto })
            .ToListAsync(ct);
        return lista.Select(x => (x.Id, x.Nombre, (string?)x.CodigoCorto)).ToList();
    }
}
