using Propia.Domain.Enums;

namespace Propia.Application.EquipoOrg;

// ===================== Cargos =====================

/// <summary>Cargo dentro de la organizacion. Spec 1.3 v1.0 seccion 3.</summary>
public record CargoDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool EsDefault,
    bool Activo,
    int CantidadColaboradores);

/// <summary>Detalle del cargo con la plantilla de permisos Capa 1.</summary>
public record CargoDetalleDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool EsDefault,
    bool Activo,
    int CantidadColaboradores,
    IReadOnlyList<PermisoCapa1Dto> Permisos);

public record PermisoCapa1Dto(ModuloCapa1 Modulo, NivelPermisoCapa1 Nivel);

public record CrearCargoRequest(string Nombre, string? Descripcion);
public record ActualizarCargoRequest(string Nombre, string? Descripcion, bool Activo);
public record AjustarPermisoCargoRequest(ModuloCapa1 Modulo, NivelPermisoCapa1 Nivel);

// ===================== Colaboradores =====================

/// <summary>Fila de la bandeja de equipo. Spec 1.3 v1.0 seccion 6.1.</summary>
public record ColaboradorListaDto(
    Guid Id,
    Guid PersonaId,
    string Nombres,
    string Apellidos,
    TipoDocumento TipoDocumento,
    string Documento,
    string? Email,
    string? Telefono,
    Guid CargoId,
    string Cargo,
    EstadoColaborador Estado,
    DateOnly FechaVinculacion,
    int CantidadCopropiedadesAsignadas);

/// <summary>Detalle del colaborador con asignaciones, permisos y resumen del historial.</summary>
public record ColaboradorDetalleDto(
    Guid Id,
    Guid PersonaId,
    string Nombres,
    string Apellidos,
    TipoDocumento TipoDocumento,
    string Documento,
    string? Email,
    string? Telefono,
    Guid CargoId,
    string Cargo,
    EstadoColaborador Estado,
    DateOnly FechaVinculacion,
    DateOnly? FechaDesvinculacion,
    IReadOnlyList<AsignacionCopropiedadDto> Asignaciones,
    IReadOnlyList<PermisoCapa1EfectivoDto> PermisosEfectivos,
    IReadOnlyList<EventoHistorialDto> Historial);

public record AsignacionCopropiedadDto(
    Guid Id,
    Guid TenantId,
    string CopropiedadNombre,
    string? CodigoPropia,
    Guid RolCapa2Id,
    string RolCapa2Nombre,
    DateOnly FechaDesde);

public record PermisoCapa1EfectivoDto(
    ModuloCapa1 Modulo,
    NivelPermisoCapa1 NivelEfectivo,
    NivelPermisoCapa1 NivelCargo,
    NivelPermisoCapa1? NivelOverride,
    bool TieneOverride);

public record EventoHistorialDto(
    TipoEventoEquipo TipoEvento,
    string Descripcion,
    Guid RealizadoPor,
    DateTimeOffset OcurridoAt);

// ===================== Flujos =====================

/// <summary>
/// Crear colaborador (busqueda por cedula/email + creacion vinculada).
/// Si <paramref name="PersonaIdExistente"/> viene definido, reusa esa persona (identidad unica).
/// Si viene null, se crea Persona con los datos provistos.
/// </summary>
public record AgregarColaboradorRequest(
    Guid? PersonaIdExistente,
    TipoDocumento? TipoDocumento,
    string? Documento,
    string? Nombres,
    string? Apellidos,
    string? Email,
    string? Telefono,
    Guid CargoId,
    IReadOnlyList<AsignacionInicialDto>? Asignaciones,
    bool AsignarATodas,
    Guid? RolCapa2ParaTodas);

/// <summary>Asignacion (Tenant, Rol Capa 2) en el momento de crear colaborador o editarlo.</summary>
public record AsignacionInicialDto(Guid TenantId, Guid RolCapa2Id);

/// <summary>Resultado de busqueda por documento/email para reusar identidad existente.</summary>
public record BusquedaIdentidadDto(
    Guid PersonaId,
    TipoDocumento TipoDocumento,
    string Documento,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono,
    bool YaVinculadoAEstaOrganizacion);

public record CambiarCargoRequest(Guid CargoId, bool ResetearPermisos);

public record AjustarPermisoColaboradorRequest(ModuloCapa1 Modulo, NivelPermisoCapa1 Nivel);

public record DesactivarColaboradorRequest(string? Motivo, Guid? ReasignarA);

public record AgregarAsignacionRequest(Guid TenantId, Guid RolCapa2Id);
public record CambiarRolPhRequest(Guid RolCapa2Id);
