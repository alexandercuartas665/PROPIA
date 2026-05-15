using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Relacion Persona-Empresa: una persona puede ser representante legal, contacto principal
/// o personal de una empresa. Spec 2.4 v1.0 tabla persona_empresa.
/// </summary>
public class PersonaEmpresa : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string? Cargo { get; set; }
    public bool EsRepresentanteLegal { get; set; }
    public bool EsContactoPrincipal { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public EstadoDirectorio Estado { get; set; } = EstadoDirectorio.Activo;
}
