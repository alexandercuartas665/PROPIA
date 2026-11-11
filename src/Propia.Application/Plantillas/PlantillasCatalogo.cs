namespace Propia.Application.Plantillas;

/// <summary>
/// Catalogo (en codigo) de las categorias de plantillas de documentos y de los datos {grupo.campo} que cada
/// una puede insertar. Fuente: replica/js/mod-plantillas-doc.js (CATS, TOKENS_COMUNES, TOKENS_CAT), copiado
/// tal cual. La categoria define (1) que datos ofrece el editor (comunes + los suyos) y (2) en que modulo
/// se ofrece la plantilla ("Se ofrece en"). El Color es un color de DATO (chip pastel), no de diseno: se
/// pinta inline con PaletaChips.TextoSobre, igual que las opciones de lista.
/// </summary>
public static class PlantillasCatalogo
{
    public const string Pqrsd = "pqrsd";

    public static readonly IReadOnlyList<PlantillaCategoriaDto> Categorias = new[]
    {
        new PlantillaCategoriaDto("pqrsd", "PQRSD", "fi-rr-comments-question", "#C9B8FF", "Responder PQRSD"),
        new PlantillaCategoriaDto("cartera", "Cartera", "fi-rr-credit-card", "#AEE6C4", "Cartera: paz y salvo, cobro y acuerdos de pago"),
        new PlantillaCategoriaDto("asambleas", "Asambleas", "fi-rr-podium-star", "#FFD8A8", "Asambleas: convocatoria, poderes y actas"),
        new PlantillaCategoriaDto("comunicaciones", "Comunicaciones", "fi-rr-megaphone", "#AAD4FF", "Nuevo comunicado"),
        new PlantillaCategoriaDto("contratos", "Contratos", "fi-rr-file-signature", "#FFE39B", "Ficha del contrato"),
        new PlantillaCategoriaDto("reservas", "Reservas", "fi-rr-calendar-check", "#FFC4D6", "Confirmación de reserva"),
        new PlantillaCategoriaDto("certificados", "Certificados", "fi-rr-diploma", "#CFE0EF", "Residentes y Unidades: certificados"),
    };

