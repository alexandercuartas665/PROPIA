namespace Propia.Application.Navegacion;

/// <summary>
/// Menu de navegacion configurable GLOBAL (plataforma). Resuelve el menu base (MenuCatalog) con los
/// overrides guardados (nombre / orden / ubicacion) y persiste los cambios del editor del Super Admin.
/// Es global: el mismo menu resuelto lo ven todas las copropiedades (por eso se cachea una sola vez).
/// </summary>
public interface IMenuConfigService
{
    /// <summary>Menu resuelto (base + overrides) para render y editor. Cacheado (invalidado al guardar).</summary>
    Task<ResolvedMenu> GetResolvedMenuAsync(CancellationToken ct = default);

    /// <summary>Guarda el arreglo del editor: calcula deltas vs el base y los persiste. Invalida el cache.</summary>
    Task SaveArrangementAsync(SaveMenuArrangementRequest request, CancellationToken ct = default);

    /// <summary>Borra todos los overrides (vuelve el menu al base de codigo). Invalida el cache.</summary>
    Task ResetAsync(CancellationToken ct = default);
}
