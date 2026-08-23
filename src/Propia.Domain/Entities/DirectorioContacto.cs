using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Contacto adicional (email, telefono, whatsapp, direccion) de una persona o empresa.
/// Spec 2.4 v1.0 tabla directorio_contacto. Multiples contactos por entidad con visibilidad.
/// </summary>
public class DirectorioContacto : TenantEntity
{
    public EntidadDirectorio EntidadTipo { get; set; }
    public Guid EntidadId { get; set; }

    public TipoContacto Tipo { get; set; }
    public string? SubtipoLabel { get; set; }  // "Personal", "Corporativo", "Facturacion"
    public string Valor { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public bool EsPrincipal { get; set; }
    public VisibilidadContacto Visibilidad { get; set; } = VisibilidadContacto.Copropiedad;
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Documento adjunto de una persona o empresa del directorio (RUT, camara de comercio,
/// certificados, cedula). GLOBAL como los contactos: el documento viaja con la identidad
/// y se reutiliza en cualquier copropiedad; tenant_id solo registra el origen del cargue.
/// </summary>
public class DirectorioAdjunto : TenantEntity
{
    public EntidadDirectorio EntidadTipo { get; set; }
    public Guid EntidadId { get; set; }

    /// <summary>Nombre original del archivo (como lo subio el usuario).</summary>
    public string Nombre { get; set; } = string.Empty;
    /// <summary>URL relativa servida por el host unificado (convencion ResolveUrl).</summary>
    public string Url { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long TamanoBytes { get; set; }
}
