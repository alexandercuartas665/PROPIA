namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de la ZONA COMUN. Espejo de <c>UnidadCampoSistema</c>: los que el usuario
/// puede mostrar, ocultar, renombrar y reordenar por copropiedad desde Zonas &gt; Campos. Los
/// personalizados (EAV) viven en <c>zona_campos_personalizados</c> y NO entran aqui.
/// </summary>
public sealed record ZonaCampoSistema(
    string Clave,
    string Label,
    string Encabezado,
    string Ayuda,
    bool VisiblePorDefecto,
    bool EsLista = false,
    bool SiempreEnPlantilla = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema de la ZONA COMUN. Espejo exacto de <c>UnidadCamposSistema</c>
/// para la adopcion del gestor de campos compartido (Fase 1, doc 5.4/5.5). Los <c>Encabezado</c> son los
/// de la plantilla de carga de zonas (<c>PlantillasService.ZonasFijas</c>), para que un campo del sistema
/// visible salga en la plantilla y pueda importarse. El ORDEN es el orden por defecto de columnas.
///
/// Zonas SI tiene campos propios (EAV): endpoints estandar <c>zonas-campos</c>, <c>zonas-campos-valores</c>,
/// <c>zonas/{id}/campos-din</c> (GET), <c>zonas/{id}/campos/{defId}</c> (PUT), <c>zonas/{id}/campos</c> (POST),
/// todos en <c>MiCopropiedadController</c>. Solo falta, respecto al contrato 5.4, el alias de lectura
/// <c>{id}/campos</c> (hoy es <c>{id}/campos-din</c>): se reporta, no se toca (fuera de mi limite).
/// </summary>
public static class ZonaCamposSistema
{
    /// <summary>Categorias de fabrica (enum CategoriaZonaComun); la copropiedad puede ocultarlas.</summary>
    public static readonly string[] EstadosSemilla = { "Activa", "EnMantenimiento", "Inactiva" };

    public static readonly IReadOnlyList<ZonaCampoSistema> Todos = new[]
    {
        new ZonaCampoSistema("nombre", "Nombre", "Nombre *", "Nombre de la zona comun. Obligatorio",
            VisiblePorDefecto: true, SiempreEnPlantilla: true),
        new ZonaCampoSistema("categoria", "Categoria", "Categoria", "Elige de la lista (Social, Deportiva, ...)",
            VisiblePorDefecto: true, EsLista: true),
        new ZonaCampoSistema("reservable", "Reservable", "Reservable (Si/No)", "Si la zona se puede reservar (Si/No)",
            VisiblePorDefecto: true),
        new ZonaCampoSistema("aforo", "Aforo", "Aforo (personas)", "Capacidad de personas (entero)", false),
        new ZonaCampoSistema("estado", "Estado", "Estado", "Elige de la lista (Activa, EnMantenimiento, Inactiva)",
            VisiblePorDefecto: true, EsLista: true),
        new ZonaCampoSistema("descripcion", "Descripcion", "Descripcion", "Texto libre", false),
        new ZonaCampoSistema("tarifa", "Tarifa reserva", "Tarifa reserva", "Tarifa de reserva si aplica (numero)", false),
        new ZonaCampoSistema("reglas", "Reglas de uso", "Reglas de uso", "Texto libre", false),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static ZonaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public static ZonaCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
