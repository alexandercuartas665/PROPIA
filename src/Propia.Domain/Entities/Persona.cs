using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Persona natural. Entidad GLOBAL de la plataforma - identidad unica.
/// Una persona existe UNA sola vez en la plataforma, identificada por documento + tipo.
/// Spec: modulo 2.4 Directorio + principio de arquitectura "identidad unica de plataforma".
/// Email es opcional pero unico cuando se especifica (citext - case insensitive).
/// </summary>
public class Persona : BaseEntity
{
    public TipoDocumento TipoDocumento { get; set; } = TipoDocumento.CC;
    public string Documento { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? FotoUrl { get; set; }
    public string? FirmaUrl { get; set; }

    // Modulo 2.4 Directorio (spec v1.0)
    public DateOnly? FechaNacimiento { get; set; }
    public GeneroPersona? Genero { get; set; }
    public bool AceptoTratamientoDatos { get; set; }
    public DateTimeOffset? FechaAceptacionDatos { get; set; }
    public string? VersionPoliticaDatos { get; set; }
    public CanalAceptacionDatos? CanalAceptacion { get; set; }
    public string? IpAceptacion { get; set; }
    public bool PerfilIncompleto { get; set; } = true;
    public EstadoDirectorio EstadoDirectorio { get; set; } = EstadoDirectorio.Activo;

    public ICollection<UsuarioTenant> VinculosATenants { get; set; } = new List<UsuarioTenant>();
}
