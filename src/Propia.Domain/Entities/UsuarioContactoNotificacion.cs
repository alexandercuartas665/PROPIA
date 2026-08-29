using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Contacto (correo o telefono) al que el usuario quiere recibir sus notificaciones.
/// Entidad GLOBAL por persona (no por copropiedad): un mismo set de contactos aplica en
/// todas las copropiedades donde el usuario tiene acceso. Sin tenant_id ni RLS.
/// MVP: se guardan sin verificacion por codigo (Fase 2).
/// </summary>
public class UsuarioContactoNotificacion : BaseEntity
{
    /// <summary>Persona (usuario) duenia del contacto. Tabla global personas.</summary>
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    /// <summary>Canal implicito: Email (correo) o WhatsApp (telefono). Solo estos dos en el MVP self-service.</summary>
    public CanalNotificacion Canal { get; set; }

    /// <summary>Correo o numero de telefono destino.</summary>
    public string Valor { get; set; } = string.Empty;

    /// <summary>Si esta activo, el sistema envia notificaciones a este contacto.</summary>
    public bool Activo { get; set; } = true;
}
