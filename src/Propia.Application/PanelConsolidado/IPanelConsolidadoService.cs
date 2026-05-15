namespace Propia.Application.PanelConsolidado;

/// <summary>Servicio del modulo 1.1 Panel y Dashboard Consolidado (Capa 1, spec v1.0).</summary>
public interface IPanelConsolidadoService
{
    /// <summary>Devuelve el panel completo (KPIs + tarjetas + alertas) para la org del JWT.</summary>
    Task<PanelResumenDto> GetPanelAsync(CancellationToken ct);

    /// <summary>Recalcula los snapshots de TODAS las copropiedades de la organizacion.
    /// Disenado para MVP - en produccion ira en un job programado.</summary>
    Task<int> RecalcularSnapshotsAsync(CancellationToken ct);

    Task<IReadOnlyList<PanelFeedEventoDto>> ListarFeedAsync(int limit, CancellationToken ct);
}
