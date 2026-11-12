namespace Propia.Application.InfraestructuraIa;

/// <summary>Agente del sistema resuelto en la copropiedad activa: su id y si esta activo.</summary>
public sealed record AgenteSistema(Guid Id, bool Activo);

/// <summary>
/// Resuelve los agentes que usa el propio sistema (Agente Documental, Auxiliar Administrativo) en la
/// copropiedad activa por la CLAVE de su plantilla (PlantillaAgente.Clave*), no por el nombre. Si la
/// copropiedad no lo tiene, lo crea en ese momento desde la plantilla del Super Admin ("al usarlo").
/// </summary>
public interface IAgentesSistema
{
    /// <summary>Null si no hay copropiedad activa o no existe la plantilla (ni el agente).</summary>
    Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default);
}
