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
    Task<UnidadDto?> ActualizarUnidadAsync(Guid unidadId, ActualizarUnidadRequest req, CancellationToken ct);
    Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct);

    // Vinculos entre unidades (principal <-> asociadas, RN-09)
    Task<IReadOnlyList<UnidadVinculoDto>> ListVinculosAsync(Guid unidadPrincipalId, CancellationToken ct);
    Task<UnidadVinculoDto> CrearVinculoAsync(Guid unidadPrincipalId, CrearVinculoUnidadRequest req, CancellationToken ct);
    Task<bool> EliminarVinculoAsync(Guid vinculoId, CancellationToken ct);

    // Personas vinculadas a una unidad (Propietario/Residente/Familiar)
    Task<IReadOnlyList<UnidadPersonaDto>> ListPersonasUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadPersonaDto> AgregarPersonaUnidadAsync(Guid unidadId, AgregarPersonaUnidadRequest req, CancellationToken ct);
    Task<UnidadPersonaDto?> EditarPersonaUnidadAsync(Guid unidadPersonaId, AgregarPersonaUnidadRequest req, CancellationToken ct);
    Task<bool> EliminarPersonaUnidadAsync(Guid unidadPersonaId, CancellationToken ct);

    // Campos personalizados de unidad (definicion compartida por copropiedad + valor por unidad)
    Task<IReadOnlyList<UnidadCampoDefinicionDto>> ListCamposDefinicionAsync(CancellationToken ct);
    Task<UnidadCampoDefinicionDto> CrearCampoDefinicionAsync(CrearCampoDefinicionRequest req, CancellationToken ct);
    Task<bool> EliminarCampoDefinicionAsync(Guid definicionId, CancellationToken ct);
    Task<IReadOnlyList<UnidadCampoDto>> ListCamposUnidadAsync(Guid unidadId, CancellationToken ct);
    Task SetCampoValorUnidadAsync(Guid unidadId, Guid definicionId, SetCampoValorRequest req, CancellationToken ct);

    // Documentos / anexos de una unidad
    Task<IReadOnlyList<UnidadDocumentoDto>> ListDocumentosUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadDocumentoDto?> AgregarDocumentoUnidadAsync(Guid unidadId, string nombre, string url, long tamano, CancellationToken ct);
    Task<bool> EliminarDocumentoUnidadAsync(Guid documentoId, CancellationToken ct);

    // Prototipo v3 - bloques nuevos de la ficha de inmueble
    // Placas habilitadas para ingreso
    Task<IReadOnlyList<UnidadPlacaDto>> ListPlacasUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadPlacaDto?> AgregarPlacaUnidadAsync(Guid unidadId, CrearUnidadPlacaRequest req, CancellationToken ct);
    Task<bool> EliminarPlacaUnidadAsync(Guid placaId, CancellationToken ct);

    // Arriendos y cobros mensuales
    Task<IReadOnlyList<UnidadArriendoDto>> ListArriendosUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadArriendoDto?> AgregarArriendoUnidadAsync(Guid unidadId, CrearUnidadArriendoRequest req, CancellationToken ct);
    Task<bool> EliminarArriendoUnidadAsync(Guid arriendoId, CancellationToken ct);

    // Mascotas
    Task<IReadOnlyList<UnidadMascotaDto>> ListMascotasUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadMascotaDto?> AgregarMascotaUnidadAsync(Guid unidadId, CrearUnidadMascotaRequest req, CancellationToken ct);
    Task<bool> EliminarMascotaUnidadAsync(Guid mascotaId, CancellationToken ct);

    // Empleada(s) de servicio
    Task<IReadOnlyList<UnidadEmpleadaDto>> ListEmpleadasUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadEmpleadaDto?> AgregarEmpleadaUnidadAsync(Guid unidadId, CrearUnidadEmpleadaRequest req, CancellationToken ct);
    Task<bool> EliminarEmpleadaUnidadAsync(Guid empleadaId, CancellationToken ct);

    // Oleada 2 - Historico de titularidad (propietarios)
    Task<IReadOnlyList<UnidadTitularidadDto>> ListTitularidadUnidadAsync(Guid unidadId, CancellationToken ct);
    Task<UnidadTitularidadDto?> AgregarTitularidadUnidadAsync(Guid unidadId, CrearUnidadTitularidadRequest req, CancellationToken ct);
    Task<bool> EliminarTitularidadUnidadAsync(Guid titularidadId, CancellationToken ct);

    // Oleada 2 - Campos dinamicos por persona vinculada
    Task<IReadOnlyList<UnidadPersonaCampoDto>> ListCamposPersonaAsync(Guid unidadPersonaId, CancellationToken ct);
    Task<UnidadPersonaCampoDto?> AgregarCampoPersonaAsync(Guid unidadPersonaId, CrearUnidadPersonaCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoPersonaAsync(Guid campoId, CancellationToken ct);

    // Cuota consolidada de una unidad (principal + asociadas con incluyeEnFacturacion=true)
    Task<CuotaConsolidadaDto?> GetCuotaConsolidadaAsync(Guid unidadId, CancellationToken ct);

    // Rebalancear coeficientes: distribuye 100% equitativamente entre unidades que pagan
    // administracion (parqueaderos/depositos opcionalmente con un coef bajo fijo).
    Task<RebalanceoCoeficientesDto> RebalancearCoeficientesAsync(CancellationToken ct);

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

    // Gobierno + equipo consolidado por persona (modulo Usuarios 2.5 - tags de gobierno)
    Task<GobiernoPersonaDto> GetGobiernoPersonaAsync(Guid personaId, CancellationToken ct);
    // Helper compartido: busca o crea una Persona global por documento. Necesario hasta que exista 2.4 Directorio.
    Task<Guid> VincularPersonaPorDocumentoAsync(VincularPersonaPorDocumentoRequest req, CancellationToken ct);

    // Seccion 5 - Servicios (contratos)
    Task<IReadOnlyList<ContratoServicioDto>> ListContratosAsync(CancellationToken ct);
    Task<ContratoServicioDto> CrearContratoAsync(CrearContratoServicioRequest req, CancellationToken ct);
    Task<bool> ActualizarContratoAsync(Guid contratoId, ActualizarContratoRequest req, CancellationToken ct);
    Task<bool> EliminarContratoAsync(Guid contratoId, CancellationToken ct);
    // Campos personalizados (EAV) de contratos
    Task<IReadOnlyList<ContratoCampoDto>> ListContratoCamposAsync(CancellationToken ct);
    Task<ContratoCampoDto> CrearContratoCampoAsync(CrearContratoCampoRequest req, CancellationToken ct);
    Task<bool> ActualizarContratoCampoAsync(Guid campoId, ActualizarContratoCampoRequest req, CancellationToken ct);
    Task<bool> EliminarContratoCampoAsync(Guid campoId, CancellationToken ct);
    Task<bool> GuardarContratoCampoValorAsync(Guid contratoId, Guid campoId, GuardarContratoCampoValorRequest req, CancellationToken ct);

    // Seccion 6 - Zonas Comunes
    Task<IReadOnlyList<ZonaComunDto>> ListZonasComunesAsync(CancellationToken ct);
    Task<ZonaComunDto> CrearZonaComunAsync(CrearZonaComunRequest req, CancellationToken ct);
    Task<ZonaComunDto?> ActualizarZonaComunAsync(Guid zonaId, ActualizarZonaComunRequest req, CancellationToken ct);
    Task<bool> CambiarEstadoZonaAsync(Guid zonaId, CambiarEstadoZonaRequest req, CancellationToken ct);
    Task<bool> EliminarZonaComunAsync(Guid zonaId, CancellationToken ct);

    // Seccion 7 - Equipos Activos
    Task<IReadOnlyList<EquipoActivoDto>> ListEquiposAsync(CancellationToken ct);
    Task<EquipoActivoDto> CrearEquipoAsync(CrearEquipoActivoRequest req, CancellationToken ct);
    Task<EquipoActivoDto?> ActualizarEquipoAsync(Guid id, ActualizarEquipoActivoRequest req, CancellationToken ct);
    Task<bool> CambiarEstadoEquipoAsync(Guid equipoId, CambiarEstadoEquipoRequest req, CancellationToken ct);
    Task<bool> EliminarEquipoAsync(Guid equipoId, CancellationToken ct);

    // Ventanas de disponibilidad reutilizables (equipos reservables, zonas comunes).
    Task<IReadOnlyList<VentanaDisponibilidadDto>> ListVentanasAsync(Propia.Domain.Enums.TipoEntidadDisponibilidad tipo, Guid entidadId, CancellationToken ct);
    Task GuardarVentanasAsync(GuardarVentanasRequest req, CancellationToken ct);

    // Ficha tecnica completa del equipo/activo (modal grande estilo prototipo).
    Task<EquipoFichaDto?> GetEquipoFichaAsync(Guid equipoId, CancellationToken ct);
    Task<EquipoFotoDto?> AgregarFotoEquipoAsync(Guid equipoId, string url, CancellationToken ct);
    Task<bool> EliminarFotoEquipoAsync(Guid fotoId, CancellationToken ct);
    Task<EquipoMejoraDto?> AgregarMejoraEquipoAsync(Guid equipoId, AgregarMejoraRequest req, CancellationToken ct);
    Task<bool> EliminarMejoraEquipoAsync(Guid mejoraId, CancellationToken ct);
    Task ToggleActivoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct);
    Task ToggleContratoVinculadoAsync(Guid equipoId, ToggleVinculoRequest req, CancellationToken ct);
    Task<EquipoCampoDto?> AgregarCampoEquipoAsync(Guid equipoId, AgregarCampoRequest req, CancellationToken ct);
    Task<bool> EliminarCampoEquipoAsync(Guid campoId, CancellationToken ct);

    // Ficha completa de zona comun (seccion 4, prototipo zonaDet).
    Task<ZonaFichaDto?> GetZonaFichaAsync(Guid zonaId, Guid? personaId, CancellationToken ct);
    Task<bool> GuardarZonaFichaAsync(Guid zonaId, GuardarZonaFichaRequest req, CancellationToken ct);
    Task<string?> SetZonaImagenAsync(Guid zonaId, string url, CancellationToken ct);
    Task<ZonaFacturaDto?> AgregarZonaFacturaAsync(Guid zonaId, AgregarZonaFacturaRequest req, CancellationToken ct);
    Task<bool> EliminarZonaFacturaAsync(Guid facturaId, CancellationToken ct);
    Task<ZonaDocumentoDto?> AgregarZonaDocumentoAsync(Guid zonaId, string nombre, string url, CancellationToken ct);
    Task<bool> EliminarZonaDocumentoAsync(Guid docId, CancellationToken ct);
    Task<ZonaCampoDto?> AgregarZonaCampoAsync(Guid zonaId, AgregarZonaCampoRequest req, CancellationToken ct);
    Task<bool> EliminarZonaCampoAsync(Guid campoId, CancellationToken ct);

    // Seccion 8 - Finanzas (parametros). El resumen en tiempo real lo orquesta el controller
    // combinando 2.6 Presupuesto + 2.7 Cartera.
    IReadOnlyList<MonedaDto> ListMonedas();
    Task<FinanzasParametrosDto> GetFinanzasParametrosAsync(Guid tenantId, CancellationToken ct);
    Task<FinanzasParametrosDto> ActualizarFinanzasAsync(Guid tenantId, ActualizarFinanzasRequest req, CancellationToken ct);

    // Configuracion avanzada de Finanzas (RN: bloque "Mas informacion" del modal).
    Task<ConfiguracionFinanzasDto> GetConfiguracionFinanzasAsync(Guid tenantId, CancellationToken ct);
    Task<ConfiguracionFinanzasDto> ActualizarConfiguracionFinanzasAsync(Guid tenantId, ActualizarConfiguracionFinanzasRequest req, CancellationToken ct);

    // Cuentas bancarias de la copropiedad.
    Task<IReadOnlyList<CuentaBancariaDto>> ListCuentasBancariasAsync(CancellationToken ct);
    Task<CuentaBancariaDto> CrearCuentaBancariaAsync(CrearCuentaBancariaRequest req, CancellationToken ct);
    Task<CuentaBancariaDto?> ActualizarCuentaBancariaAsync(Guid id, ActualizarCuentaBancariaRequest req, CancellationToken ct);
    Task<bool> EliminarCuentaBancariaAsync(Guid id, CancellationToken ct);

    // Bitacora de cambios (RN-06)
    Task<IReadOnlyList<BitacoraEntradaDto>> ListBitacoraAsync(int limit, CancellationToken ct);

    /// <summary>
    /// Registra una entrada en la bitacora de la copropiedad activa. Lo usan los
    /// metodos del servicio y la Capa MCP para auditar acciones de agentes (RN-06).
    /// </summary>
    Task RegistrarBitacoraAsync(string categoria, string descripcion, CancellationToken ct);
}
