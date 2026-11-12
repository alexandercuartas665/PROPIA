using Propia.Domain.Enums;

namespace Propia.Application.SuperAdmin;

// =====================================================================================================
// Semillas globales que siembra la Consola A&D (filas con tenant_id NULL). Indice + API de las que no
// tenian endpoint de administracion: roles base, plantillas de comunicados, etiquetas del Directorio,
// etiquetas de documentos y catalogo de reportes. Las demas (listas, agentes IA, plantillas PQRSD, menu,
// planes) ya tienen su API: el indice las enumera con sus rutas.
// Todas las escrituras son PUT MERGE (null = no se toca).
// =====================================================================================================

/// <summary>Un tipo de semilla del indice: clave, nombre, cuantas hay y las rutas de su API.</summary>
public sealed record SemillaTipoDto(string Clave, string Nombre, string Descripcion, int Total, IReadOnlyList<string> Rutas);

// ---- Roles base globales (roles_copropiedad con tenant_id NULL) + permisos por defecto (rol_permisos) ----
public sealed record RolPermisoSemillaDto(string ModuloCodigo, AccionPermiso Accion, bool Habilitado, NivelDato NivelDato);
public sealed record RolSemillaDto(Guid Id, string Nombre, string? Descripcion, TipoRol Tipo, CategoriaRol Categoria,
    bool EsEliminable, bool Activo, IReadOnlyList<RolPermisoSemillaDto> Permisos);
public sealed record GuardarRolSemillaRequest(string? Nombre = null, string? Descripcion = null, CategoriaRol? Categoria = null, bool? Activo = null);
public sealed record GuardarPermisosRolSemillaRequest(IReadOnlyList<RolPermisoSemillaDto> Permisos);

// ---- Plantillas globales de comunicados (comunicado_plantillas EsGlobal) ----
public sealed record ComunicadoPlantillaSemillaDto(Guid Id, string Nombre, TipoComunicadoBase TipoComunicado, string AsuntoModelo,
    string CuerpoModelo, bool AcusePorDefecto, bool Activa);
public sealed record GuardarComunicadoPlantillaSemillaRequest(string? Nombre = null, TipoComunicadoBase? TipoComunicado = null,
    string? AsuntoModelo = null, string? CuerpoModelo = null, bool? AcusePorDefecto = null, bool? Activa = null);

// ---- Etiquetas base del Directorio (etiquetas_catalogo con tenant_id NULL) ----
public sealed record EtiquetaDirectorioSemillaDto(Guid Id, string Codigo, string Nombre, GrupoEtiqueta Grupo, AplicaEtiqueta AplicaA,
    bool TieneLogicaEspecial, string? Icono, string? Color, int Orden, bool Activo);
public sealed record GuardarEtiquetaDirectorioSemillaRequest(string? Codigo = null, string? Nombre = null, GrupoEtiqueta? Grupo = null,
    AplicaEtiqueta? AplicaA = null, string? Icono = null, string? Color = null, int? Orden = null, bool? Activo = null);

// ---- Etiquetas base de documentos (documento_etiquetas_catalogo con tenant_id NULL) ----
public sealed record EtiquetaDocumentoSemillaDto(Guid Id, string Nombre, string? Color, bool Activa);
public sealed record GuardarEtiquetaDocumentoSemillaRequest(string? Nombre = null, string? Color = null, bool? Activa = null);

// ---- Catalogo de reportes (reporte_categorias / reporte_catalogo con tenant_id NULL). Solo se editan y activan:
//      cada reporte lo resuelve el motor por su Clave, asi que no se crean desde aqui. ----
public sealed record ReporteSemillaDto(Guid Id, Guid CategoriaId, string Clave, string Nombre, string? Descripcion,
    string ModuloOrigen, string AudienciasJson, int Orden, bool Activo);
public sealed record ReporteCategoriaSemillaDto(Guid Id, string Nombre, string? Icono, string? Color,
    string ModuloOrigen, int Orden, bool Activa, IReadOnlyList<ReporteSemillaDto> Reportes);
public sealed record GuardarReporteCategoriaSemillaRequest(string? Nombre = null, string? Icono = null,
    string? Color = null, int? Orden = null, bool? Activa = null);
public sealed record GuardarReporteSemillaRequest(string? Nombre = null, string? Descripcion = null, string? AudienciasJson = null,
    int? Orden = null, bool? Activo = null);

public interface ISemillasAdminService
{
    Task<IReadOnlyList<SemillaTipoDto>> IndiceAsync(CancellationToken ct);

    Task<IReadOnlyList<RolSemillaDto>> ListarRolesAsync(CancellationToken ct);
    /// <summary>Crea un rol EXTENDIDO global (desactivable y eliminable). InvalidOperationException si el nombre falta o ya existe.</summary>
    Task<RolSemillaDto> CrearRolAsync(GuardarRolSemillaRequest req, CancellationToken ct);
    /// <summary>null si no existe. InvalidOperationException si intenta renombrar o desactivar "Administrador".</summary>
    Task<RolSemillaDto?> ActualizarRolAsync(Guid id, GuardarRolSemillaRequest req, CancellationToken ct);
    /// <summary>Upsert de los permisos POR DEFECTO del rol (las copropiedades pueden tener su override).</summary>
    Task<RolSemillaDto?> GuardarPermisosRolAsync(Guid id, GuardarPermisosRolSemillaRequest req, CancellationToken ct);
    /// <summary>Solo roles eliminables y sin usuarios asignados en ninguna copropiedad. InvalidOperationException si no se puede.</summary>
    Task<bool> EliminarRolAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ComunicadoPlantillaSemillaDto>> ListarPlantillasComunicadoAsync(CancellationToken ct);
    Task<ComunicadoPlantillaSemillaDto> CrearPlantillaComunicadoAsync(GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct);
    Task<ComunicadoPlantillaSemillaDto?> ActualizarPlantillaComunicadoAsync(Guid id, GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct);

    Task<IReadOnlyList<EtiquetaDirectorioSemillaDto>> ListarEtiquetasDirectorioAsync(CancellationToken ct);
    Task<EtiquetaDirectorioSemillaDto> CrearEtiquetaDirectorioAsync(GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct);
    Task<EtiquetaDirectorioSemillaDto?> ActualizarEtiquetaDirectorioAsync(Guid id, GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct);

    Task<IReadOnlyList<EtiquetaDocumentoSemillaDto>> ListarEtiquetasDocumentoAsync(CancellationToken ct);
    Task<EtiquetaDocumentoSemillaDto> CrearEtiquetaDocumentoAsync(GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct);
    Task<EtiquetaDocumentoSemillaDto?> ActualizarEtiquetaDocumentoAsync(Guid id, GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct);

    Task<IReadOnlyList<ReporteCategoriaSemillaDto>> ListarReportesAsync(CancellationToken ct);
    Task<ReporteCategoriaSemillaDto?> ActualizarReporteCategoriaAsync(Guid id, GuardarReporteCategoriaSemillaRequest req, CancellationToken ct);
    Task<ReporteSemillaDto?> ActualizarReporteAsync(Guid id, GuardarReporteSemillaRequest req, CancellationToken ct);
}
