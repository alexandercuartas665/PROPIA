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
        // Catalogos de campos dinamicos (definiciones POR COPROPIEDAD) de cada entidad: se emiten
        // como columnas [Label] al final de su hoja y el importador los lee y guarda.
        // TERCEROS no lleva columnas dinamicas a proposito: su catalogo (TerceroCamposDefiniciones)
        // es de las EMPLEADAS de una unidad, y esta hoja carga terceros del DIRECTORIO
        // (empresa/persona global + vinculo), que no crean un registro de empleada al que colgarle
        // el valor. Emitir columnas que nadie puede guardar solo repetiria el descarte silencioso.
        // Visibilidad por copropiedad de los campos de la unidad: la hoja UNIDADES PRIVADAS solo
        // emite las columnas ACTIVAS (los campos ocultos en la tabla de unidades no se piden).
        var cfgUnidad = await ConfigCamposUnidadAsync(ct);
        var defsUnidad = await _db.UnidadCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => new { c.Id, c.Label }).ToListAsync(ct);
        // Los campos dinamicos guardan su visibilidad con la clave "cd:{id}" de la definicion.
        var camposUnidad = defsUnidad.Where(d => cfgUnidad.Visible("cd:" + d.Id)).Select(d => d.Label).ToList();
        // Tipos de unidad PROPIOS de la copropiedad: van en el desplegable de TIPO junto a los del sistema.
        var tiposPropios = (await _db.TiposUnidadCustom.AsNoTracking()
                .OrderBy(t => t.Nombre).Select(t => new { t.Id, t.Nombre }).ToListAsync(ct))
            .Select(t => (t.Id, t.Nombre)).ToList();
        var camposPersona = await _db.PersonaCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);
        var camposVehiculo = await _db.VehiculoCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);
        var camposMascota = await _db.MascotaCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);
        var camposZona = await _db.ZonaCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);
        var camposEquipo = await _db.EquipoCamposDefiniciones.AsNoTracking()
            .OrderBy(c => c.Orden).ThenBy(c => c.Label).Select(c => c.Label).ToListAsync(ct);

        using var wb = new XLWorkbook();

        // Todas las listas de los desplegables viven en una hoja OCULTA y se referencian por nombre
        // definido: asi no hay tope de longitud ni problema con valores que traigan comas.
        var listas = new HojaListas(wb);
        var coproList = listas.Definir("COPROPIEDAD", copros.Select(c => c.Nombre));
        var rolesList = listas.Definir("ROL", roles);
        // Terceros: la lista arranca con "Todas las copropiedades" (visible en todas) + las del cliente.
        var coproTercerosList = listas.Definir("COPROPIEDAD_TERCEROS",
            new[] { TodasLasCopropiedades }.Concat(copros.Select(c => c.Nombre)));

        // ---- Hojas de datos ----
        HojaUnidades(wb, listas, coproList, camposUnidad, cfgUnidad, tiposPropios);
        HojaPersonas(wb, listas, coproList, rolesList, camposPersona);
        HojaVehiculos(wb, listas, coproList, camposVehiculo);
        HojaMascotas(wb, listas, coproList, camposMascota);
        HojaTerceros(wb, listas, coproTercerosList);
        HojaZonasComunes(wb, listas, coproList, camposZona);
        HojaEquipos(wb, listas, coproList, camposEquipo);
        listas.Cerrar();

        wb.Properties.Author = "PROPIA";
        wb.Properties.Company = "A&D GROUP S.A.S";
        wb.Properties.Title = "Plantilla de carga - Unidades privadas";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), "Plantilla carga unidades privadas.xlsx");
    }

    /// <summary>
    /// Hoja OCULTA con las listas de los desplegables. Cada lista ocupa una columna y se expone
    /// como NOMBRE DEFINIDO, que es lo que referencia la validacion de datos.
    ///
    /// Reemplaza a las listas "inline" (la formula "a,b,c" dentro de la propia validacion), que
    /// tienen dos limites de Excel: no admiten valores con coma y el texto completo no puede pasar
    /// de 255 caracteres. Al superarlos, la columna se quedaba SIN desplegable y el usuario tenia
    /// que adivinar los valores validos. Con la hoja de referencia no hay tope practico.
    ///
    /// Se referencia por nombre definido y no por "HOJA!$A$1:$A$9" porque la referencia directa a
    /// otra hoja en una validacion de datos no funciona en Excel 2007; el nombre si.
    /// </summary>
    private sealed class HojaListas
    {
        private const string NombreHoja = "PROPIA_LISTAS";

        private readonly XLWorkbook _wb;
        private readonly IXLWorksheet _ws;
        private readonly Dictionary<string, string?> _definidas = new(StringComparer.OrdinalIgnoreCase);
        private int _col;

        public HojaListas(XLWorkbook wb)
        {
            _wb = wb;
            _ws = wb.AddWorksheet(NombreHoja);
        }

        /// <summary>
        /// Escribe una lista en la hoja y devuelve la formula para la validacion ("=LISTA_TIPO").
        /// Null si la lista queda vacia: esa columna se deja libre en vez de poner un desplegable
        /// sin opciones, que bloquearia la captura.
        /// </summary>
        public string? Definir(string clave, IEnumerable<string> valores)
        {
            // Una misma clave se pide desde varias hojas (SI_NO, TIPO_ID): se escribe UNA vez y todas
            // apuntan al mismo nombre. Definirla dos veces reventaria por nombre duplicado.
            if (_definidas.TryGetValue(clave, out var ya)) return ya;

            var vals = valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
            if (vals.Count == 0)
            {
                _definidas[clave] = null;
                return null;
            }

            _col++;
            _ws.Cell(1, _col).Value = clave;                 // encabezado, solo para poder leer la hoja
            _ws.Cell(1, _col).Style.Font.Bold = true;
            for (var i = 0; i < vals.Count; i++) _ws.Cell(i + 2, _col).Value = vals[i];

            var nombre = "LISTA_" + Sanear(clave);
            _wb.DefinedNames.Add(nombre, _ws.Range(2, _col, vals.Count + 1, _col));
            var formula = "=" + nombre;
            _definidas[clave] = formula;
            return formula;
        }

        /// <summary>Manda la hoja al final y la oculta. Se llama cuando ya se definieron todas.</summary>
        public void Cerrar()
        {
            _ws.Columns().AdjustToContents();
            _ws.Position = _wb.Worksheets.Count;
            _ws.Hide();
        }

        // Un nombre definido de Excel solo admite letras, digitos y guion bajo, y no puede empezar
        // por digito. Las claves las damos nosotras, pero se sanean igual por si entra un alias.
        private static string Sanear(string clave)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in clave.ToUpperInvariant())
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            var s = sb.ToString();
            return s.Length > 0 && char.IsDigit(s[0]) ? "L" + s : s;
        }
    }

    // ===================== Hojas de datos =====================
    // La hoja de unidades solo trae las columnas de los campos ACTIVOS de la copropiedad. Cada
    // columna declara su clave en unidad_campos_config (entidad 'unidad'); Clave = null marca las
    // columnas ESTRUCTURALES, que nunca se filtran: COPROPIEDAD y UNIDAD PRIVADA son obligatorias
    // para el importador y PRINCIPAL es la que vincula un anexo con su unidad principal. Sin ellas
    // la plantilla quedaria inservible, asi que no dependen de la configuracion.
    private static void HojaUnidades(XLWorkbook wb, HojaListas listas, string? coproRange,
        List<string> camposUnidad, ConfigCamposUnidad cfg, List<(Guid Id, string Nombre)> tiposPropios)
    {
        // Las columnas salen del catalogo UNICO de campos de sistema (UnidadCamposSistema), el mismo
        // que alimenta la tabla de unidades. Antes esta lista estaba duplicada aqui y se quedo corta:
        // area, estado, piso, habitaciones, banos, parqueaderos, paga admin, cuota y observaciones se
        // podian activar en la copropiedad pero NO salian en la plantilla ni se podian importar.
        var cols = new List<(string H, string Ayuda)> { ("COPROPIEDAD", "Elige de la lista") };
        foreach (var campo in UnidadCamposSistema.Todos)
        {
            if (campo.SiempreEnPlantilla || cfg.Visible(campo.Clave))
                cols.Add((campo.Encabezado, AyudaDe(campo, cfg)));
            // PRINCIPAL va pegada a AGRUPACION y es estructural: es la que vincula un anexo con su
            // unidad principal, asi que se emite este oculta o no la agrupacion.
            if (campo.Clave == "agrupacion")
                cols.Add(("PRINCIPAL", "Si es Anexo (3): codigo de la unidad principal"));
        }
        AgregarColumnasDinamicas(cols, camposUnidad);   // ya vienen filtrados por visibilidad

        var ws = Encabezado(wb, "UNIDADES PRIVADAS", cols);
        // Dropdowns y ejemplo POR ENCABEZADO (no por posicion): al filtrar columnas los indices se
        // mueven, y una columna ausente simplemente no recibe nada.
        Dropdown(ws, Indice(cols, "COPROPIEDAD"), coproRange);
        // TIPO: los tipos que ofrece ESTA copropiedad (del sistema que no oculto + los propios), con
        // la misma etiqueta que muestra la app. Antes se volcaba el enum crudo: los tipos propios no
        // aparecian y, si el usuario los escribia a mano, se guardaban como Apartamento en silencio.
        Dropdown(ws, Indice(cols, "TIPO"), listas.Definir("TIPO_UNIDAD", cfg.OpcionesTipo(tiposPropios)));
        Dropdown(ws, Indice(cols, "AGRUPACION"), listas.Definir("AGRUPACION", new[] { "1", "2", "3" }));
        // ESTADO: las opciones que tenga configuradas la copropiedad (las ocultas no se ofrecen).
        Dropdown(ws, Indice(cols, "ESTADO"), listas.Definir("ESTADO_UNIDAD", cfg.OpcionesEstado()));
        Dropdown(ws, Indice(cols, "PAGA ADMIN"), listas.Definir("SI_NO", SiNo));
        Ejemplo(ws, cols,
            ("COPROPIEDAD", EjemploCopro), ("UNIDAD PRIVADA", "A1-203"),
            ("TIPO", "Apartamento"), ("AGRUPACION", "2"), ("COEFICIENTE", "1.25"));
        Ajustar(ws, cols.Count);
    }

    // Ayuda de la columna. Si la copropiedad renombro el campo, se dice en la misma linea: el
    // ENCABEZADO se mantiene canonico (es el contrato con el importador y hace que un archivo sirva
    // para varias copropiedades), pero el usuario necesita reconocer su campo por el nombre que le puso.
    private static string AyudaDe(UnidadCampoSistema campo, ConfigCamposUnidad cfg)
    {
        var alias = cfg.Alias(campo.Clave);
        return string.IsNullOrWhiteSpace(alias) || string.Equals(alias, campo.Encabezado, StringComparison.OrdinalIgnoreCase)
            ? campo.Ayuda
            : $"{campo.Ayuda}{(campo.Ayuda.Length > 0 ? " " : "")}(en esta copropiedad: {alias})";
    }

    private static void HojaPersonas(XLWorkbook wb, HojaListas listas, string? coproRange, string? rolesRange, List<string> camposPersona)
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
        AgregarColumnasDinamicas(cols, camposPersona);

        var ws = Encabezado(wb, "PERSONAS", cols);
        Dropdown(ws, 1, coproRange);
        Dropdown(ws, 3, listas.Definir("TIPO_RESIDENTE",
            new[] { "Propietario", "Residente", "Familiar", "Arrendatario", "Apoderado" }));
        Dropdown(ws, 4, listas.Definir("TIPO_ID", TiposId));
        Dropdown(ws, 9, listas.Definir("SEXO", new[] { "M", "F" }));
        Dropdown(ws, 12, rolesRange);
        Ejemplo(ws, EjemploCopro, "A1-203", "Propietario", "CC", "Juan Perez", "123456789", "juan@correo.com", "3001234567", "M", "1985-04-12", "Ingeniero", "");
        Ajustar(ws, cols.Count);
    }

    private static void HojaVehiculos(XLWorkbook wb, HojaListas listas, string? coproRange, List<string> camposVehiculo)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad"),
            ("TIPO DE VEHICULO", "Elige de la lista"),
            ("MARCA", ""), ("MODELO", ""), ("COLOR", ""), ("PLACA", ""),
        };
        AgregarColumnasDinamicas(cols, camposVehiculo);

        var ws = Encabezado(wb, "VEHICULOS", cols);
        Dropdown(ws, 1, coproRange);
        Dropdown(ws, 3, listas.Definir("TIPO_VEHICULO",
            new[] { "Automovil", "Moto", "Bicicleta", "Camioneta", "Otro" }));
        Ejemplo(ws, EjemploCopro, "A1-203", "Automovil", "Mazda", "2022", "Gris", "ABC123");
        Ajustar(ws, cols.Count);
    }

    private static void HojaMascotas(XLWorkbook wb, HojaListas listas, string? coproRange, List<string> camposMascota)
    {
        var cols = new List<(string H, string Ayuda)>
        {
            ("COPROPIEDAD", "Elige de la lista"),
            ("UNIDAD PRIVADA", "Codigo de la unidad"),
            ("TIPO MASCOTA", "Elige de la lista"),
            ("RAZA", ""), ("NOMBRE", ""),
        };
        AgregarColumnasDinamicas(cols, camposMascota);

        var ws = Encabezado(wb, "MASCOTAS", cols);
        Dropdown(ws, 1, coproRange);
        Dropdown(ws, 3, listas.Definir("TIPO_MASCOTA", new[] { "Perro", "Gato", "Ave", "Otro" }));
        Ejemplo(ws, EjemploCopro, "A1-203", "Perro", "Labrador", "Rocky");
        Ajustar(ws, cols.Count);
    }

    // Un tercero NO se relaciona con una unidad; solo con la copropiedad. Con "Todas las copropiedades"
    // el tercero queda visible en TODAS las copropiedades del cliente (se crea global + un vinculo en cada
    // una). Por eso no hay columnas ALCANCE ni UNIDAD PRIVADA.
    private static void HojaTerceros(XLWorkbook wb, HojaListas listas, string? coproTercerosRange)
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
        Dropdown(ws, 2, listas.Definir("TIPO_ID", TiposId));
        // El ejemplo usa EjemploCopro para que el importador lo omita; la ayuda ya explica "Todas...".
        Ejemplo(ws, EjemploCopro, "CC", "Maria Lopez", "987654321", "maria@correo.com", "3009876543");
        Ajustar(ws, cols.Count);
    }

    // ===================== Hojas nuevas: Zonas comunes y Equipos =====================
    private static void HojaZonasComunes(XLWorkbook wb, HojaListas listas, string? coproRange, List<string> camposZona)
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
        AgregarColumnasDinamicas(cols, camposZona);

        var ws = Encabezado(wb, "ZONAS COMUNES", cols);
        Dropdown(ws, 1, coproRange);
        Dropdown(ws, 3, listas.Definir("CATEGORIA_ZONA", EnumNombres<CategoriaZonaComun>()));
        Dropdown(ws, 4, listas.Definir("SI_NO", SiNo));
        Dropdown(ws, 6, listas.Definir("ESTADO_ZONA", EnumNombres<EstadoZonaComunMantenimiento>()));
        Ejemplo(ws, EjemploCopro, "Salon Social", "Social", "Si", "80", "Activa", "Salon para eventos", "50000", "Reservar con 3 dias");
        Ajustar(ws, cols.Count);
    }

    private static void HojaEquipos(XLWorkbook wb, HojaListas listas, string? coproRange, List<string> camposEquipo)
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
        AgregarColumnasDinamicas(cols, camposEquipo);

        var ws = Encabezado(wb, "EQUIPOS", cols);
        Dropdown(ws, 1, coproRange);
        Dropdown(ws, 3, listas.Definir("CATEGORIA_EQUIPO", EnumNombres<CategoriaEquipo>()));
        Dropdown(ws, 4, listas.Definir("TIPO_ELEMENTO", EnumNombres<TipoElemento>()));
        Dropdown(ws, 6, listas.Definir("SI_NO", SiNo));
        Dropdown(ws, 10, listas.Definir("ESTADO_EQUIPO", EnumNombres<EstadoEquipoActivo>()));
        Ejemplo(ws, EjemploCopro, "Bomba de agua principal", "Bombeo", "Equipo", "1", "No", "BX-200", "SER-123",
            "Cuarto de bombas", "Operativo", "Revision mensual", "10", "5000000", "HidroServicios", "FAC-001");
        Ajustar(ws, cols.Count);
    }

    // ===================== Helpers de formato =====================

    // Agrega AL FINAL de la hoja una columna por cada campo dinamico del catalogo de la
    // copropiedad, con el encabezado entre corchetes ("[N de medidor]"). Los corchetes son el
    // contrato con el importador: UnidadesCargaImportService reconoce ese encabezado, resuelve el
    // label contra el catalogo de la entidad y guarda el valor. Van al final para no mover los
    // indices de columna de los dropdowns ni de la fila de ejemplo (Ejemplo escribe solo el prefijo
    // de valores que recibe, asi que las columnas dinamicas quedan sin muestra).
    private static void AgregarColumnasDinamicas(List<(string H, string Ayuda)> cols, List<string> campos)
    {
        foreach (var lbl in campos) cols.Add(($"[{lbl}]", "Campo dinamico de la copropiedad"));
    }

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

    // Listas cortas que se repiten entre hojas. Se definen una vez en la hoja de listas y todas las
    // columnas apuntan al mismo nombre.
    private static readonly string[] SiNo = { "Si", "No" };
    private static readonly string[] TiposId = { "CC", "CE", "Pasaporte", "NIT", "Otro" };

    // Numero de columna (1-based) de un encabezado; 0 si la columna no se emitio (campo oculto).
    private static int Indice(List<(string H, string Ayuda)> cols, string encabezado)
        => cols.FindIndex(c => string.Equals(c.H, encabezado, StringComparison.OrdinalIgnoreCase)) + 1;

    private static void AplicarLista(IXLWorksheet ws, int col, string? listFormula)
    {
        if (col <= 0) return;                            // columna filtrada -> no hay nada que validar
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
            EjemploCelda(ws, i + 1, valores[i], muted);
        }
    }

    // Igual que la anterior pero direccionando POR ENCABEZADO: la usan las hojas cuyas columnas se
    // filtran por visibilidad, donde la posicion de cada columna no es fija. Un valor de un
    // encabezado que no se emitio simplemente se descarta (no desplaza al resto).
    private static void Ejemplo(IXLWorksheet ws, List<(string H, string Ayuda)> cols,
        params (string H, string V)[] valores)
    {
        var muted = XLColor.FromHtml("#9AA7B4");
        foreach (var (h, v) in valores)
        {
            if (string.IsNullOrEmpty(v)) continue;
            var col = Indice(cols, h);
            if (col <= 0) continue;
            EjemploCelda(ws, col, v, muted);
        }
    }

    private static void EjemploCelda(IXLWorksheet ws, int col, string valor, XLColor muted)
    {
        var c = ws.Cell(DataStart, col);
        c.Value = valor;
        c.Style.Font.Italic = true;
        c.Style.Font.FontColor = muted;
    }

    // Nombres de un enum, para las listas desplegables (coinciden con lo que parsea el importador).
    private static IEnumerable<string> EnumNombres<TEnum>() where TEnum : struct, Enum
        => Enum.GetNames<TEnum>();

    // ===================== Visibilidad de campos de la unidad =====================

    // Campos FIJOS de la unidad visibles cuando la copropiedad no tiene fila en unidad_campos_config.
    // Sale del catalogo unico (los 5 modulos contributivos, area, piso, etc. nacen OCULTOS), asi que
    // este servicio y la tabla de unidades no pueden discrepar sobre que se ve por defecto.
    private static readonly HashSet<string> CamposUnidadVisiblesPorDefecto =
        new(UnidadCamposSistema.ClavesVisiblesPorDefecto, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Configuracion de los campos de la unidad para ESTA copropiedad (unidad_campos_config,
    /// entidad 'unidad'): que se ve, como lo renombro el usuario y que opciones tiene una lista.
    /// Un campo SIN fila de config usa su default del catalogo.
    /// </summary>
    private sealed class ConfigCamposUnidad
    {
        private readonly Dictionary<string, (bool Oculto, string? Alias, string? Opciones)> _filas;

        public ConfigCamposUnidad(Dictionary<string, (bool, string?, string?)> filas) => _filas = filas;

        public bool Visible(string clave)
        {
            if (_filas.TryGetValue(clave, out var f)) return !f.Oculto;
            // Sin config: los campos personalizados ("cd:{id}") entran visibles (igual que en la
            // tabla de unidades) y de los fijos solo los que nacen visibles.
            return clave.StartsWith("cd:", StringComparison.OrdinalIgnoreCase)
                || CamposUnidadVisiblesPorDefecto.Contains(clave);
        }

        public string? Alias(string clave)
            => _filas.TryGetValue(clave, out var f) ? f.Alias : null;

        /// <summary>Opciones VISIBLES del campo ESTADO, en su orden; si no hay config, las de fabrica.</summary>
        public IEnumerable<string> OpcionesEstado()
        {
            var raw = _filas.TryGetValue("estado", out var f) ? f.Opciones : null;
            var guardadas = LeerOpciones(raw);
            return guardadas.Count == 0 ? UnidadCamposSistema.EstadosSemilla : guardadas;
        }

        /// <summary>
        /// Opciones del campo TIPO para esta copropiedad: los tipos del sistema que no haya ocultado
        /// mas sus tipos PROPIOS. Se emiten con la ETIQUETA que ve el usuario en la app (no el nombre
        /// crudo del enum), que es lo que el importador sabe resolver.
        /// La configuracion guarda las ocultas con clave "e:{Enum}" (del sistema) y "c:{guid}" (propio).
        /// </summary>
        public IEnumerable<string> OpcionesTipo(IEnumerable<(Guid Id, string Nombre)> propios)
        {
            var raw = _filas.TryGetValue("tipo", out var f) ? f.Opciones : null;
            var ocultas = OpcionesOcultas(raw);
            foreach (var (tipo, etiqueta) in TiposUnidadCatalogo.Todos)
                if (!ocultas.Contains("e:" + tipo)) yield return etiqueta;
            foreach (var (id, nombre) in propios)
                if (!ocultas.Contains("c:" + id) && !string.IsNullOrWhiteSpace(nombre)) yield return nombre.Trim();
        }

        // Claves de opciones marcadas como OCULTAS (para las listas cuyo universo no vive en la
        // config, como TIPO: ahi lo guardado solo dice que se oculto y en que orden).
        private static HashSet<string> OpcionesOcultas(string? raw)
        {
            var res = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('[')) return res;
            try
            {
                var ops = System.Text.Json.JsonSerializer.Deserialize<List<OpcionListaJson>>(raw);
                foreach (var o in ops ?? new()) if (o.Oculta && !string.IsNullOrWhiteSpace(o.K)) res.Add(o.K.Trim());
            }
            catch { /* config corrupta: no se oculta nada */ }
            return res;
        }

        // Mismo formato que escribe el panel de Configurar: JSON [{"K":"...","Oculta":false}].
        // Tolera el formato legado (una opcion por linea) para las copropiedades configuradas antes.
        private static List<string> LeerOpciones(string? raw)
        {
            var res = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return res;
            var t = raw.TrimStart();
            if (t.StartsWith('['))
            {
                try
                {
                    var ops = System.Text.Json.JsonSerializer.Deserialize<List<OpcionListaJson>>(raw);
                    foreach (var o in ops ?? new()) if (!o.Oculta && !string.IsNullOrWhiteSpace(o.K)) res.Add(o.K.Trim());
                }
                catch { /* config corrupta: se cae a las opciones de fabrica */ }
                return res;
            }
            foreach (var l in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                res.Add(l);
            return res;
        }

        private sealed record OpcionListaJson(string K, bool Oculta = false);
    }

    private async Task<ConfigCamposUnidad> ConfigCamposUnidadAsync(CancellationToken ct)
    {
        var filas = await _db.UnidadCamposConfig.AsNoTracking()
            .Where(c => c.Entidad == "unidad")
            .Select(c => new { c.CampoClave, c.Oculto, c.Alias, c.Opciones })
            .ToListAsync(ct);
        var map = new Dictionary<string, (bool, string?, string?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas) map[(f.CampoClave ?? "").Trim()] = (f.Oculto, f.Alias, f.Opciones);
        return new ConfigCamposUnidad(map);
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
