using Propia.Domain.Enums;

namespace Propia.Application.Directorio;

/// <summary>
/// Servicio del modulo 2.4 Directorio - registro maestro de personas y empresas
/// vinculadas a las copropiedades. Spec v1.0.
///
/// Las operaciones de busqueda y CRUD de Persona/Empresa son GLOBALES (sin tenant).
/// Las operaciones de Vinculo, Contacto, PersonaEmpresa son TENANT (RLS).
/// </summary>
public interface IDirectorioService
{
    // --- Busqueda y CRUD de Personas (global) ---
    Task<PersonaDetalleDto?> BuscarPersonaPorDocumentoAsync(BuscarPorDocumentoRequest req, CancellationToken ct);
    Task<PersonaDetalleDto?> ObtenerPersonaAsync(Guid id, CancellationToken ct);
    Task<PersonaDetalleDto> CrearPersonaAsync(CrearPersonaRequest req, CancellationToken ct);
    Task<PersonaDetalleDto?> ActualizarPersonaAsync(Guid id, ActualizarPersonaRequest req, CancellationToken ct);

    // --- Busqueda y CRUD de Empresas (global) ---
    Task<EmpresaDetalleDto?> BuscarEmpresaPorNitAsync(string nit, CancellationToken ct);
    Task<EmpresaDetalleDto?> ObtenerEmpresaAsync(Guid id, CancellationToken ct);
    Task<EmpresaDetalleDto> CrearEmpresaAsync(CrearEmpresaRequest req, CancellationToken ct);
    Task<EmpresaDetalleDto?> ActualizarEmpresaAsync(Guid id, ActualizarEmpresaRequest req, CancellationToken ct);

    // --- Bandeja (filtrada al tenant via vinculos) ---
    Task<IReadOnlyList<PersonaResumenDto>> ListarPersonasDelTenantAsync(string? query, CancellationToken ct);
    Task<IReadOnlyList<EmpresaResumenDto>> ListarEmpresasDelTenantAsync(string? query, CancellationToken ct);

    // --- Selector de personas (autocompletado a nivel organizacion) ---

    /// <summary>
    /// Busca en TODAS las copropiedades de la organizacion del usuario, marcando cuales
    /// candidatos ya estan en la copropiedad activa. Nunca sale de la organizacion.
    /// </summary>
    Task<IReadOnlyList<PersonaCandidatoDto>> BuscarCandidatosAsync(string query, CancellationToken ct);

    /// <summary>
    /// Trae a la copropiedad activa una persona que ya existe en otra de la organizacion.
    /// No duplica la persona (es global): crea el vinculo que faltaba.
    /// </summary>
    Task<bool> VincularCandidatoAsync(Guid personaId, CancellationToken ct);

    // --- Vista 360 (incluye vinculos del tenant activo) ---
    Task<Persona360Dto?> GetPersona360Async(Guid personaId, CancellationToken ct);
    Task<Empresa360Dto?> GetEmpresa360Async(Guid empresaId, CancellationToken ct);

    // --- Vinculos con copropiedad ---
    Task<VinculoDto> CrearVinculoAsync(CrearVinculoRequest req, CancellationToken ct);
    Task<bool> InactivarVinculoAsync(Guid vinculoId, string? motivo, CancellationToken ct);
    Task<VinculoDto> AsignarEtiquetaAsync(AsignarEtiquetaRequest req, CancellationToken ct);
    Task<bool> QuitarEtiquetaAsync(Guid asignacionId, CancellationToken ct);

    // --- Contactos ---
    Task<ContactoDto> AgregarContactoAsync(AgregarContactoRequest req, CancellationToken ct);
    Task<bool> EliminarContactoAsync(Guid contactoId, CancellationToken ct);

    // --- Catalogo de etiquetas (base + custom) ---
    Task<IReadOnlyList<EtiquetaCatalogoDto>> ListarEtiquetasAsync(AplicaEtiqueta? aplicaA, GrupoEtiqueta? grupo, CancellationToken ct);
    Task<EtiquetaCatalogoDto> CrearEtiquetaCustomAsync(CrearEtiquetaCustomRequest req, CancellationToken ct);
    Task<bool> EliminarEtiquetaCustomAsync(Guid etiquetaId, CancellationToken ct);

    // --- Helper de validacion NIT (digito DIAN) ---
    string CalcularDigitoVerificacionNit(string nit);
}
