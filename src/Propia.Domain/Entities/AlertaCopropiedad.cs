using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Alerta critica de la copropiedad. Spec 2.2 v1.0 banda de alertas criticas.
/// Generada automaticamente por modulos operativos (2.6, 2.7, 2.9, 2.10, 2.11, 2.3).
/// </summary>
public class AlertaCopropiedad : TenantEntity
{
    public TipoAlertaDashboard Tipo { get; set; }
    public SeveridadAlerta Severidad { get; set; } = SeveridadAlerta.Critica;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>URL accionable - lleva al modulo o item afectado.</summary>
    public string? UrlAccion { get; set; }

    /// <summary>Modulo que genero la alerta (codigo del catalogo).</summary>
    public string? ModuloOrigenCodigo { get; set; }

    /// <summary>Entidad puntual a la que apunta (tarea, contrato, pqrsd, ...).</summary>
    public Guid? EntidadId { get; set; }

    public bool Activa { get; set; } = true;
    public DateTimeOffset? ResueltaAt { get; set; }
}
