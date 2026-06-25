using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Servicio contratado por la copropiedad (modulo Finanzas - Servicios y contratos).
/// Un servicio agrupa UNO O MAS contratos (ContratoServicio.ServicioId).
/// "Quien ejecuta" es un tercero del Directorio (Persona o Empresa). Se guarda el
/// FK logico + un snapshot del nombre para listar sin join cross-contexto.
/// </summary>
public class Servicio : TenantEntity
{
    public TipoServicio Tipo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Quien ejecuta el servicio = tercero del Directorio (a lo sumo uno de los dos).
    public Guid? EjecutorPersonaId { get; set; }
    public Guid? EjecutorEmpresaId { get; set; }
    /// <summary>Snapshot del nombre del ejecutor para mostrar en listas.</summary>
    public string? EjecutorNombre { get; set; }

    public decimal? CostoMensual { get; set; }
    public decimal? CostoAnual { get; set; }

    public EstadoServicio Estado { get; set; } = EstadoServicio.Activo;

    public ICollection<ServicioContacto> Contactos { get; set; } = new List<ServicioContacto>();
    public ICollection<ServicioAdjunto> Adjuntos { get; set; } = new List<ServicioAdjunto>();
    public ICollection<ContratoServicio> Contratos { get; set; } = new List<ContratoServicio>();
}

/// <summary>
/// Contacto relacionado con un servicio. El contacto es a su vez un tercero del
/// Directorio (Persona o Empresa) - FK logico + snapshot de nombre.
/// </summary>
public class ServicioContacto : TenantEntity
{
    public Guid ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    public Guid? PersonaId { get; set; }
    public Guid? EmpresaId { get; set; }
    public string NombreSnapshot { get; set; } = string.Empty;

    /// <summary>Rol del contacto: "Supervisor", "Facturacion", "Soporte tecnico", etc.</summary>
    public string? Rol { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Adjunto de un servicio (cotizaciones, fichas tecnicas, etc.).
/// Mismo patron que MantenimientoAdjunto: blob en IBlobStorage + metadata.
/// </summary>
public class ServicioAdjunto : TenantEntity
{
    public Guid ServicioId { get; set; }
    public Servicio? Servicio { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    /// <summary>Key en el bucket. Nunca se expone directo.</summary>
    public string UrlStorage { get; set; } = string.Empty;
    public Guid? SubidoPorUsuarioId { get; set; }
}

/// <summary>
/// Adjunto de un contrato. La carga de contratos es el proceso mas importante del
/// modulo: aqui viven el PDF firmado, anexos, otrosi, etc.
/// </summary>
public class ContratoAdjunto : TenantEntity
{
    public Guid ContratoId { get; set; }
    public ContratoServicio? Contrato { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string UrlStorage { get; set; } = string.Empty;
    public Guid? SubidoPorUsuarioId { get; set; }
}
