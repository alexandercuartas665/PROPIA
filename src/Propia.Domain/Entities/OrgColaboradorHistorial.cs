using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Evento append-only del historial de un colaborador. Spec 1.3 v1.0 tabla org_colaborador_historial.
/// Trigger de PostgreSQL bloquea UPDATE y DELETE - solo INSERT.
/// </summary>
public class OrgColaboradorHistorial : BaseEntity
{
    public Guid ColaboradorId { get; set; }
    public OrgColaborador? Colaborador { get; set; }

    public TipoEventoEquipo TipoEvento { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>JSON serializado con el estado previo (puede ser null para Vinculacion).</summary>
    public string? ValorAnterior { get; set; }

    /// <summary>JSON serializado con el estado nuevo.</summary>
    public string? ValorNuevo { get; set; }

    public Guid RealizadoPor { get; set; }
    public DateTimeOffset OcurridoAt { get; set; } = DateTimeOffset.UtcNow;
}
