using Propia.Domain.Enums;

namespace Propia.Application.Mantenimiento;

/// <summary>
/// Catalogo UNICO de los campos de SISTEMA de las INTERVENCIONES de mantenimiento (tabla canon).
/// Reusa el record <see cref="MantenimientoCampoSistema"/>. Clave de config con prefijo 'mantenimiento',
/// entidad 'intervencion' (unidades-config?entidad=intervencion). Los campos propios (EAV) viven en
/// intervencion_campos_definiciones/valores.
/// </summary>
public static class IntervencionCamposSistema
{
    public static readonly IReadOnlyList<MantenimientoCampoSistema> Todos = new[]
    {
        new MantenimientoCampoSistema("codigo", "Codigo", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("activo", "Activo", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("titulo", "Titulo", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true),
        new MantenimientoCampoSistema("estado", "Estado", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("prioridad", "Prioridad", TipoCampoTablero.Seleccion, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("proveedor", "Proveedor", TipoCampoTablero.Texto, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("programada", "Programada", TipoCampoTablero.Fecha, VisiblePorDefecto: true),
        new MantenimientoCampoSistema("cierre", "Cierre", TipoCampoTablero.Fecha, VisiblePorDefecto: false),
        new MantenimientoCampoSistema("tarea", "Tarea", TipoCampoTablero.Texto, VisiblePorDefecto: true),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static MantenimientoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