    /// <summary>Categoria por clave; si no existe devuelve la primera (PQRSD), como cat() de la replica.</summary>
    public static PlantillaCategoriaDto Categoria(string? clave)
        => Categorias.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase)) ?? Categorias[0];

    public static bool EsCategoriaValida(string? clave)
        => Categorias.Any(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    private static PlantillaTokenDto[] Tk(bool comun, params (string Campo, string Desc, string Ej)[] lista)
        => lista.Select(t => new PlantillaTokenDto("{" + t.Campo + "}", t.Desc, t.Ej, comun)).ToArray();

    /// <summary>Datos que toda plantilla puede usar (copropiedad + fecha + quien genera el documento).</summary>
    public static readonly IReadOnlyList<PlantillaTokenDto> TokensComunes = Tk(true,
        ("copropiedad.nombre", "Nombre de la copropiedad", "Conjunto Altos del Bosque"),
        ("copropiedad.nit", "NIT de la copropiedad", "811654321-1"),
        ("copropiedad.direccion", "Direccion de la copropiedad", "Calle 11 # 21-31"),
        ("copropiedad.ciudad", "Ciudad", "Medellín"),
        ("administrador.nombre", "Nombre del administrador", "Admin Delegado Demo"),
        ("gestor.nombre", "Quien genera el documento (usuario actual)", "Admin Delegado Demo"),
        ("fecha.hoy", "Fecha de hoy", "10 de noviembre de 2026"));

    /// <summary>Datos propios de cada categoria. PQRSD = los que resuelve PqrsdService.ResolverPlantillaAsync.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<PlantillaTokenDto>> TokensPorCategoria =
        new Dictionary<string, IReadOnlyList<PlantillaTokenDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pqrsd"] = Tk(false,
                ("radicado.numero", "Numero de radicado", "PQR-2026-0042"), ("radicado.tipo", "Tipo de PQRSD", "Petición"),
                ("radicado.categoria", "Categoria", "Convivencia"), ("radicado.fecha", "Fecha de radicacion", "3 nov 2026"),
                ("radicado.estado", "Estado actual", "En gestión"), ("solicitante.nombre", "Nombre del solicitante", "Laura Restrepo Gómez"),
                ("solicitante.identificacion", "Identificacion del solicitante", "CC 43100200"), ("solicitante.correo", "Correo del solicitante", "laura.restrepo@correo.co"),
                ("solicitante.telefono", "Telefono del solicitante", "3001200000"), ("unidad.numero", "Numero de la unidad", "T1-101"),
                ("unidad.torre", "Torre/bloque de la unidad", "Torre 1"), ("unidad.propietario", "Propietario de la unidad", "Laura Restrepo Gómez")),
            ["cartera"] = Tk(false,
                ("unidad.numero", "Numero de la unidad", "T2-304"), ("unidad.torre", "Torre/bloque de la unidad", "Torre 2"),
                ("propietario.nombre", "Propietario", "Andrés Mejía Ochoa"), ("propietario.identificacion", "Identificacion del propietario", "CC 43107511"),
                ("deuda.total", "Saldo total adeudado", "$ 1.845.000"), ("deuda.capital", "Capital", "$ 1.620.000"),
                ("deuda.intereses", "Intereses de mora", "$ 225.000"), ("deuda.dias_mora", "Dias en mora", "95"),
                ("cuota.valor", "Cuota mensual de administracion", "$ 265.556"), ("fecha.corte", "Fecha de corte", "31 oct 2026")),
            ["asambleas"] = Tk(false,
                ("asamblea.tipo", "Tipo de sesion", "Asamblea Ordinaria"), ("asamblea.fecha", "Fecha de la sesion", "15 mar 2027"),
                ("asamblea.hora", "Hora", "7:00 p. m."), ("asamblea.lugar", "Lugar", "Salón social"),
                ("asamblea.enlace", "Enlace de videoconferencia", "https://meet.google.com/abc-defg-hij"),
                ("asamblea.orden_del_dia", "Orden del dia", "1. Verificación del quórum 2. Informe de gestión 3. Aprobación del presupuesto"),
                ("unidad.numero", "Numero de la unidad", "T3-202"), ("propietario.nombre", "Propietario", "Carolina Vásquez Ríos")),
            ["comunicaciones"] = Tk(false,
                ("destinatario.nombre", "Nombre del destinatario", "Juan Pablo Arango Toro"), ("unidad.numero", "Numero de la unidad", "T1-402"),
                ("comunicado.asunto", "Asunto del comunicado", "Mantenimiento de ascensores"), ("comunicado.fecha", "Fecha del comunicado", "10 nov 2026")),
            ["contratos"] = Tk(false,
                ("contrato.numero", "Numero del contrato", "CTR-2026-014"), ("contrato.objeto", "Objeto del contrato", "Servicio de aseo de zonas comunes"),
                ("contratista.nombre", "Contratista", "Aseo Total SAS"), ("contratista.nit", "NIT del contratista", "900456789"),
                ("contrato.valor", "Valor del contrato", "$ 36.000.000"), ("contrato.inicio", "Fecha de inicio", "1 ene 2026"), ("contrato.fin", "Fecha de finalizacion", "31 dic 2026")),
            ["reservas"] = Tk(false,
                ("reserva.zona", "Zona reservada", "Salón social"), ("reserva.fecha", "Fecha de la reserva", "22 nov 2026"),
                ("reserva.horario", "Horario", "2:00 p. m. a 6:00 p. m."), ("residente.nombre", "Quien reserva", "Marcela Gutiérrez Peña"),
                ("unidad.numero", "Numero de la unidad", "T2-101"), ("reserva.valor", "Valor de la reserva", "$ 80.000")),
            ["certificados"] = Tk(false,
                ("residente.nombre", "Nombre del residente", "Santiago Londoño Díaz"), ("residente.identificacion", "Identificacion del residente", "CC 43136755"),
                ("unidad.numero", "Numero de la unidad", "T3-104"), ("unidad.torre", "Torre/bloque", "Torre 3"),
                ("residente.relacion", "Relacion con la unidad", "Propietario"), ("residente.desde", "Reside desde", "12 feb 2021")),
        };

    /// <summary>Propios de la categoria + comunes (lo que ofrece el panel "Datos para reemplazo").</summary>
    public static IReadOnlyList<PlantillaTokenDto> TokensDe(string? categoria)
    {
        var propios = TokensPorCategoria.TryGetValue(Categoria(categoria).Clave, out var l) ? l : Array.Empty<PlantillaTokenDto>();
        return propios.Concat(TokensComunes).ToList();
    }

    /// <summary>Cuantos datos {grupo.campo} distintos usa el cuerpo (columna "Datos que usa").</summary>
    public static int DatosQueUsa(string? cuerpoHtml)
        => System.Text.RegularExpressions.Regex.Matches(cuerpoHtml ?? "", @"\{[a-z_]+\.[a-z_]+\}")
            .Select(m => m.Value).Distinct(StringComparer.Ordinal).Count();
}
