using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Propia.Application.Common;
using Propia.Domain.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de PROPIA. Aplica:
/// 1. Auditoria automatica (CreatedAt / UpdatedAt) en SaveChangesAsync.
/// 2. Asignacion automatica de TenantId a entidades TenantEntity nuevas.
/// 3. HasQueryFilter por tenant en todas las TenantEntity como red de seguridad.
///    (La red final es Row-Level Security de PostgreSQL, configurada en migracion posterior.)
///
/// Hereda de IdentityDbContext para integrar ASP.NET Core Identity (paso 6).
/// Las tablas de Identity (asp_net_users, asp_net_roles, ...) son GLOBALES,
/// no llevan tenant_id. La gestion de quien tiene acceso a que copropiedad
/// vive en UsuarioTenant (modulo 2.5).
/// </summary>
public partial class PropiaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenantContext;

    public PropiaDbContext(DbContextOptions<PropiaDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Entidades globales (sin tenant_id)
    // Llaves de Data Protection (infra global, sin tenant_id ni RLS). Persisten los secretos
    // cifrados a traves de redeploys en produccion (ver AddDataProtection en DependencyInjection).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Organizacion> Organizaciones => Set<Organizacion>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<UsuarioContactoNotificacion> UsuarioContactosNotificacion => Set<UsuarioContactoNotificacion>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<SuperAdminUsuario> SuperAdminUsuarios => Set<SuperAdminUsuario>();
    public DbSet<SuperAdminLog> SuperAdminLogs => Set<SuperAdminLog>();

    // Integraciones de plataforma (Super Admin) - portadas de CUBOT.travels. Globales singleton.
    public DbSet<EmailConfig> EmailConfigs => Set<EmailConfig>();
    public DbSet<GoogleAuthConfig> GoogleAuthConfigs => Set<GoogleAuthConfig>();
    public DbSet<GmailEnvioAppConfig> GmailEnvioAppConfigs => Set<GmailEnvioAppConfig>();
    public DbSet<GmailEnvioConexion> GmailEnvioConexiones => Set<GmailEnvioConexion>();
    public DbSet<PlatformBranding> PlatformBrandings => Set<PlatformBranding>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<OcrProviderConfig> OcrProviderConfigs => Set<OcrProviderConfig>();

    // Menu de navegacion configurable (global plataforma): overrides de nombre/orden/ubicacion.
    public DbSet<MenuOverride> MenuOverrides => Set<MenuOverride>();
    public DbSet<WompiMasterConfig> WompiMasterConfigs => Set<WompiMasterConfig>();
    public DbSet<WompiWebhookEvent> WompiWebhookEvents => Set<WompiWebhookEvent>();
    public DbSet<EvolutionMasterConfig> EvolutionMasterConfigs => Set<EvolutionMasterConfig>();

    // Entidades de tenant (con tenant_id + RLS + HasQueryFilter)
    public DbSet<UsuarioTenant> UsuariosTenant => Set<UsuarioTenant>();

    // Modulo 2.3 Mi Copropiedad - todas TenantEntity (RLS + tenant_id)
    public DbSet<Torre> Torres => Set<Torre>();
    public DbSet<UnidadPrivada> UnidadesPrivadas => Set<UnidadPrivada>();
    public DbSet<UnidadVinculo> UnidadVinculos => Set<UnidadVinculo>();
    public DbSet<UnidadPersona> UnidadPersonas => Set<UnidadPersona>();
    public DbSet<UnidadCampoDefinicion> UnidadCamposDefiniciones => Set<UnidadCampoDefinicion>();
    public DbSet<UnidadCampoValor> UnidadCamposValores => Set<UnidadCampoValor>();
    public DbSet<UnidadDocumento> UnidadDocumentos => Set<UnidadDocumento>();
    public DbSet<UnidadPlaca> UnidadPlacas => Set<UnidadPlaca>();
    public DbSet<UnidadArriendo> UnidadArriendos => Set<UnidadArriendo>();
    public DbSet<UnidadMascota> UnidadMascotas => Set<UnidadMascota>();
    public DbSet<UnidadEmpleada> UnidadEmpleadas => Set<UnidadEmpleada>();
    public DbSet<UnidadTitularidad> UnidadTitularidades => Set<UnidadTitularidad>();
    public DbSet<UnidadPersonaCampo> UnidadPersonaCampos => Set<UnidadPersonaCampo>();
    public DbSet<BitacoraMiCopropiedad> BitacoraMiCopropiedad => Set<BitacoraMiCopropiedad>();
    public DbSet<ZonaComun> ZonasComunes => Set<ZonaComun>();
    public DbSet<EquipoActivo> EquiposActivos => Set<EquipoActivo>();
    public DbSet<ContratoServicio> ContratosServicio => Set<ContratoServicio>();
    public DbSet<ContratoCampo> ContratoCampos => Set<ContratoCampo>();
    public DbSet<ContratoCampoValor> ContratoCampoValores => Set<ContratoCampoValor>();
    public DbSet<ContratoEtapa> ContratoEtapas => Set<ContratoEtapa>();
    public DbSet<ContratoExpediente> ContratoExpedientes => Set<ContratoExpediente>();
    // Modulo Seguros (Ola 4)
    public DbSet<Poliza> Polizas => Set<Poliza>();
    public DbSet<PolizaCampo> PolizaCampos => Set<PolizaCampo>();
    public DbSet<PolizaCampoValor> PolizaCampoValores => Set<PolizaCampoValor>();
    public DbSet<PolizaReclamacion> PolizaReclamaciones => Set<PolizaReclamacion>();
    // Informes de gestion (plantillas inteligentes + generacion IA)
    public DbSet<InformePlantilla> InformePlantillas => Set<InformePlantilla>();
    public DbSet<InformePlantillaSeccion> InformePlantillaSecciones => Set<InformePlantillaSeccion>();
    public DbSet<Informe> Informes => Set<Informe>();
    public DbSet<InformeSeccion> InformeSecciones => Set<InformeSeccion>();
    // Servicios y contratos (Finanzas)
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<ServicioContacto> ServicioContactos => Set<ServicioContacto>();
    public DbSet<ServicioAdjunto> ServicioAdjuntos => Set<ServicioAdjunto>();
    public DbSet<ContratoAdjunto> ContratoAdjuntos => Set<ContratoAdjunto>();
    // Programador de tareas (2.10)
    public DbSet<ProgramacionTarea> ProgramacionTareas => Set<ProgramacionTarea>();
    public DbSet<ProgramacionTareaResponsable> ProgramacionTareaResponsables => Set<ProgramacionTareaResponsable>();
    // Modulo 2.17 Servicios publicos
    public DbSet<CuentaServicioPublico> CuentasServicioPublico => Set<CuentaServicioPublico>();
    public DbSet<RegistroConsumoServicio> RegistrosConsumoServicio => Set<RegistroConsumoServicio>();
    public DbSet<ReclamacionServicio> ReclamacionesServicio => Set<ReclamacionServicio>();
    public DbSet<MiembroConsejo> MiembrosConsejo => Set<MiembroConsejo>();
    public DbSet<TipoUnidadCustom> TiposUnidadCustom => Set<TipoUnidadCustom>();
    public DbSet<TipoCoeficiente> TiposCoeficiente => Set<TipoCoeficiente>();
    public DbSet<UnidadCoeficiente> UnidadCoeficientes => Set<UnidadCoeficiente>();
    public DbSet<Comite> Comites => Set<Comite>();
    public DbSet<ComiteMiembro> ComiteMiembros => Set<ComiteMiembro>();
    public DbSet<RevisorFiscal> RevisoresFiscales => Set<RevisorFiscal>();
    public DbSet<MiembroEquipo> MiembrosEquipo => Set<MiembroEquipo>();

    // Modulo 2.4 Directorio
    public DbSet<EtiquetaCatalogo> EtiquetasCatalogo => Set<EtiquetaCatalogo>();  // global+tenant mezclados via TenantId nullable
    public DbSet<DirectorioVinculo> DirectorioVinculos => Set<DirectorioVinculo>();
    public DbSet<DirectorioContacto> DirectorioContactos => Set<DirectorioContacto>();
    public DbSet<DirectorioAdjunto> DirectorioAdjuntos => Set<DirectorioAdjunto>();
    public DbSet<DirectorioEtiqueta> DirectorioEtiquetas => Set<DirectorioEtiqueta>();
    public DbSet<PersonaEmpresa> PersonaEmpresas => Set<PersonaEmpresa>();

    // Modulo 2.5 Usuarios, Roles y Accesos
    // (RolesCopropiedad para no colisionar con IdentityDbContext.Roles de IdentityRole)
    public DbSet<Rol> RolesCopropiedad => Set<Rol>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<RolSemillaTenant> RolesSemillaTenant => Set<RolSemillaTenant>();
    public DbSet<UsuarioInvitacion> UsuarioInvitaciones => Set<UsuarioInvitacion>();
    public DbSet<UsuarioAuthMetodo> UsuarioAuthMetodos => Set<UsuarioAuthMetodo>();
    public DbSet<UsuarioSesion> UsuarioSesiones => Set<UsuarioSesion>();
    public DbSet<AccesoAuditoria> AccesoAuditorias => Set<AccesoAuditoria>();
    public DbSet<EtiquetaUsuario> EtiquetasUsuario => Set<EtiquetaUsuario>();
    public DbSet<UsuarioTenantEtiqueta> UsuarioTenantEtiquetas => Set<UsuarioTenantEtiqueta>();

    // Modulo 2.6 Presupuesto, Cuotas y Pagos
    public DbSet<Domain.Entities.Presupuesto> Presupuestos => Set<Domain.Entities.Presupuesto>();
    public DbSet<PresupuestoRubro> PresupuestoRubros => Set<PresupuestoRubro>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();
    public DbSet<LiquidacionUnidad> LiquidacionUnidades => Set<LiquidacionUnidad>();
    public DbSet<GastoPresupuestal> GastosPresupuestales => Set<GastoPresupuestal>();
    public DbSet<PagoCuota> PagosCuotas => Set<PagoCuota>();
    public DbSet<CuotaExtraordinaria> CuotasExtraordinarias => Set<CuotaExtraordinaria>();
    public DbSet<EjecucionPresupuestal> EjecucionesPresupuestales => Set<EjecucionPresupuestal>();
    public DbSet<AuditLogPresupuesto> AuditLogPresupuestos => Set<AuditLogPresupuesto>();

    // Modulo 1.3 Gestion de Equipo (Capa 1 - todas GLOBAL, FK a Organizacion)
    public DbSet<OrgCargo> OrgCargos => Set<OrgCargo>();
    public DbSet<OrgCargoPermiso> OrgCargoPermisos => Set<OrgCargoPermiso>();
    public DbSet<OrgColaborador> OrgColaboradores => Set<OrgColaborador>();
    public DbSet<OrgColaboradorPermiso> OrgColaboradorPermisos => Set<OrgColaboradorPermiso>();
    public DbSet<OrgColaboradorCopropiedad> OrgColaboradorCopropiedades => Set<OrgColaboradorCopropiedad>();
    public DbSet<OrgColaboradorHistorial> OrgColaboradorHistorial => Set<OrgColaboradorHistorial>();

    // Modulo 1.1 Panel y Dashboard Consolidado (Capa 1 - GLOBAL, FK a Organizacion)
    public DbSet<PanelSnapshotCopropiedad> PanelSnapshots => Set<PanelSnapshotCopropiedad>();
    public DbSet<PanelConfiguracionUsuario> PanelConfiguraciones => Set<PanelConfiguracionUsuario>();
    public DbSet<PanelFeedEvento> PanelFeedEventos => Set<PanelFeedEvento>();

    // Modulo 2.2 Dashboard de la Copropiedad (TenantEntity con RLS)
    public DbSet<AlertaCopropiedad> AlertasCopropiedad => Set<AlertaCopropiedad>();
    public DbSet<ActividadFeed> ActividadFeed => Set<ActividadFeed>();

    // Modulo 2.10 Tareas y Proyectos (TenantEntity con RLS)
    public DbSet<TareaEstado> TareasEstados => Set<TareaEstado>();
    public DbSet<TareaEtiqueta> TareaEtiquetas => Set<TareaEtiqueta>();
    public DbSet<TareaEtiquetaAsignacion> TareaEtiquetaAsignaciones => Set<TareaEtiquetaAsignacion>();
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<TareaColaborador> TareaColaboradores => Set<TareaColaborador>();
    public DbSet<TareaComentario> TareaComentarios => Set<TareaComentario>();
    public DbSet<TareaHistorial> TareaHistorial => Set<TareaHistorial>();
    public DbSet<TareaDependencia> TareaDependencias => Set<TareaDependencia>();
    // Tableros de trabajo (2.10)
    public DbSet<Tablero> Tableros => Set<Tablero>();
    public DbSet<TableroUsuario> TableroUsuarios => Set<TableroUsuario>();
    public DbSet<TableroCampo> TableroCampos => Set<TableroCampo>();
    public DbSet<TareaCampoValor> TareaCampoValores => Set<TareaCampoValor>();
    public DbSet<TareaAdjunto> TareaAdjuntos => Set<TareaAdjunto>();
    public DbSet<TareaSubtarea> TareaSubtareas => Set<TareaSubtarea>();

    // Modulo 2.7 Cartera y Estado de Cuenta (TenantEntity con RLS)
    public DbSet<EstadoCarteraConfig> EstadosCarteraConfig => Set<EstadoCarteraConfig>();
    public DbSet<CarteraConfig> CarteraConfigs => Set<CarteraConfig>();
    public DbSet<CarteraUnidad> CarteraUnidades => Set<CarteraUnidad>();
    public DbSet<DeudaDetalle> DeudaDetalles => Set<DeudaDetalle>();
    public DbSet<AcuerdoPago> AcuerdosPago => Set<AcuerdoPago>();
    public DbSet<AcuerdoCuota> AcuerdoCuotas => Set<AcuerdoCuota>();
    public DbSet<PazSalvoEmitido> PazSalvosEmitidos => Set<PazSalvoEmitido>();
    public DbSet<Condonacion> Condonaciones => Set<Condonacion>();
    public DbSet<CarteraHistorial> CarteraHistorial => Set<CarteraHistorial>();

    // Modulo 2.9 PQRSD y Convivencia (TenantEntity con RLS)
    public DbSet<PqrsdExpediente> PqrsdExpedientes => Set<PqrsdExpediente>();
    public DbSet<PqrsdCategoria> PqrsdCategorias => Set<PqrsdCategoria>();
    public DbSet<PqrsdAdjunto> PqrsdAdjuntos => Set<PqrsdAdjunto>();
    public DbSet<PqrsdHistorialEstado> PqrsdHistorialEstados => Set<PqrsdHistorialEstado>();
    public DbSet<PqrsdConfiguracionPlazo> PqrsdConfiguracionPlazos => Set<PqrsdConfiguracionPlazo>();
    public DbSet<PqrsdFormularioPublicoConfig> PqrsdFormularioPublicoConfigs => Set<PqrsdFormularioPublicoConfig>();
    public DbSet<PqrsdTareasConfig> PqrsdTareasConfigs => Set<PqrsdTareasConfig>();
    public DbSet<PqrsdComiteSesion> PqrsdComiteSesiones => Set<PqrsdComiteSesion>();
    public DbSet<PqrsdComiteMiembroSesion> PqrsdComiteMiembros => Set<PqrsdComiteMiembroSesion>();
    public DbSet<PqrsdEstado> PqrsdEstados => Set<PqrsdEstado>();
    public DbSet<PqrsdCampo> PqrsdCampos => Set<PqrsdCampo>();
    public DbSet<PqrsdCampoValor> PqrsdCampoValores => Set<PqrsdCampoValor>();
    public DbSet<PqrsdComentario> PqrsdComentarios => Set<PqrsdComentario>();
    public DbSet<PqrsdRespuesta> PqrsdRespuestas => Set<PqrsdRespuesta>();
    public DbSet<PqrsdRespuestaVersion> PqrsdRespuestaVersiones => Set<PqrsdRespuestaVersion>();
    public DbSet<PqrsdRespuestaDestinatario> PqrsdRespuestaDestinatarios => Set<PqrsdRespuestaDestinatario>();
    public DbSet<PqrsdPlantillaRespuesta> PqrsdPlantillasRespuesta => Set<PqrsdPlantillaRespuesta>();
    public DbSet<PqrsdPlantillaSemilla> PqrsdPlantillasSemilla => Set<PqrsdPlantillaSemilla>();
    public DbSet<PqrsdTipo> PqrsdTipos => Set<PqrsdTipo>();

    // Motivos de cierre configurables (compartido Tareas/PQRSD via discriminador Modulo)
    public DbSet<MotivoCierre> MotivosCierre => Set<MotivoCierre>();

    // Modulo 2.8 Asambleas y Organos de Gobierno (TenantEntity con RLS)
    public DbSet<Sesion> Sesiones => Set<Sesion>();
    public DbSet<SesionPunto> SesionPuntos => Set<SesionPunto>();
    public DbSet<SesionDocumento> SesionDocumentos => Set<SesionDocumento>();
    public DbSet<SesionParticipante> SesionParticipantes => Set<SesionParticipante>();
    public DbSet<SesionPoder> SesionPoderes => Set<SesionPoder>();
    public DbSet<SesionQuorumLog> SesionQuorumLog => Set<SesionQuorumLog>();
    public DbSet<Votacion> Votaciones => Set<Votacion>();
    public DbSet<Voto> Votos => Set<Voto>();
    public DbSet<Acta> Actas => Set<Acta>();
    public DbSet<EleccionConsejo> ElectionesConsejo => Set<EleccionConsejo>();
    public DbSet<EleccionCandidato> EleccionCandidatos => Set<EleccionCandidato>();
    public DbSet<AsambleaConfig> AsambleaConfigs => Set<AsambleaConfig>();

    // Modulo 2.11 Mantenimiento y Activos (TenantEntity con RLS)
    public DbSet<MantenimientoPlan> MantenimientoPlanes => Set<MantenimientoPlan>();
    public DbSet<MantenimientoIntervencion> MantenimientoIntervenciones => Set<MantenimientoIntervencion>();
    public DbSet<MantenimientoBitacora> MantenimientoBitacora => Set<MantenimientoBitacora>();
    public DbSet<MantenimientoAdjunto> MantenimientoAdjuntos => Set<MantenimientoAdjunto>();
    public DbSet<MantenimientoHistorialEstado> MantenimientoHistorialEstados => Set<MantenimientoHistorialEstado>();

    // Modulo 2.14 Comunicaciones (mezcla: ComunicadoPlantilla con TenantId nullable para globales)
    public DbSet<ComunicadoPlantilla> ComunicadoPlantillas => Set<ComunicadoPlantilla>();
    public DbSet<Comunicado> Comunicados => Set<Comunicado>();
    public DbSet<ComunicadoSegmento> ComunicadoSegmentos => Set<ComunicadoSegmento>();
    public DbSet<ComunicadoAdjunto> ComunicadoAdjuntos => Set<ComunicadoAdjunto>();
    public DbSet<ComunicadoDestinatario> ComunicadoDestinatarios => Set<ComunicadoDestinatario>();
    public DbSet<ComunicadoAcuse> ComunicadoAcuses => Set<ComunicadoAcuse>();

    // Modulo 2.15 Documentos y Archivo Digital
    // (Categoria y EtiquetaCatalogo con TenantId nullable para mezclar globales PropIA + tenant)
    public DbSet<DocumentoCategoria> DocumentoCategorias => Set<DocumentoCategoria>();
    public DbSet<DocumentoCarpeta> DocumentoCarpetas => Set<DocumentoCarpeta>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<DocumentoVersion> DocumentoVersiones => Set<DocumentoVersion>();
    public DbSet<DocumentoEtiquetaCatalogo> DocumentoEtiquetasCatalogo => Set<DocumentoEtiquetaCatalogo>();
    public DbSet<DocumentoEtiqueta> DocumentoEtiquetas => Set<DocumentoEtiqueta>();
    public DbSet<DocumentoDestacadoPersonal> DocumentoDestacadosPersonal => Set<DocumentoDestacadoPersonal>();
    public DbSet<DocumentoAuditoria> DocumentoAuditorias => Set<DocumentoAuditoria>();
    public DbSet<DocumentoConsumo> DocumentoConsumos => Set<DocumentoConsumo>();
    // Documentos 2.15 - vista Expedientes (TRD)
    public DbSet<SerieDocumental> SeriesDocumentales => Set<SerieDocumental>();
    public DbSet<SubserieDocumental> SubseriesDocumentales => Set<SubserieDocumental>();
    public DbSet<SubserieTipologia> SubserieTipologias => Set<SubserieTipologia>();
    public DbSet<SubserieCampo> SubserieCampos => Set<SubserieCampo>();
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<ExpedienteTipologia> ExpedienteTipologias => Set<ExpedienteTipologia>();
    public DbSet<ExpedienteTipologiaVersion> ExpedienteTipologiaVersiones => Set<ExpedienteTipologiaVersion>();
    public DbSet<ExpedienteCampo> ExpedienteCampos => Set<ExpedienteCampo>();

    // Modulo 2.16 Reportes e Indicadores
    // (Categoria y Catalogo con TenantId nullable para mezclar globales PropIA + tenant)
    public DbSet<ReporteCategoria> ReporteCategorias => Set<ReporteCategoria>();
    public DbSet<ReporteCatalogo> ReporteCatalogo => Set<ReporteCatalogo>();
    public DbSet<ReporteGenerado> ReporteGenerados => Set<ReporteGenerado>();
    public DbSet<ReporteProgramacion> ReporteProgramaciones => Set<ReporteProgramacion>();
    public DbSet<ReporteProgramacionDestinatario> ReporteProgramacionDestinatarios => Set<ReporteProgramacionDestinatario>();
    public DbSet<ReporteSemaforoConfig> ReporteSemaforoConfigs => Set<ReporteSemaforoConfig>();

    // Modulo 2.12 Porteria y Control de Acceso
    public DbSet<TurnoPorteria> TurnosPorteria => Set<TurnoPorteria>();
    public DbSet<VisitanteFrecuente> VisitantesFrecuentes => Set<VisitanteFrecuente>();
    public DbSet<AutorizacionPrevia> AutorizacionesPrevia => Set<AutorizacionPrevia>();
    public DbSet<CodigoIngreso> CodigosIngreso => Set<CodigoIngreso>();
    public DbSet<RegistroVisita> RegistrosVisita => Set<RegistroVisita>();
    public DbSet<VehiculoAutorizado> VehiculosAutorizados => Set<VehiculoAutorizado>();
    public DbSet<RegistroVehiculo> RegistrosVehiculo => Set<RegistroVehiculo>();
    public DbSet<Correspondencia> Correspondencias => Set<Correspondencia>();
    public DbSet<NovedadTurno> NovedadesTurno => Set<NovedadTurno>();
    public DbSet<PorteriaConfiguracion> PorteriaConfiguraciones => Set<PorteriaConfiguracion>();
    public DbSet<PorteriaCampo> PorteriaCampos => Set<PorteriaCampo>();

    // Modulo 2.13 Reservas de Zonas Comunes
    public DbSet<ZonaConfigReserva> ZonaConfigReservas => Set<ZonaConfigReserva>();
    public DbSet<ZonaFranja> ZonaFranjas => Set<ZonaFranja>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<PrestamoEquipo> PrestamosEquipo => Set<PrestamoEquipo>();
    public DbSet<EntregaFoto> EntregaFotos => Set<EntregaFoto>();
    public DbSet<ReservaRecurrente> ReservasRecurrentes => Set<ReservaRecurrente>();
    public DbSet<ReservaPago> ReservaPagos => Set<ReservaPago>();
    public DbSet<ZonaBloqueo> ZonaBloqueos => Set<ZonaBloqueo>();

    // Modulo 1.2 Calendario Multi-Copropiedad (global - FK Organizacion)
    public DbSet<CalendarioEvento> CalendarioEventos => Set<CalendarioEvento>();
    public DbSet<CalendarioConfigUsuario> CalendarioConfigUsuarios => Set<CalendarioConfigUsuario>();

    // Modulo 1.4 Reportes Consolidados (global - FK Organizacion)
    public DbSet<OrgReporte> OrgReportes => Set<OrgReporte>();
    public DbSet<OrgReporteGeneracion> OrgReporteGeneraciones => Set<OrgReporteGeneracion>();

    // Modulo 1.5 Transferencia de Custodia (global - FK 2 organizaciones + 1 copropiedad)
    public DbSet<TransferenciaCustodia> TransferenciasCustodia => Set<TransferenciaCustodia>();
    public DbSet<TransferenciaDocumento> TransferenciaDocumentos => Set<TransferenciaDocumento>();
    public DbSet<TransferenciaEvento> TransferenciaEventos => Set<TransferenciaEvento>();

    // T.2 Motor de Notificaciones (servicio comun - TenantId nullable porque sirve a Capa 0/1/2)
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    // Calendario habil Colombia (global, no tenant)
    public DbSet<FestivoColombiano> FestivosColombianos => Set<FestivoColombiano>();

    // Modulo 0.3 Monitoria y Auditoria Global (todo GLOBAL, sin tenant)
    public DbSet<SistemaLog> SistemaLogs => Set<SistemaLog>();
    public DbSet<SistemaIncidente> SistemaIncidentes => Set<SistemaIncidente>();
    public DbSet<MetricaUsoDiaria> MetricasUsoDiarias => Set<MetricaUsoDiaria>();

    // Background jobs (transversal, global)
    public DbSet<JobEjecucion> JobEjecuciones => Set<JobEjecucion>();

    // Infraestructura IA (Capa 2 - tenant-scoped, RLS). Lineas WhatsApp, Agentes, Chat, Consumo.
    public DbSet<WhatsAppLine> WhatsAppLines => Set<WhatsAppLine>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AiAgent> AiAgents => Set<AiAgent>();
    public DbSet<AiAgentPrompt> AiAgentPrompts => Set<AiAgentPrompt>();
    public DbSet<AiAgentResource> AiAgentResources => Set<AiAgentResource>();
    public DbSet<AiAgentLineBinding> AiAgentLineBindings => Set<AiAgentLineBinding>();
    public DbSet<AiAgentCacheField> AiAgentCacheFields => Set<AiAgentCacheField>();
    public DbSet<AiAgentCacheValue> AiAgentCacheValues => Set<AiAgentCacheValue>();
    public DbSet<AiAgentMcpTool> AiAgentMcpTools => Set<AiAgentMcpTool>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiAgentRunLog> AiAgentRunLogs => Set<AiAgentRunLog>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<AiAgentTemplate> AiAgentTemplates => Set<AiAgentTemplate>();
    public DbSet<AiAgentTemplateMcpTool> AiAgentTemplateMcpTools => Set<AiAgentTemplateMcpTool>();
    public DbSet<NumeroEnListaNegra> NumerosEnListaNegra => Set<NumeroEnListaNegra>();
    public DbSet<CuentaBancaria> CuentasBancarias => Set<CuentaBancaria>();
    public DbSet<VentanaDisponibilidad> VentanasDisponibilidad => Set<VentanaDisponibilidad>();
    public DbSet<EquipoFoto> EquipoFotos => Set<EquipoFoto>();
    public DbSet<EquipoMejora> EquipoMejoras => Set<EquipoMejora>();
    public DbSet<EquipoVinculo> EquipoVinculos => Set<EquipoVinculo>();
    public DbSet<EquipoContratoVinculo> EquipoContratoVinculos => Set<EquipoContratoVinculo>();
    public DbSet<EquipoCampoPersonalizado> EquipoCamposPersonalizados => Set<EquipoCampoPersonalizado>();

    // Ficha de zona comun (2.3 seccion 4)
    public DbSet<ZonaFactura> ZonaFacturas => Set<ZonaFactura>();
    public DbSet<ZonaDocumento> ZonaDocumentos => Set<ZonaDocumento>();
    public DbSet<ZonaCampoPersonalizado> ZonaCamposPersonalizados => Set<ZonaCampoPersonalizado>();
    // Muro de novedades generico: cuelga de cualquier entidad via (EntidadTipo, EntidadId).
    public DbSet<Novedad> Novedades => Set<Novedad>();
    public DbSet<NovedadComentario> NovedadComentarios => Set<NovedadComentario>();
    public DbSet<NovedadLike> NovedadLikes => Set<NovedadLike>();

    // Modulo 0.2 - Billing y Suscripciones (todo GLOBAL, sin tenant_id)
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<Cupon> Cupones => Set<Cupon>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<IntentoCobro> IntentosCobro => Set<IntentoCobro>();
    public DbSet<SuscripcionHistorial> SuscripcionHistorial => Set<SuscripcionHistorial>();
    public DbSet<BillingConfig> BillingConfig => Set<BillingConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Convencion: tablas en snake_case
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        // Configuracion de modelo particionada por modulo (ver carpeta ModelConfig/).
        // El orden se conserva identico al original para no alterar el modelo EF.
        ConfigureGlobalesCore(modelBuilder);
        ConfigureMiCopropiedad(modelBuilder);
        ConfigureDirectorioUsuariosPresupuesto(modelBuilder);
        ConfigureCapa1YTareas(modelBuilder);
        ConfigureCarteraYPqrsd(modelBuilder);
        ConfigureAsambleasMantenimientoComunicaciones(modelBuilder);
        ConfigureDocumentosYReportes(modelBuilder);
        ConfigurePorteriaReservasGlobales(modelBuilder);
        ConfigureAuditoriaIaBilling(modelBuilder);

    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                AssignTenantIdIfNeeded(entry);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        // RLS lo cubre TenantConnectionInterceptor.ConnectionOpenedAsync, que
        // setea app.tenant_id en cada conexion abierta por EF. Aqui solo
        // delegamos al base SaveChangesAsync.
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AssignTenantIdIfNeeded(EntityEntry<BaseEntity> entry)
    {
        if (entry.Entity is TenantEntity tenantEntity && tenantEntity.TenantId == Guid.Empty)
        {
            if (_tenantContext.CurrentTenantId is null)
            {
                throw new InvalidOperationException(
                    $"No se puede insertar una TenantEntity ({entry.Entity.GetType().Name}) sin TenantId. " +
                    "Asegurate de que ITenantContext.SetTenant fue llamado antes de SaveChangesAsync.");
            }
            tenantEntity.TenantId = _tenantContext.CurrentTenantId.Value;
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToLowerInvariant(name[0]));
        for (int i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
