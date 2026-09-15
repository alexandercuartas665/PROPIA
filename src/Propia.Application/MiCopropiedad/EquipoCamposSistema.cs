using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Un campo FIJO (del sistema) del EQUIPO / ACTIVO. Forma canonica del catalogo (VIGIA 2026-09-13, comun a
/// todos los agentes). <c>EsLista</c> se deriva de <c>Tipo == Seleccion</c>. Los campos propios (EAV) viven
/// en <c>equipo_campos_personalizados</c> y NO entran aqui.
/// </summary>
public sealed record EquipoCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false,
    bool SiempreEnPlantilla = false,
    string? Encabezado = null,
    string? Ayuda = null,
    // Solo Tipo=Seleccion: true = opciones configurables por copropiedad; false = enum fijo, read-only.
    // categoria, tipo y estado de equipo salen de enums: false en todas.
    bool OpcionesEditables = false);

/// <summary>
/// Catalogo UNICO de los campos de sistema del EQUIPO / ACTIVO (preparacion Fase 1, doc 5.4/5.5). Los
/// primeros <c>Encabezado</c> son los de la plantilla de carga (<c>PlantillasService.EquiposFijas</c>); los
/// ultimos son campos de la ficha tecnica de <c>EquipoActivo</c> que no estan en la plantilla
/// (<c>Encabezado = null</c>, VisiblePorDefecto=false). El ORDEN es el orden por defecto.
///
/// Equipos SI tiene campos propios (EAV): endpoints estandar <c>equipos-campos</c>,
/// <c>equipos-campos-valores</c>, <c>equipos/{id}/campos-din</c> (GET), <c>equipos/{id}/campos/{defId}</c>
/// (PUT), <c>equipos/{id}/campos</c> (POST) en <c>MiCopropiedadController</c>. Igual que zonas, solo falta el
/// alias de lectura <c>{id}/campos</c> (hoy <c>{id}/campos-din</c>): se reporta, no se toca.
/// </summary>
public static class EquipoCamposSistema
{
    public static readonly IReadOnlyList<EquipoCampoSistema> Todos = new[]
    {
        new EquipoCampoSistema("nombre", "Nombre", TipoCampoTablero.Texto, VisiblePorDefecto: true, Fija: true,
            SiempreEnPlantilla: true, Encabezado: "Nombre *", Ayuda: "Nombre del equipo o activo. Obligatorio"),
        new EquipoCampoSistema("categoria", "Categoria", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "Categoria", Ayuda: "Elige de la lista"),
        new EquipoCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "Tipo (Equipo/Activo)", Ayuda: "Equipo (unitario) o Activo (agrupado)"),
        new EquipoCampoSistema("cantidad", "Cantidad", TipoCampoTablero.Numero, VisiblePorDefecto: false,
            Encabezado: "Cantidad", Ayuda: "Para activos agrupados: cuantos (entero)"),
        new EquipoCampoSistema("reservable", "Reservable", TipoCampoTablero.Booleano, VisiblePorDefecto: false,
            Encabezado: "Reservable (Si/No)", Ayuda: "Si el activo se puede prestar/reservar (Si/No)"),
        new EquipoCampoSistema("modelo", "Modelo", TipoCampoTablero.Texto, VisiblePorDefecto: false,
            Encabezado: "Modelo", Ayuda: "Modelo del equipo"),
        new EquipoCampoSistema("serie", "Numero de serie", TipoCampoTablero.Texto, VisiblePorDefecto: false,
            Encabezado: "Numero de serie", Ayuda: "Numero de serie"),
        new EquipoCampoSistema("ubicacion", "Ubicacion", TipoCampoTablero.Texto, VisiblePorDefecto: true,
            Encabezado: "Ubicacion", Ayuda: "Donde esta ubicado"),
        new EquipoCampoSistema("estado", "Estado", TipoCampoTablero.Seleccion, VisiblePorDefecto: true,
            Encabezado: "Estado", Ayuda: "Elige de la lista (Operativo, EnMantenimiento, FueraDeServicio)"),
        new EquipoCampoSistema("observaciones", "Observaciones", TipoCampoTablero.AreaTexto, VisiblePorDefecto: false,
            Encabezado: "Observaciones", Ayuda: "Texto libre"),
        new EquipoCampoSistema("vidautil", "Vida util (anios)", TipoCampoTablero.Numero, VisiblePorDefecto: false,
            Encabezado: "Vida util (anios)", Ayuda: "Vida util estimada en anios (entero)"),
        new EquipoCampoSistema("valoradq", "Valor adquisicion", TipoCampoTablero.Moneda, VisiblePorDefecto: false,
            Encabezado: "Valor adquisicion", Ayuda: "Valor de compra (numero)"),
        new EquipoCampoSistema("proveedor", "Proveedor", TipoCampoTablero.Texto, VisiblePorDefecto: false,
            Encabezado: "Proveedor", Ayuda: "Proveedor al que se compro"),
        new EquipoCampoSistema("numfactura", "Numero factura", TipoCampoTablero.Texto, VisiblePorDefecto: false,
            Encabezado: "Numero factura", Ayuda: "Numero de factura de compra"),
        // Campos de la ficha tecnica que NO estan en la plantilla de carga (Encabezado null).
        new EquipoCampoSistema("marca", "Marca", TipoCampoTablero.Texto, VisiblePorDefecto: false),
        new EquipoCampoSistema("codigobarra", "Codigo de barra", TipoCampoTablero.Texto, VisiblePorDefecto: false),
        new EquipoCampoSistema("fechainstalacion", "Fecha instalacion", TipoCampoTablero.Fecha, VisiblePorDefecto: false),
        new EquipoCampoSistema("garantiahasta", "Garantia hasta", TipoCampoTablero.Fecha, VisiblePorDefecto: false),
        new EquipoCampoSistema("fechaadquisicion", "Fecha adquisicion", TipoCampoTablero.Fecha, VisiblePorDefecto: false),
    };

    public static IEnumerable<string> ClavesVisiblesPorDefecto
        => Todos.Where(c => c.VisiblePorDefecto).Select(c => c.Clave);

    public static EquipoCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public static EquipoCampoSistema? PorEncabezado(string encabezado)
        => Todos.FirstOrDefault(c => c.Encabezado is not null
            && string.Equals(c.Encabezado, (encabezado ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
