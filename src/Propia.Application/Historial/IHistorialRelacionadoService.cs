using Propia.Domain.Enums;

namespace Propia.Application.Historial;

/// <summary>Historial relacionado cross-modulo: dado una entidad de la copropiedad (unidad, zona o
/// equipo) reune las tareas, PQRSD y mantenimientos vinculados. Base de las pestanas "Historial"
/// de las fichas de Zonas, Equipos y Unidades.
///
/// Cobertura del vinculo por tipo de entidad:
/// - Mantenimiento: por ActivoTipo+ActivoId (fiable) para Zona y Equipo.
/// - Tareas: por Tarea.OrigenEntidadId (fiable); las tareas legado sin Id se resuelven por nombre
///   (OrigenTipo + OrigenReferencia). Aplica a los tres tipos.
/// - PQRSD: por PqrsdExpediente.UnidadPrivadaId. Solo aplica a Unidad (no hay vinculo a zona/equipo).</summary>
public interface IHistorialRelacionadoService
{
    Task<HistorialRelacionadoDto> GetAsync(TipoEntidadHistorial tipo, Guid entidadId, CancellationToken ct);
}
