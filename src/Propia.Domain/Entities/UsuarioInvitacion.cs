using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Token de invitacion al sistema. Spec 2.5 v1.0 tabla <c>usuario_invitacion</c>.
/// 72 horas de vigencia, un solo uso (RN-11). Token generado con crypto.RandomBytes(64).
/// </summary>
public class UsuarioInvitacion : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid RolId { get; set; }
    public Rol? Rol { get; set; }

    /// <summary>Token unico, generado con crypto.RandomBytes. Hash NO necesario al ser un secreto efimero.</summary>
    public string Token { get; set; } = string.Empty;

    public EstadoInvitacion Estado { get; set; } = EstadoInvitacion.Pendiente;
    public DateTimeOffset ExpiraAt { get; set; }
    public DateTimeOffset? AceptadaAt { get; set; }
    public DateTimeOffset? CanceladaAt { get; set; }
    public CanalEnvioInvitacion CanalEnvio { get; set; } = CanalEnvioInvitacion.Email;
    public Guid? CreadaPorUsuarioId { get; set; }

    /// <summary>
    /// Tablero de trabajo (2.10) destino: si esta seteado, al aceptar la invitacion la persona
    /// queda agregada a ese tablero. Usado para invitar externos a colaborar en un tablero.
    /// </summary>
    public Guid? TableroId { get; set; }
}
