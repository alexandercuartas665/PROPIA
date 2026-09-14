namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de una PERSONA (residente) en la carga masiva / gestor de campos. Misma
/// forma que <see cref="UnidadCampoSistema"/> (Encabezado/Ayuda/SiempreEnPlantilla), para que la hoja de
/// PERSONAS de la plantilla se genere config-driven igual que la de UNIDADES (homogenizacion Fase 1,
/// decision de Alex 2026-09-14). Los campos PROPIOS de persona viven en su EAV y NO entran aqui.
///
/// OJO (persona es entidad GLOBAL + asociacion unidad_personas): la mayoria de columnas escriben en la
/// persona global (nombre/documento/email/telefono/genero/fechanac), pero TIPO RESIDENTE (rol) vive en la
/// ASOCIACION unidad_personas. El importador escribe cada columna en su tabla y valida que la persona
/// pertenezca al tenant. Este catalogo solo declara la VISTA/columnas y su orden por defecto.
/// </summary>
/// <param name="Clave">Clave de configuracion en unidad_campos_config (entidad 'personas').</param>
/// <param name="Label">Nombre de fabrica (antes del alias del tenant).</param>
/// <param name="Encabezado">Encabezado CANONICO de la columna en la plantilla (contrato del importador).</param>
/// <param name="Ayuda">Fila de ayuda de la plantilla.</param>
/// <param name="VisiblePorDefecto">Si se muestra cuando la copropiedad no configuro nada.</param>
/// <param name="EsLista">La columna trae un desplegable (TIPO RESIDENTE, TIPO ID, SEXO).</param>
/// <param name="SiempreEnPlantilla">Columna estructural (identidad/clave): la plantilla la emite aunque
/// este oculta y no se puede ocultar. NOMBRE (identidad) e IDENTIFICACION (clave) lo son.</param>
public sealed record PersonaCampoSistema(
    string Clave,
    string Label,
    string Encabezado,
    string Ayuda,
    bool VisiblePorDefecto,
    bool EsLista = false,
    bool SiempreEnPlantilla = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de una PERSONA/residente. El ORDEN es el orden por defecto de
/// las columnas de la hoja PERSONAS. Fuente de verdad de la generacion de la plantilla y del importador.
///
/// Decisiones (Alex 2026-09-14): PROFESION NO se incluye (la entidad Persona no tiene esa columna; agregarla
/// seria entrega aparte con migracion). El "rol de sistema/usuario" (ROLL) NO se incluye por SEGURIDAD: el
/// acceso/RBAC de una persona no se setea por carga masiva de Excel.
/// </summary>
public static class PersonaCamposSistema
{
    /// <summary>Tipos de residente de fabrica (columna TIPO RESIDENTE). La copropiedad puede ocultarlos.</summary>
    public static readonly string[] TiposResidenteSemilla =
        { "Propietario", "Residente", "Familiar", "Arrendatario", "Apoderado" };

    public static readonly IReadOnlyList<PersonaCampoSistema> Todos = new[]
    {
        // TIPO RESIDENTE: rol de la persona en la unidad (vive en unidad_personas). Lista.
        new PersonaCampoSistema("tiporesidente", "Tipo de residente", "TIPO RESIDENTE", "Elige de la lista",
            VisiblePorDefecto: true, EsLista: true),
        // TIPO ID: tipo de documento. Lista.
        new PersonaCampoSistema("tipoid", "Tipo de identificacion", "TIPO ID", "Elige de la lista",
            VisiblePorDefecto: true, EsLista: true),
        // NOMBRE: identidad de la fila -> estructural (no se puede ocultar).
        new PersonaCampoSistema("nombre", "Nombre", "NOMBRE", "Nombre completo (o razon social si NIT)",
            VisiblePorDefecto: true, SiempreEnPlantilla: true),
        // IDENTIFICACION (documento): clave -> estructural.
        new PersonaCampoSistema("identificacion", "Identificacion", "IDENTIFICACION", "Documento/NIT",
            VisiblePorDefecto: true, SiempreEnPlantilla: true),
        new PersonaCampoSistema("email", "Email", "EMAIL", "",
            VisiblePorDefecto: true),
        new PersonaCampoSistema("telefono", "Telefono", "TELEFONO", "",
            VisiblePorDefecto: true),
        // SEXO -> Persona.Genero (existe). Lista M/F. Visible por defecto: la plantilla historica siempre
        // emitia esta columna y aun no hay panel de config de Personas para reactivarla si se ocultara.
        new PersonaCampoSistema("genero", "Sexo", "SEXO", "M o F",
            VisiblePorDefecto: true, EsLista: true),
        // FECHA NACIMIENTO -> Persona.FechaNacimiento (existe). Visible por defecto por la misma razon.
        new PersonaCampoSistema("fechanacimiento", "Fecha de nacimiento", "FECHA NACIMIENTO", "AAAA-MM-DD",
            VisiblePorDefecto: true),
    };

    /// <summary>Claves visibles cuando la copropiedad no tiene configuracion.</summary>
    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static PersonaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>Encabezado canonico de plantilla -> campo (lo usa el importador para resolver la columna).</summary>
    public static PersonaCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
