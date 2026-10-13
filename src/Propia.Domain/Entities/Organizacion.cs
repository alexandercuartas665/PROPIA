using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Organizacion administradora de Copropiedades. Puede ser una empresa administradora
/// (persona juridica) o una persona natural que gestiona N copropiedades.
/// Entidad GLOBAL - sin tenant_id.
/// Spec: modulo 0.1 Super Admin Console y modulo 1.x (Capa 1).
/// </summary>
public class Organizacion : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public TipoOrganizacion Tipo { get; set; } = TipoOrganizacion.Administradora;
    public string? Nit { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public DateTimeOffset? FechaActivacion { get; set; }

    /// <summary>Estado operativo (Activa por defecto). Inactiva/Archivada la sacan de la vista principal.</summary>
    public EstadoOrganizacion Estado { get; set; } = EstadoOrganizacion.Activa;

    /// <summary>
    /// Override del maximo de COPROPIEDADES para ESTA organizacion, fijado por el operador (A&D) desde la
    /// consola. null = usar el limite del plan de su suscripcion (comportamiento por defecto). Un numero
    /// tiene prioridad sobre el limite del plan. 0 = no puede crear mas (bloqueada).
    /// </summary>
    public int? LimiteCopropiedades { get; set; }

    // Navegacion - todas las Copropiedades que administra esta Organizacion
    public ICollection<Tenant> Copropiedades { get; set; } = new List<Tenant>();
}
