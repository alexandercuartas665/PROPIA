namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) del EQUIPO / ACTIVO. Espejo de <c>UnidadCampoSistema</c>: los que el
/// usuario puede mostrar, ocultar, renombrar y reordenar por copropiedad desde Equipos &gt; Campos. Los
/// personalizados (EAV) viven en <c>equipo_campos_personalizados</c> y NO entran aqui.
/// </summary>
public sealed record EquipoCampoSistema(
    string Clave,
    string Label,
    string Encabezado,
    string Ayuda,
    bool VisiblePorDefecto,
    bool EsLista = false,
    bool SiempreEnPlantilla = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema del EQUIPO / ACTIVO. Espejo exacto de
/// <c>UnidadCamposSistema</c> para la adopcion del gestor de campos compartido (Fase 1, doc 5.4/5.5).
/// Los primeros <c>Encabezado</c> son los de la plantilla de carga de equipos
/// (<c>PlantillasService.EquiposFijas</c>); los ultimos son campos de la ficha tecnica de
/// <c>EquipoActivo</c> que no estan en la plantilla (SiempreEnPlantilla=false, VisiblePorDefecto=false).
/// El ORDEN es el orden por defecto de columnas.
///
/// Equipos SI tiene campos propios (EAV): endpoints estandar <c>equipos-campos</c>,
/// <c>equipos-campos-valores</c>, <c>equipos/{id}/campos-din</c> (GET), <c>equipos/{id}/campos/{defId}</c>
/// (PUT), <c>equipos/{id}/campos</c> (POST) en <c>MiCopropiedadController</c>. Igual que zonas, solo falta
/// el alias de lectura <c>{id}/campos</c> (hoy <c>{id}/campos-din</c>): se reporta, no se toca.
/// </summary>
public static class EquipoCamposSistema
{
    public static readonly IReadOnlyList<EquipoCampoSistema> Todos = new[]
    {
        new EquipoCampoSistema("nombre", "Nombre", "Nombre *", "Nombre del equipo o activo. Obligatorio",
            VisiblePorDefecto: true, SiempreEnPlantilla: true),
        new EquipoCampoSistema("categoria", "Categoria", "Categoria", "Elige de la lista", VisiblePorDefecto: true, EsLista: true),
        new EquipoCampoSistema("tipo", "Tipo", "Tipo (Equipo/Activo)", "Equipo (unitario) o Activo (agrupado)", VisiblePorDefecto: true, EsLista: true),
        new EquipoCampoSistema("cantidad", "Cantidad", "Cantidad", "Para activos agrupados: cuantos (entero)", false),
        new EquipoCampoSistema("reservable", "Reservable", "Reservable (Si/No)", "Si el activo se puede prestar/reservar (Si/No)", false),
        new EquipoCampoSistema("modelo", "Modelo", "Modelo", "Modelo del equipo", false),
        new EquipoCampoSistema("serie", "Numero de serie", "Numero de serie", "Numero de serie", false),
        new EquipoCampoSistema("ubicacion", "Ubicacion", "Ubicacion", "Donde esta ubicado", VisiblePorDefecto: true),
        new EquipoCampoSistema("estado", "Estado", "Estado", "Elige de la lista (Operativo, EnMantenimiento, FueraDeServicio)", VisiblePorDefecto: true, EsLista: true),
        new EquipoCampoSistema("observaciones", "Observaciones", "Observaciones", "Texto libre", false),
        new EquipoCampoSistema("vidautil", "Vida util (anios)", "Vida util (anios)", "Vida util estimada en anios (entero)", false),
        new EquipoCampoSistema("valoradq", "Valor adquisicion", "Valor adquisicion", "Valor de compra (numero)", false),
        new EquipoCampoSistema("proveedor", "Proveedor", "Proveedor", "Proveedor al que se compro", false),
        new EquipoCampoSistema("numfactura", "Numero factura", "Numero factura", "Numero de factura de compra", false),
        // Campos de la ficha tecnica que NO estan en la plantilla de carga (solo en la ficha del equipo).
        new EquipoCampoSistema("marca", "Marca", "Marca", "Marca del equipo", false),
        new EquipoCampoSistema("codigobarra", "Codigo de barra", "Codigo de barra", "Codigo de barras / etiqueta de inventario", false),
        new EquipoCampoSistema("fechainstalacion", "Fecha instalacion", "Fecha instalacion", "Fecha de instalacion", false),
        new EquipoCampoSistema("garantiahasta", "Garantia hasta", "Garantia hasta", "Fecha de fin de garantia", false),
        new EquipoCampoSistema("fechaadquisicion", "Fecha adquisicion", "Fecha adquisicion", "Fecha de compra", false),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static EquipoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public static EquipoCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
