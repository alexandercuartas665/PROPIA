using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Genera la plantilla Excel de carga masiva de unidades privadas (y personas/vehiculos/mascotas/
/// terceros). Trae los IDs de las copropiedades del cliente y catalogos como datos de referencia,
/// y aplica listas desplegables (validacion de datos) para forzar valores validos del sistema.
/// </summary>
public sealed class UnidadesPlantillaService : IUnidadesPlantillaService
{
    private const int DataStart = 3;      // fila 1 = encabezado, fila 2 = ayuda, datos desde la 3
    private const int MaxRows = 1000;     // hasta donde se aplican los dropdowns

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

        // ---- Hoja de referencia (datos + rangos para dropdowns) ----
        var wsRef = wb.AddWorksheet("DATOS DE CARGA");
        var (coproRange, rolesRange) = ConstruirReferencia(wsRef, copros, roles);

        // ---- Hojas de datos ----
        HojaUnidades(wb, coproRange, camposUnidad);
        HojaPersonas(wb, coproRange, rolesRange);
        HojaVehiculos(wb, coproRange);
        HojaMascotas(wb, coproRange);
        HojaTerceros(wb, coproRange);

        wsRef.SheetView.FreezeRows(3);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), "Plantilla carga unidades privadas.xlsx");
    }

    // ===================== Hoja de referencia =====================
    private static (string CoproRange, string RolesRange) ConstruirReferencia(
        IXLWorksheet ws, List<(Guid Id, string Nombre, string? Codigo)> copros, List<string> roles)
    {
        ws.Cell(1, 1).Value = "DATOS DE CARGA - REFERENCIA";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Usa la columna COPROPIEDAD (por nombre) en cada hoja. Aqui ves su ID y codigo. Las listas fuerzan valores validos.";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        // Copropiedades del cliente: Nombre | Codigo | ID
        ws.Cell(4, 1).Value = "COPROPIEDAD (nombre)";
        ws.Cell(4, 2).Value = "CODIGO";
        ws.Cell(4, 3).Value = "ID (uuid)";
        for (var i = 0; i < 3; i++) ws.Cell(4, i + 1).Style.Font.Bold = true;
        var r = 5;
        foreach (var c in copros)
        {
            ws.Cell(r, 1).Value = c.Nombre;
            ws.Cell(r, 2).Value = c.Codigo ?? "";
            ws.Cell(r, 3).Value = c.Id.ToString();
            r++;
        }
        var coproLast = Math.Max(5, r - 1);
        var coproRange = $"'DATOS DE CARGA'!$A$5:$A${coproLast}";

        // Roles del sistema (para ROLL)
        ws.Cell(4, 5).Value = "ROL DEL SISTEMA";
        ws.Cell(4, 5).Style.Font.Bold = true;
        var rr = 5;
        foreach (var rol in roles) ws.Cell(rr++, 5).Value = rol;
        var rolesLast = Math.Max(5, rr - 1);
        var rolesRange = $"'DATOS DE CARGA'!$E$5:$E${rolesLast}";

        ws.Columns(1, 5).AdjustToContents();
        return (coproRange, rolesRange);
    }

    // ===================== Hojas de datos =====================
    private static void HojaUnidades(XLWorkbook wb, string coproRange, List<string> camposUnidad)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad (ej. A1101)"),
            ("TIPO", "Elige de la lista"),
            ("AGRUPACION", "1=Individual, 2=Principal, 3=Anexo"),
            ("PRINCIPAL", "Si es Anexo (3): codigo de la unidad principal"),
            ("MATRICULA", "Matricula inmobiliaria"),
            ("COEFICIENTE", "Porcentaje. Max 5 decimales"),
            ("REF PAGO", "Referencia de pago (alfanumerica)"),
        };
        foreach (var lbl in camposUnidad) cols.Add(($"[{lbl}]", "Campo dinamico de la copropiedad"));

        var ws = Encabezado(wb, "UNIDADES PRIVADAS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 3, "Apartamento,Local,Casa,Oficina,Bodega,Parqueadero,UtilCuarto");
        DropdownInline(ws, 4, "1,2,3");
        Ajustar(ws, cols.Count);
    }

    private static void HojaPersonas(XLWorkbook wb, string coproRange, string rolesRange)
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
        Ajustar(ws, cols.Count);
    }

    private static void HojaVehiculos(XLWorkbook wb, string coproRange)
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
        Ajustar(ws, cols.Count);
    }

    private static void HojaMascotas(XLWorkbook wb, string coproRange)
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
        Ajustar(ws, cols.Count);
    }

    private static void HojaTerceros(XLWorkbook wb, string coproRange)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("ALCANCE", "TODAS = todas las unidades; ESPECIFICA = una"),
            ("UNIDAD PRIVADA", "Solo si ALCANCE = ESPECIFICA"),
            ("TIPO ID", "Elige de la lista"),
            ("NOMBRE", "Nombre completo / razon social"),
            ("IDENTIFICACION", "Documento/NIT"),
            ("EMAIL", ""), ("TELEFONO", ""),
        };
        var ws = Encabezado(wb, "TERCEROS", cols);
        Dropdown(ws, 1, coproRange);
        DropdownInline(ws, 2, "TODAS,ESPECIFICA");
        DropdownInline(ws, 4, "CC,CE,Pasaporte,NIT,Otro");
        Ajustar(ws, cols.Count);
    }

    // ===================== Helpers de formato =====================
    private static IXLWorksheet Encabezado(XLWorkbook wb, string nombre, List<(string H, string Ayuda)> cols)
    {
        var ws = wb.AddWorksheet(nombre);
        for (var i = 0; i < cols.Count; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = cols[i].H;
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B2A3A");
            c.Style.Font.FontColor = XLColor.White;
            var a = ws.Cell(2, i + 1);
            a.Value = cols[i].Ayuda;
            a.Style.Font.FontColor = XLColor.Gray;
            a.Style.Font.Italic = true;
        }
        ws.SheetView.FreezeRows(2);
        return ws;
    }

    private static void Dropdown(IXLWorksheet ws, int col, string rangeFormula)
    {
        var dv = ws.Range(DataStart, col, MaxRows, col).CreateDataValidation();
        dv.List(rangeFormula, true);
        dv.IgnoreBlanks = true;
    }

    private static void DropdownInline(IXLWorksheet ws, int col, string csv)
    {
        var dv = ws.Range(DataStart, col, MaxRows, col).CreateDataValidation();
        dv.List("\"" + csv + "\"", true);
        dv.IgnoreBlanks = true;
    }

    private static void Ajustar(IXLWorksheet ws, int nCols)
    {
        for (var i = 1; i <= nCols; i++) ws.Column(i).Width = 18;
    }

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
