using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Prestamo/reserva de un equipo o activo reservable (EquipoActivo.EsReservable == true), con
/// trazabilidad de entrega y devolucion. Las fotos van en <see cref="EntregaFoto"/> (OrigenTipo = "prestamo").
/// </summary>
public class PrestamoEquipo : TenantEntity
{
    /// <summary>PRE-{ANO}-{SECUENCIAL} unico por copropiedad.</summary>
    public string Codigo { get; set; } = string.Empty;

    public Guid EquipoActivoId { get; set; }
    public EquipoActivo? EquipoActivo { get; set; }

    /// <summary>Persona que toma prestado el equipo.</summary>
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid? UnidadPrivadaId { get; set; }
    public UnidadPrivada? UnidadPrivada { get; set; }

    public DateOnly Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    public EstadoPrestamoEquipo Estado { get; set; } = EstadoPrestamoEquipo.Reservado;
    public string? Observaciones { get; set; }

    // Trazabilidad entrega/devolucion.
    public DateTimeOffset? EntregadoAt { get; set; }
    public Guid? EntregadoPorPersonaId { get; set; }
    public string? EntregaObservacion { get; set; }
    public DateTimeOffset? DevueltoAt { get; set; }
    public Guid? DevueltoPorPersonaId { get; set; }
    public string? DevolucionObservacion { get; set; }

    public string? MotivoCancelacion { get; set; }
}

/// <summary>
/// Foto de trazabilidad (como se entrega / como se devuelve) de una reserva de zona o de un prestamo
/// de equipo. Polimorfica: OrigenTipo = "reserva" | "prestamo", OrigenId = Reserva.Id | PrestamoEquipo.Id.
/// </summary>
public class EntregaFoto : TenantEntity
{
    public string OrigenTipo { get; set; } = null!;   // "reserva" | "prestamo"
    public Guid OrigenId { get; set; }
    public string Url { get; set; } = null!;           // key/URL en IBlobStorage
    public MomentoEntrega Momento { get; set; }
    public Guid? RegistradoPorPersonaId { get; set; }
}
