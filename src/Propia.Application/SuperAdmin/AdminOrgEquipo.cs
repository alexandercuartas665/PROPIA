using Propia.Application.EquipoOrg;
using Propia.Domain.Enums;

namespace Propia.Application.SuperAdmin;

/// <summary>
/// Equipo (Capa 1) de CUALQUIER organizacion desde la API del Super Admin (JWT de Super Admin o token pak_).
/// Fija la organizacion de la ruta y delega en IEquipoOrgService (misma logica que la consola de la org).
/// No crea cuentas ni recibe claves: el acceso a cada copropiedad queda como UsuarioTenant (Activo si la
/// persona ya tiene cuenta; si no, Pendiente para activarla con clave temporal o invitacion).
/// Organizacion inexistente / colaborador de otra org 404; negocio 400 { error }.
/// </summary>
public interface IAdminOrgEquipoService
{
    Task<IReadOnlyList<ColaboradorListaDto>> ListarEquipoAsync(Guid orgId, EstadoColaborador? estado, string? q, CancellationToken ct);
    Task<IReadOnlyList<CargoDto>> ListarCargosAsync(Guid orgId, CancellationToken ct);
    Task<AdminColaboradorResultadoDto> AgregarAsync(Guid orgId, AdminAgregarColaboradorRequest req, AdminActor actor, CancellationToken ct);
    Task CambiarCargoAsync(Guid orgId, Guid colaboradorId, AdminCambiarCargoRequest req, AdminActor actor, CancellationToken ct);
    Task DesactivarAsync(Guid orgId, Guid colaboradorId, AdminDesactivarColaboradorRequest req, AdminActor actor, CancellationToken ct);
    Task ReactivarAsync(Guid orgId, Guid colaboradorId, AdminActor actor, CancellationToken ct);
}

/// <summary>
/// Persona por personaId, o por tipoDocumento+documento, o con datos (persona) para buscarla (documento y
/// luego correo) o crearla. Asignaciones: copropiedades de la org y rol de cada una.
/// </summary>
public sealed record AdminAgregarColaboradorRequest(
    Guid? PersonaId,
    TipoDocumento? TipoDocumento,
    string? Documento,
    AdminPersonaNuevaRequest? Persona,
    Guid CargoId,
    IReadOnlyList<AdminAsignacionColaboradorRequest>? Asignaciones);

public sealed record AdminAsignacionColaboradorRequest(Guid TenantId, Guid RolId);

public sealed record AdminAsignacionResultadoDto(Guid TenantId, Guid UsuarioTenantId, EstadoUsuarioTenant Estado, bool UsuarioCreado);

public sealed record AdminColaboradorResultadoDto(
    ColaboradorDetalleDto Colaborador,
    bool YaExistia,
    IReadOnlyList<AdminAsignacionResultadoDto> Asignaciones,
    IReadOnlyList<string>? Advertencias);

public sealed record AdminCambiarCargoRequest(Guid CargoId, bool ResetearPermisos = false);

public sealed record AdminDesactivarColaboradorRequest(string? Motivo);
