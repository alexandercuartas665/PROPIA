using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) de la ZONA COMUN. Forma canonica del catalogo (VIGIA 2026-09-13, comun a
/// todos los agentes). <c>EsLista</c> se deriva de <c>Tipo == Seleccion</c>. Los campos propios (EAV)
/// viven en <c>zona_campos_personalizados</c> y NO entran aqui.
/// </summary>
public sealed record ZonaCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool SiempreEnPlantilla = false,
    string? Encabezado = null,
    string? Ayuda = null);

/// <summary>
/// Catalogo UNICO de los campos de sistema de la ZONA COMUN (preparacion Fase 1, doc 5.4/5.5). Los
/// <c>Encabezado</c> son los de la plantilla de carga de zonas (<c>PlantillasService.ZonasFijas</c>): un
/// campo del sistema visible sale en la plantilla y puede importarse. El ORDEN es el orden por defecto.
///
/// Zonas SI tiene campos propios (EAV): endpoints estandar <c>zonas-campos</c>, <c>zonas-campos-valores</c>,
/// <c>zonas/{id}/campos-din</c> (GET), <c>zonas/{id}/campos/{defId}</c> (PUT), <c>zonas/{id}/campos</c>
/// (POST) en <c>MiCopropiedadController</c>. Respecto al contrato 5.4 solo falta el alias de lectura
/// <c>{id}/campos</c> (hoy <c>{id}/campos-din</c>): se reporta, no se toca (fuera de mi limite).
/// </summary>
public static class ZonaCamposSistema
{
    public static readonly IReadOnlyList<ZonaCampoSistema> Todos = new[]
    {
        new ZonaCampoSistema("nombre", "Nombre", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true,
            SiempreEnPlantilla: true, Encabezado: "Nombre *", Ayuda: "Nombre de la zona comun. Obligatorio"),
        new ZonaCampoSistema("categoria", "Categoria", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "Categoria", Ayuda: "Elige de la lista (Social, Deportiva, ...)"),
        new ZonaCampoSistema("reservable", "Reservable", TipoCampoTablero.Booleano, VisiblePorDefecto: true,
            Encabezado: "Reservable (Si/No)", Ayuda: "Si la zona se puede reservar (Si/No)"),
        new ZonaCampoSistema("aforo", "Aforo", TipoCampoTablero.Numero, VisiblePorDefecto: false,
            Encabezado: "Aforo (personas)", Ayuda: "Capacidad de personas (entero)"),
        new ZonaCampoSistema("estado", "Estado", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "Estado", Ayuda: "Elige de la lista (Activa, EnMantenimiento, Inactiva)"),
        new ZonaCampoSistema("descripcion", "Descripcion", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false,
            Encabezado: "Descripcion", Ayuda: "Texto libre"),
        new ZonaCampoSistema("tarifa", "Tarifa reserva", TipoCampoTablero.Moneda, VisiblePorDefecto: false,
            Encabezado: "Tarifa reserva", Ayuda: "Tarifa de reserva si aplica (numero)"),
        new ZonaCampoSistema("reglas", "Reglas de uso", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false,
            Encabezado: "Reglas de uso", Ayuda: "Texto libre"),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static ZonaCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public static ZonaCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => c.Encabezado is not null
            && string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
