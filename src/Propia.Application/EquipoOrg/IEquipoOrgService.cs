using Propia.Domain.Enums;

namespace Propia.Application.EquipoOrg;

/// <summary>
/// Servicio del modulo 1.3 Gestion de Equipo (Capa 1) - spec v1.0.
///
/// Filtra todo por OrganizacionId derivada del tenant activo del JWT
/// (Tenant.OrganizacionId). El Director ve el equipo de SU organizacion,
/// no de otras organizaciones.
///
/// Reglas clave:
///  - RN-01: identidad unica - si la persona ya existe en PropIA, se vincula (no se duplica).
///  - RN-02: cargo (Capa 1) != rol (Capa 2). Asignar PHs requiere rol_capa2 explicito.
///  - RN-06: cargo con colaboradores activos no puede eliminarse.
///  - RN-07: nombre del cargo unico por organizacion.
///  - Permiso efectivo: override individual gana sobre la plantilla del cargo.
/// </summary>
public interface IEquipoOrgService
{
    // ---------- Cargos (catalogo de la organizacion) ----------
    Task<IReadOnlyList<CargoDto>> ListarCargosAsync(CancellationToken ct);
    Task<CargoDetalleDto?> GetCargoDetalleAsync(Guid cargoId, CancellationToken ct);
    Task<CargoDto> CrearCargoAsync(CrearCargoRequest req, CancellationToken ct);
    Task<bool> ActualizarCargoAsync(Guid cargoId, ActualizarCargoRequest req, CancellationToken ct);
    Task<bool> EliminarCargoAsync(Guid cargoId, CancellationToken ct);
    Task<bool> AjustarPermisoCargoAsync(Guid cargoId, AjustarPermisoCargoRequest req, CancellationToken ct);

    // ---------- Colaboradores ----------
    Task<IReadOnlyList<ColaboradorListaDto>> ListarColaboradoresAsync(EstadoColaborador? estado, string? query, CancellationToken ct);
    Task<ColaboradorDetalleDto?> GetColaboradorAsync(Guid colaboradorId, CancellationToken ct);

    /// <summary>Busca por documento o email global - usado por el modal de agregar para identidad unica.</summary>
    Task<BusquedaIdentidadDto?> BuscarIdentidadAsync(string? documento, TipoDocumento? tipoDocumento, string? email, CancellationToken ct);

    Task<ColaboradorDetalleDto> AgregarColaboradorAsync(AgregarColaboradorRequest req, CancellationToken ct);

    Task<bool> CambiarCargoAsync(Guid colaboradorId, CambiarCargoRequest req, CancellationToken ct);
    Task<bool> AjustarPermisoColaboradorAsync(Guid colaboradorId, AjustarPermisoColaboradorRequest req, CancellationToken ct);
    Task<bool> ResetearPermisosColaboradorAsync(Guid colaboradorId, CancellationToken ct);

    Task<bool> DesactivarColaboradorAsync(Guid colaboradorId, DesactivarColaboradorRequest req, CancellationToken ct);
    Task<bool> ReactivarColaboradorAsync(Guid colaboradorId, CancellationToken ct);

    // ---------- Asignaciones a copropiedades ----------
    Task<AsignacionCopropiedadDto> AgregarAsignacionAsync(Guid colaboradorId, AgregarAsignacionRequest req, CancellationToken ct);
    Task<bool> CambiarRolPhAsync(Guid asignacionId, CambiarRolPhRequest req, CancellationToken ct);
    Task<bool> QuitarAsignacionAsync(Guid asignacionId, CancellationToken ct);

    // ---------- Vista del colaborador (perfil propio) ----------
    Task<ColaboradorDetalleDto?> GetMiPerfilAsync(CancellationToken ct);
    Task<IReadOnlyList<ColaboradorListaDto>> ListarCompanerosAsync(CancellationToken ct);
}
