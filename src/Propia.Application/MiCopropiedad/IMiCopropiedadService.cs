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
    Task<UnidadDto?> ObtenerUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadDto> CrearUnidadAsync(CrearUnidadRequest req, CancellationToken ct);
    Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct);

    // Tipos de unidad personalizados
    Task<IReadOnlyList<TipoUnidadCustomDto>> ListTiposUnidadCustomAsync(CancellationToken ct);
    Task<TipoUnidadCustomDto> CrearTipoUnidadCustomAsync(CrearTipoUnidadCustomRequest req, CancellationToken ct);
    Task<bool> EliminarTipoUnidadCustomAsync(Guid tipoId, CancellationToken ct);

    // Tipos de coeficiente PH (spec 2.3 - RN-02)
    Task<IReadOnlyList<TipoCoeficienteDto>> ListTiposCoeficienteAsync(CancellationToken ct);
    Task<TipoCoeficienteDto> CrearTipoCoeficienteAsync(CrearTipoCoeficienteRequest req, CancellationToken ct);
    Task<bool> EliminarTipoCoeficienteAsync(Guid tipoId, CancellationToken ct);
    Task<IReadOnlyList<UnidadCoeficienteDto>> ListCoeficientesUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadCoeficienteDto> SetCoeficienteUnidadAsync(Guid unidadId, SetCoeficienteUnidadRequest req, CancellationToken ct);

    // Generador inteligente - crea torres + unidades en transaccion
    Task<GenerarUnidadesResponse> GenerarUnidadesAsync(GenerarUnidadesRequest req, CancellationToken ct);

    // Importacion CSV transaccional (todo-o-nada)
    Task<ImportarUnidadesResponse> ImportarUnidadesCsvAsync(ImportarUnidadesRequest req, CancellationToken ct);

    // Seccion 4 - Gobierno (Consejo)
    Task<IReadOnlyList<MiembroConsejoDto>> ListMiembrosConsejoAsync(CancellationToken ct);
    Task<MiembroConsejoDto> AgregarMiembroConsejoAsync(AgregarMiembroConsejoRequest req, CancellationToken ct);
    Task<bool> DesactivarMiembroConsejoAsync(Guid miembroId, CancellationToken ct);

    // Seccion 4 - Gobierno (Comites)
    Task<IReadOnlyList<ComiteDto>> ListComitesAsync(CancellationToken ct);
    Task<ComiteDto> CrearComiteAsync(CrearComiteRequest req, CancellationToken ct);
    Task<bool> DesactivarComiteAsync(Guid comiteId, CancellationToken ct);
    Task<IReadOnlyList<ComiteMiembroDto>> ListMiembrosComiteAsync(Guid comiteId, CancellationToken ct);
    Task<ComiteMiembroDto> AgregarMiembroComiteAsync(AgregarComiteMiembroRequest req, CancellationToken ct);
    Task<bool> RetirarMiembroComiteAsync(Guid miembroId, CancellationToken ct);

    // Seccion 4 - Gobierno (Revisor Fiscal)
    Task<RevisorFiscalDto?> GetRevisorFiscalActivoAsync(CancellationToken ct);
    Task<RevisorFiscalDto> DesignarRevisorFiscalAsync(DesignarRevisorFiscalRequest req, CancellationToken ct);
    Task<bool> RetirarRevisorFiscalAsync(Guid revisorId, CancellationToken ct);

    // Seccion 3 - Equipo de trabajo
    Task<IReadOnlyList<MiembroEquipoDto>> ListEquipoAsync(CancellationToken ct);
    Task<MiembroEquipoDto> AgregarMiembroEquipoAsync(AgregarMiembroEquipoRequest req, CancellationToken ct);
    Task<bool> DesactivarMiembroEquipoAsync(Guid miembroId, CancellationToken ct);
    // Helper compartido: busca o crea una Persona global por documento. Necesario hasta que exista 2.4 Directorio.
    Task<Guid> VincularPersonaPorDocumentoAsync(VincularPersonaPorDocumentoRequest req, CancellationToken ct);

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
