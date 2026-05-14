namespace Propia.Application.MiCopropiedad;

/// <summary>
/// Servicio del modulo 2.3 Mi Copropiedad - ficha viva de la PH con 8 secciones.
/// Las acciones del usuario son de la copropiedad ACTIVA (tenant del JWT).
/// TODA tabla operativa es TenantEntity + RLS garantiza aislamiento.
/// </summary>
public interface IMiCopropiedadService
{
    // Resumen para la pagina principal (calcula % de completitud por seccion)
    Task<ResumenMiCopropiedadDto?> GetResumenAsync(Guid tenantId, CancellationToken ct);

    // Seccion 1 - Identidad
    Task<IdentidadDto?> ActualizarIdentidadAsync(Guid tenantId, ActualizarIdentidadRequest req, CancellationToken ct);

    // Seccion 2 - Distribucion (torres + unidades)
    Task<IReadOnlyList<TorreDto>> ListTorresAsync(CancellationToken ct);
    Task<TorreDto> CrearTorreAsync(CrearTorreRequest req, CancellationToken ct);
    Task<bool> EliminarTorreAsync(Guid torreId, CancellationToken ct);

    Task<IReadOnlyList<UnidadDto>> ListUnidadesAsync(CancellationToken ct);
    Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct);
    Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct);

    // Seccion 4 - Gobierno (Consejo)
    Task<IReadOnlyList<MiembroConsejoDto>> ListMiembrosConsejoAsync(CancellationToken ct);
    Task<MiembroConsejoDto> AgregarMiembroConsejoAsync(AgregarMiembroConsejoRequest req, CancellationToken ct);
    Task<bool> DesactivarMiembroConsejoAsync(Guid miembroId, CancellationToken ct);

    // Seccion 5 - Servicios (contratos)
    Task<IReadOnlyList<ContratoServicioDto>> ListContratosAsync(CancellationToken ct);
    Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct);
    Task<bool> EliminarContratoAsync(Guid contratoId, CancellationToken ct);

    // Seccion 6 - Zonas Comunes
    Task<IReadOnlyList<ZonaComunDto>> ListZonasComunesAsync(CancellationToken ct);
    Task<ZonaComunDto> CrearZonaComunAsync(CrearZonaComunRequest req, CancellationToken ct);
    Task<bool> EliminarZonaComunAsync(Guid zonaId, CancellationToken ct);

    // Seccion 7 - Equipos Activos
    Task<IReadOnlyList<EquipoActivoDto>> ListEquiposAsync(CancellationToken ct);
    Task<EquipoActivoDto> CrearEquipoAsync(CrearEquipoActivoRequest req, CancellationToken ct);
    Task<bool> EliminarEquipoAsync(Guid equipoId, CancellationToken ct);
}
