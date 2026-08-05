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
public class PropiaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
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
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<SuperAdminUsuario> SuperAdminUsuarios => Set<SuperAdminUsuario>();
    public DbSet<SuperAdminLog> SuperAdminLogs => Set<SuperAdminLog>();

    // Integraciones de plataforma (Super Admin) - portadas de CUBOT.travels. Globales singleton.
    public DbSet<EmailConfig> EmailConfigs => Set<EmailConfig>();
    public DbSet<GoogleAuthConfig> GoogleAuthConfigs => Set<GoogleAuthConfig>();
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
    public DbSet<PqrsdComiteSesion> PqrsdComiteSesiones => Set<PqrsdComiteSesion>();
    public DbSet<PqrsdComiteMiembroSesion> PqrsdComiteMiembros => Set<PqrsdComiteMiembroSesion>();

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

        // Organizacion
        modelBuilder.Entity<Organizacion>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Nit).HasMaxLength(20);
            b.Property(x => x.Email).HasMaxLength(200).HasColumnType("citext");
            b.HasIndex(x => x.Nit).IsUnique().HasFilter("nit IS NOT NULL");
        });

        // Tenant (Copropiedad)
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Nit).HasMaxLength(20);
            b.Property(x => x.CodigoPropia).HasMaxLength(20);
            b.HasIndex(x => x.CodigoPropia).IsUnique().HasFilter("codigo_propia IS NOT NULL");
            b.HasIndex(x => x.Nit).HasFilter("nit IS NOT NULL");

            b.HasOne(x => x.Organizacion)
                .WithMany(x => x.Copropiedades)
                .HasForeignKey(x => x.OrganizacionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Persona
        modelBuilder.Entity<Persona>(b =>
        {
            b.Property(x => x.Documento).IsRequired().HasMaxLength(20);
            b.Property(x => x.Nombres).IsRequired().HasMaxLength(150);
            b.Property(x => x.Apellidos).IsRequired().HasMaxLength(150);
            b.Property(x => x.Email).HasMaxLength(200).HasColumnType("citext");
            b.HasIndex(x => new { x.TipoDocumento, x.Documento }).IsUnique();
            b.HasIndex(x => x.Email).IsUnique().HasFilter("email IS NOT NULL");
        });

        // Empresa
        modelBuilder.Entity<Empresa>(b =>
        {
            b.Property(x => x.Nit).IsRequired().HasMaxLength(20);
            b.Property(x => x.RazonSocial).IsRequired().HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(200).HasColumnType("citext");
            b.HasIndex(x => x.Nit).IsUnique();
        });

        // -------------------- Modulo 2.3 Mi Copropiedad --------------------

        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Ciudad).HasMaxLength(100);
            b.Property(x => x.Departamento).HasMaxLength(100);
            b.Property(x => x.DigitoVerificacion).HasMaxLength(2);
            b.Property(x => x.FotoFachadaUrl).HasMaxLength(500);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.TelefonoContacto).HasMaxLength(50);
            b.Property(x => x.EmailContacto).HasMaxLength(200);
            // Identidad registral (modulo 2.3 spec v1.0)
            b.Property(x => x.NumeroReglamentoPh).HasMaxLength(100);
            b.Property(x => x.NotariaRegistro).HasMaxLength(200);
            b.Property(x => x.MatriculaInmobiliaria).HasMaxLength(50);
            b.Property(x => x.LicenciaConstruccion).HasMaxLength(50);
            b.Property(x => x.CertificadoMayorExtension).HasMaxLength(100);
            // Labels personalizables (spec v1.0 - "Sector"/"Planta")
            b.Property(x => x.LabelAgrupacion).HasMaxLength(30);
            b.Property(x => x.LabelPiso).HasMaxLength(30);
            // Parametros financieros (seccion 8)
            b.Property(x => x.Moneda).HasMaxLength(3).HasDefaultValue("COP");
            b.Property(x => x.DiaCorte).HasDefaultValue(1);
            b.Property(x => x.TasaMoraEsLegal).HasDefaultValue(true);
            b.Property(x => x.TasaMoraValor).HasPrecision(6, 4);
        });

        modelBuilder.Entity<Torre>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPrivada>(b =>
        {
            b.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            b.Property(x => x.Estado).HasMaxLength(50);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.MatriculaInmobiliaria).HasMaxLength(50);
            b.Property(x => x.CoeficientePropiedad).HasPrecision(7, 4);
            b.Property(x => x.AreaM2).HasPrecision(10, 2);
            b.Property(x => x.CuotaMensual).HasPrecision(14, 2);
            b.HasOne(x => x.Torre).WithMany().HasForeignKey(x => x.TorreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<BitacoraMiCopropiedad>(b =>
        {
            b.Property(x => x.Categoria).IsRequired().HasMaxLength(50);
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(1000);
            b.Property(x => x.Autor).HasMaxLength(200);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadVinculo>(b =>
        {
            b.HasOne(x => x.UnidadPrincipal).WithMany().HasForeignKey(x => x.UnidadPrincipalId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.UnidadAsociada).WithMany().HasForeignKey(x => x.UnidadAsociadaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadAsociadaId }).IsUnique();  // una asociada tiene un solo principal
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPersona>(b =>
        {
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            // Antiduplicado por tipo de entidad: se implementan como indices PARCIALES en la migracion
            // (uno WHERE persona_id IS NOT NULL, otro WHERE empresa_id IS NOT NULL). No se declara
            // aqui el unique compuesto porque con persona_id/empresa_id nullable Postgres trata los
            // NULL como distintos y no protegeria las filas de empresa.
            b.HasIndex(x => new { x.TenantId, x.UnidadId, x.EmpresaId, x.Rol });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadCampoDefinicion>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(80);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Label }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadCampoValor>(b =>
        {
            b.HasOne(x => x.Definicion).WithMany().HasForeignKey(x => x.DefinicionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DefinicionId, x.UnidadId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadDocumento>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.Url).IsRequired();
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Prototipo v3 - bloques nuevos de la ficha de inmueble (placas, arriendos, mascotas, empleadas)
        modelBuilder.Entity<UnidadPlaca>(b =>
        {
            b.Property(x => x.Placa).IsRequired().HasMaxLength(15);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadArriendo>(b =>
        {
            b.Property(x => x.Concepto).IsRequired().HasMaxLength(120);
            b.Property(x => x.Referencia).HasMaxLength(120);
            b.Property(x => x.ValorMensual).HasPrecision(14, 2);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadMascota>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Raza).HasMaxLength(80);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadEmpleada>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Documento).HasMaxLength(40);
            b.Property(x => x.Celular).HasMaxLength(40);
            b.Property(x => x.Horario).HasMaxLength(120);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Prototipo v3 Oleada 2 - historico de titularidad + campos dinamicos por persona
        modelBuilder.Entity<UnidadTitularidad>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(160);
            b.Property(x => x.Rol).HasMaxLength(40);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPersonaCampo>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(80);
            b.Property(x => x.Valor).HasMaxLength(400);
            b.HasOne(x => x.UnidadPersona).WithMany().HasForeignKey(x => x.UnidadPersonaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadPersonaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaComun>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.HorariosUso).HasMaxLength(200);
            b.Property(x => x.ReglasUso).HasMaxLength(2000);
            b.Property(x => x.TarifaReserva).HasPrecision(12, 2);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EquipoActivo>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Marca).HasMaxLength(100);
            b.Property(x => x.Modelo).HasMaxLength(100);
            b.Property(x => x.NumeroSerie).HasMaxLength(100);
            b.Property(x => x.Ubicacion).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ContratoServicio>(b =>
        {
            b.Property(x => x.Proveedor).IsRequired().HasMaxLength(200);
            b.Property(x => x.NitProveedor).HasMaxLength(20);
            b.Property(x => x.Contacto).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.ValorMensual).HasPrecision(12, 2);
            b.Property(x => x.Estado).HasDefaultValue(EstadoContrato.Vigente);
            b.Property(x => x.DiasAnticipacionAlerta).HasDefaultValue(30);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.FechaFin);
            b.HasIndex(x => x.ServicioId);
            b.HasMany(x => x.Adjuntos).WithOne(a => a.Contrato!).HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Servicios y contratos (Finanzas)
        modelBuilder.Entity<Servicio>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.EjecutorNombre).HasMaxLength(200);
            b.Property(x => x.CostoMensual).HasPrecision(14, 2);
            b.Property(x => x.CostoAnual).HasPrecision(14, 2);
            b.Property(x => x.Estado).HasDefaultValue(EstadoServicio.Activo);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Contactos).WithOne(c => c.Servicio!).HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Adjuntos).WithOne(a => a.Servicio!).HasForeignKey(a => a.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Contratos).WithOne(c => c.Servicio!).HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ServicioContacto>(b =>
        {
            b.Property(x => x.NombreSnapshot).IsRequired().HasMaxLength(200);
            b.Property(x => x.Rol).HasMaxLength(80);
            b.Property(x => x.Telefono).HasMaxLength(40);
            b.Property(x => x.Email).HasMaxLength(160);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ServicioAdjunto>(b =>
        {
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).HasMaxLength(120);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ContratoAdjunto>(b =>
        {
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).HasMaxLength(120);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ContratoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Muro de novedades generico (nacio en zonas comunes, hoy sirve para cualquier entidad).
        modelBuilder.Entity<Novedad>(b =>
        {
            b.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
        });
        modelBuilder.Entity<NovedadComentario>(b => b.HasIndex(x => x.NovedadId));
        modelBuilder.Entity<NovedadLike>(b => b.HasIndex(x => new { x.NovedadId, x.PersonaId }));

        // Programador de tareas (2.10)
        modelBuilder.Entity<ProgramacionTarea>(b =>
        {
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(40);
            b.Property(x => x.OrigenReferencia).HasMaxLength(200);
            b.Property(x => x.CronExpresion).HasMaxLength(120);
            b.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(60).HasDefaultValue("America/Bogota");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.Activa, x.FechaProximaEjecucion });
            // El job barre por esta columna en modo cron; sin indice haria seq scan cada 15 min.
            b.HasIndex(x => new { x.Activa, x.ProximaEjecucionUtc });
            b.HasMany(x => x.Responsables).WithOne(r => r.ProgramacionTarea!).HasForeignKey(r => r.ProgramacionTareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ProgramacionTareaResponsable>(b =>
        {
            b.Property(x => x.NombreSnapshot).HasMaxLength(200);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ProgramacionTareaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Modulo 2.17 Servicios publicos
        modelBuilder.Entity<CuentaServicioPublico>(b =>
        {
            b.Property(x => x.Alias).IsRequired().HasMaxLength(120);
            b.Property(x => x.Prestador).HasMaxLength(160);
            b.Property(x => x.NumeroCuenta).HasMaxLength(80);
            b.Property(x => x.MetodoPago).HasMaxLength(80);
            b.Property(x => x.UnidadMedida).HasMaxLength(20);
            b.Property(x => x.UmbralAlertaPct).HasDefaultValue(25);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Registros).WithOne(r => r.Cuenta!).HasForeignKey(r => r.CuentaServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Reclamaciones).WithOne(r => r.Cuenta!).HasForeignKey(r => r.CuentaServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<RegistroConsumoServicio>(b =>
        {
            b.Property(x => x.Consumo).HasPrecision(14, 2);
            b.Property(x => x.Valor).HasPrecision(14, 2);
            b.Property(x => x.NotaAdmin).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CuentaServicioId, x.Anio, x.Mes });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ReclamacionServicio>(b =>
        {
            b.Property(x => x.Motivo).IsRequired().HasMaxLength(160);
            b.Property(x => x.Radicado).HasMaxLength(80);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CuentaServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MiembroConsejo>(b =>
        {
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Cargo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Tipo unidad custom (spec 2.3 - tipos personalizables por copropiedad)
        modelBuilder.Entity<TipoUnidadCustom>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Tipos de coeficiente PH (spec 2.3 - RN-02 multiples tipos por copropiedad)
        modelBuilder.Entity<TipoCoeficiente>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Valores de coeficientes por unidad (relacion M-N + Valor)
        modelBuilder.Entity<UnidadCoeficiente>(b =>
        {
            b.Property(x => x.Valor).HasPrecision(9, 6);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.TipoCoeficiente).WithMany().HasForeignKey(x => x.TipoCoeficienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.UnidadId, x.TipoCoeficienteId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Comites y miembros (spec 2.3 - seccion 4)
        modelBuilder.Entity<Comite>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComiteMiembro>(b =>
        {
            b.Property(x => x.CargoEnComite).HasMaxLength(80);
            b.HasOne(x => x.Comite).WithMany(c => c.Miembros).HasForeignKey(x => x.ComiteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ComiteId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RevisorFiscal>(b =>
        {
            b.Property(x => x.NumeroTarjetaProfesional).HasMaxLength(50);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Equipo de trabajo (spec 2.3 - seccion 3)
        modelBuilder.Entity<MiembroEquipo>(b =>
        {
            b.Property(x => x.RolPersonalizado).HasMaxLength(80);
            b.Property(x => x.Telefono).HasMaxLength(30);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PersonaId, x.Rol });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.4 Directorio --------------------

        // Catalogo de etiquetas: base (TenantId NULL) + custom por copropiedad.
        // El query filter permite ver las base + las del tenant activo.
        modelBuilder.Entity<EtiquetaCatalogo>(b =>
        {
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DirectorioVinculo>(b =>
        {
            b.Property(x => x.MotivoInactivacion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.EntidadTipo, x.EntidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DirectorioContacto>(b =>
        {
            b.Property(x => x.Valor).IsRequired().HasMaxLength(300);
            b.Property(x => x.SubtipoLabel).HasMaxLength(50);
            b.Property(x => x.Ciudad).HasMaxLength(100);
            b.Property(x => x.Departamento).HasMaxLength(100);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
            // GLOBAL por decision de producto: los contactos viajan con la identidad (persona/empresa)
            // y se reutilizan en cualquier copropiedad donde aparezca. Sin HasQueryFilter y sin RLS
            // (la policy tenant_isolation se elimina en migracion). tenant_id queda solo como registro
            // de que copropiedad capturo el dato; la lectura/escritura es por (EntidadTipo, EntidadId).
        });

        modelBuilder.Entity<DirectorioEtiqueta>(b =>
        {
            b.HasOne(x => x.Vinculo).WithMany().HasForeignKey(x => x.VinculoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Etiqueta).WithMany().HasForeignKey(x => x.EtiquetaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.VinculoId, x.EtiquetaId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PersonaEmpresa>(b =>
        {
            b.Property(x => x.Cargo).HasMaxLength(100);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.EmpresaId, x.PersonaId, x.Cargo });
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Representante legal FK opcional
        modelBuilder.Entity<Empresa>(b =>
        {
            b.HasOne(x => x.RepresentanteLegal)
                .WithMany()
                .HasForeignKey(x => x.RepresentanteLegalPersonaId)
                .OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.TipoEmpresa).HasMaxLength(100);
            b.Property(x => x.SectorEconomico).HasMaxLength(100);
            b.Property(x => x.RegimenTributario).HasMaxLength(100);
            b.Property(x => x.SitioWeb).HasMaxLength(200);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Persona>(b =>
        {
            b.Property(x => x.VersionPoliticaDatos).HasMaxLength(20);
            b.Property(x => x.IpAceptacion).HasMaxLength(45);
        });

        // -------------------- Modulo 2.5 Usuarios, Roles y Accesos --------------------

        // Rol: GLOBAL (tenant_id nullable). Las base + extendidos tienen tenant_id NULL
        // y se ven en todas las copropiedades. Los personalizados llevan tenant_id.
        modelBuilder.Entity<Rol>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.FacetasSemilla).HasMaxLength(40);
            b.Property(x => x.SoloDirectorio).HasDefaultValue(false);
            b.HasOne(x => x.CopiadoDeRol).WithMany().HasForeignKey(x => x.CopiadoDeRolId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            // Query filter: ve sus propios + los globales (tenant_id NULL)
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RolPermiso>(b =>
        {
            b.Property(x => x.ModuloCodigo).IsRequired().HasMaxLength(50);
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RolId, x.ModuloCodigo, x.Accion }).IsUnique();
            // No tiene tenant_id - se hereda del rol. Sin query filter ya que se accede
            // siempre via rol_id y el rol ya esta filtrado.
        });

        // Config de siembra por copropiedad (override tenant-scoped, RLS). Aplica a cualquier rol.
        modelBuilder.Entity<RolSemillaTenant>(b =>
        {
            b.Property(x => x.FacetasSemilla).HasMaxLength(40);
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.RolId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UsuarioInvitacion>(b =>
        {
            b.Property(x => x.Token).IsRequired().HasMaxLength(128);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UsuarioAuthMetodo>(b =>
        {
            b.Property(x => x.ProveedorId).HasMaxLength(255);
            b.HasIndex(x => new { x.UsuarioId, x.Tipo });
        });

        modelBuilder.Entity<UsuarioSesion>(b =>
        {
            b.Property(x => x.TokenHash).IsRequired().HasMaxLength(255);
            b.Property(x => x.Dispositivo).HasMaxLength(255);
            b.Property(x => x.IpOrigen).HasMaxLength(45);
            b.HasIndex(x => x.UsuarioId);
            b.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<AccesoAuditoria>(b =>
        {
            b.Property(x => x.IpOrigen).HasMaxLength(45);
            b.Property(x => x.Dispositivo).HasMaxLength(255);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UsuarioId);
            b.HasIndex(x => x.TipoEvento);
            b.HasIndex(x => x.CreatedAt);
        });

        // -------------------- Modulo 2.6 Presupuesto, Cuotas y Pagos --------------------

        modelBuilder.Entity<Domain.Entities.Presupuesto>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.MontoTotal).HasPrecision(18, 2);
            b.Property(x => x.AprobacionActaUrl).HasMaxLength(500);
            b.Property(x => x.Notas).HasMaxLength(2000);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PresupuestoRubro>(b =>
        {
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.MontoAnual).HasPrecision(18, 2);
            b.Property(x => x.NotasInternas).HasMaxLength(1000);
            b.HasOne(x => x.Presupuesto).WithMany(p => p.Rubros).HasForeignKey(x => x.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.PresupuestoId, x.Codigo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<GastoPresupuestal>(b =>
        {
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.SoporteUrl).HasMaxLength(500);
            b.HasOne(x => x.Presupuesto).WithMany().HasForeignKey(x => x.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Rubro).WithMany().HasForeignKey(x => x.RubroId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.PresupuestoId, x.RubroId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Liquidacion>(b =>
        {
            b.Property(x => x.MontoTotal).HasPrecision(18, 2);
            b.Property(x => x.SnapshotCalculo).HasColumnType("text");
            b.HasOne(x => x.Presupuesto).WithMany().HasForeignKey(x => x.PresupuestoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.PresupuestoId, x.Periodo }).IsUnique();  // Idempotencia
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<LiquidacionUnidad>(b =>
        {
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.Desglose).HasColumnType("text");
            b.HasOne(x => x.Liquidacion).WithMany(l => l.Detalle).HasForeignKey(x => x.LiquidacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.LiquidacionId, x.UnidadPrivadaId }).IsUnique();
            b.HasIndex(x => x.EstadoPago);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PagoCuota>(b =>
        {
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.ReferenciaExterna).HasMaxLength(100);
            b.Property(x => x.Notas).HasMaxLength(1000);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LiquidacionUnidad).WithMany().HasForeignKey(x => x.LiquidacionUnidadId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CuotaExtraordinaria).WithMany().HasForeignKey(x => x.CuotaExtraordinariaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => x.ReferenciaExterna);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CuotaExtraordinaria>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Proposito).IsRequired().HasMaxLength(1000);
            b.Property(x => x.MontoTotal).HasPrecision(18, 2);
            b.Property(x => x.AprobacionActaUrl).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EjecucionPresupuestal>(b =>
        {
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(255);
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.SoporteUrl).HasMaxLength(500);
            b.HasOne(x => x.PresupuestoRubro).WithMany().HasForeignKey(x => x.PresupuestoRubroId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PresupuestoRubroId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AuditLogPresupuesto>(b =>
        {
            b.Property(x => x.Entidad).IsRequired().HasMaxLength(50);
            b.Property(x => x.Accion).IsRequired().HasMaxLength(50);
            b.Property(x => x.ValorAnterior).HasColumnType("text");
            b.Property(x => x.ValorNuevo).HasColumnType("text");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.Entidad, x.EntidadId });
            b.HasIndex(x => x.CreatedAt);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 1.3 Gestion de Equipo --------------------
        // Todas las entidades son GLOBAL (Capa 1) - sin tenant_id ni HasQueryFilter.
        // El aislamiento por organizacion se hace a nivel servicio (WHERE org_id = ...).

        modelBuilder.Entity<OrgCargo>(b =>
        {
            b.ToTable("org_cargos");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.Nombre }).IsUnique();  // RN-07
        });

        modelBuilder.Entity<OrgCargoPermiso>(b =>
        {
            b.ToTable("org_cargo_permisos");
            b.HasOne(x => x.Cargo).WithMany(c => c.Permisos).HasForeignKey(x => x.CargoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.CargoId);
            b.HasIndex(x => new { x.CargoId, x.Modulo }).IsUnique();
        });

        modelBuilder.Entity<OrgColaborador>(b =>
        {
            b.ToTable("org_colaboradores");
            b.Property(x => x.NotasIa).HasMaxLength(2000);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Cargo).WithMany(c => c.Colaboradores).HasForeignKey(x => x.CargoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.PersonaId }).IsUnique();  // RN-01 (una sola fila por persona por org)
            b.HasIndex(x => new { x.OrganizacionId, x.Estado });
            b.HasIndex(x => x.CargoId);
        });

        modelBuilder.Entity<OrgColaboradorPermiso>(b =>
        {
            b.ToTable("org_colaborador_permisos");
            b.HasOne(x => x.Colaborador).WithMany(c => c.PermisosIndividuales).HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.ColaboradorId);
            b.HasIndex(x => new { x.ColaboradorId, x.Modulo }).IsUnique();
        });

        modelBuilder.Entity<OrgColaboradorCopropiedad>(b =>
        {
            b.ToTable("org_colaborador_copropiedades");
            b.HasOne(x => x.Colaborador).WithMany(c => c.Asignaciones).HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RolCapa2).WithMany().HasForeignKey(x => x.RolCapa2Id).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ColaboradorId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ColaboradorId, x.TenantId }).IsUnique();  // RN-08 (una asignacion por par)
        });

        modelBuilder.Entity<OrgColaboradorHistorial>(b =>
        {
            b.ToTable("org_colaborador_historial");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(300);
            b.Property(x => x.ValorAnterior).HasColumnType("text");
            b.Property(x => x.ValorNuevo).HasColumnType("text");
            b.HasOne(x => x.Colaborador).WithMany(c => c.Historial).HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.ColaboradorId);
            b.HasIndex(x => new { x.ColaboradorId, x.OcurridoAt });
        });

        // -------------------- Modulo 1.1 Panel y Dashboard Consolidado --------------------
        // Tablas GLOBAL (Capa 1) - sin tenant_id ni HasQueryFilter. Filtro por OrganizacionId
        // a nivel servicio. El TenantId del snapshot es informativo, no aplica RLS.

        modelBuilder.Entity<PanelSnapshotCopropiedad>(b =>
        {
            b.ToTable("panel_snapshot_copropiedades");
            b.Property(x => x.RecaudoMesPorcentaje).HasPrecision(5, 2);
            b.Property(x => x.CarteraVencidaCop).HasPrecision(18, 2);
            b.Property(x => x.ProximoEventoTipo).HasMaxLength(50);
            b.Property(x => x.ProximoEventoLabel).HasMaxLength(200);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.EstadoSalud });
            b.HasIndex(x => new { x.OrganizacionId, x.TenantId }).IsUnique();
        });

        modelBuilder.Entity<PanelConfiguracionUsuario>(b =>
        {
            b.ToTable("panel_configuracion_usuarios");
            b.Property(x => x.KpisGlobales).HasColumnType("text");
            b.Property(x => x.TarjetaIndicadores).HasColumnType("text");
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.UsuarioId, x.OrganizacionId }).IsUnique();
        });

        modelBuilder.Entity<PanelFeedEvento>(b =>
        {
            b.ToTable("panel_feed_eventos");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(300);
            b.Property(x => x.EntidadTipo).HasMaxLength(50);
            b.Property(x => x.UrlAccion).HasMaxLength(500);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.OcurridoAt });
        });

        // -------------------- Modulo 2.2 Dashboard de la Copropiedad --------------------

        modelBuilder.Entity<AlertaCopropiedad>(b =>
        {
            b.ToTable("alertas_copropiedad");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.UrlAccion).HasMaxLength(500);
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(50);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Activa });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ActividadFeed>(b =>
        {
            b.ToTable("actividad_feed");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(300);
            b.Property(x => x.ActorNombre).HasMaxLength(150);
            b.Property(x => x.ModuloCodigo).HasMaxLength(50);
            b.Property(x => x.UrlItem).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.10 Tareas y Proyectos --------------------

        modelBuilder.Entity<TareaEstado>(b =>
        {
            b.ToTable("tarea_estados");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            // Unico por tablero (el mismo nombre de columna puede repetirse entre tableros distintos).
            b.HasIndex(x => new { x.TenantId, x.TableroId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaEtiqueta>(b =>
        {
            b.ToTable("tarea_etiquetas");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaEtiquetaAsignacion>(b =>
        {
            b.ToTable("tarea_etiqueta_asignaciones");
            b.HasOne(x => x.Tarea).WithMany(t => t.Etiquetas).HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Etiqueta).WithMany().HasForeignKey(x => x.EtiquetaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TareaId, x.EtiquetaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Tarea>(b =>
        {
            b.ToTable("tareas");
            b.Property(x => x.NumeroTarea).IsRequired().HasMaxLength(20);
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(4000);
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(50);
            b.Property(x => x.MotivoCancelacion).HasMaxLength(500);
            b.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.AsignadoPersona).WithMany().HasForeignKey(x => x.AsignadoPersonaId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Padre).WithMany(t => t.Subtareas).HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CopiaDe).WithMany(t => t.Copias).HasForeignKey(x => x.CopiaDeTareaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.NumeroTarea }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.EstadoId });
            b.HasIndex(x => new { x.TenantId, x.AsignadoPersonaId });
            b.HasIndex(x => new { x.TenantId, x.PadreId });
            b.HasIndex(x => new { x.TenantId, x.CopiaDeTareaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaColaborador>(b =>
        {
            b.ToTable("tarea_colaboradores");
            b.HasOne(x => x.Tarea).WithMany(t => t.Colaboradores).HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TareaId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaComentario>(b =>
        {
            b.ToTable("tarea_comentarios");
            b.Property(x => x.Texto).IsRequired().HasMaxLength(4000);
            b.HasOne(x => x.Tarea).WithMany(t => t.Comentarios).HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.TareaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaHistorial>(b =>
        {
            b.ToTable("tarea_historial");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(300);
            b.Property(x => x.ValorAnterior).HasColumnType("text");
            b.Property(x => x.ValorNuevo).HasColumnType("text");
            b.HasOne(x => x.Tarea).WithMany(t => t.Historial).HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TareaId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TareaDependencia>(b =>
        {
            b.ToTable("tarea_dependencias");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.HasOne(x => x.Tarea).WithMany(t => t.DependenciasDe)
                .HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.DependeDeTarea).WithMany(t => t.DependenciasA)
                .HasForeignKey(x => x.DependeDeTareaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TareaId, x.DependeDeTareaId }).IsUnique();
            b.HasIndex(x => x.DependeDeTareaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.7 Cartera y Estado de Cuenta --------------------

        modelBuilder.Entity<EstadoCarteraConfig>(b =>
        {
            b.ToTable("estados_cartera_config");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraConfig>(b =>
        {
            b.ToTable("cartera_config");
            b.Property(x => x.MensajePazSalvo).HasMaxLength(1000);
            b.Property(x => x.TasaMoraMensual).HasPrecision(8, 4);
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraUnidad>(b =>
        {
            b.ToTable("cartera_unidades");
            b.Property(x => x.SaldoCapital).HasPrecision(18, 2);
            b.Property(x => x.SaldoIntereses).HasPrecision(18, 2);
            b.Ignore(x => x.SaldoTotal);  // calculado
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.EstadoGestion).WithMany().HasForeignKey(x => x.EstadoGestionId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId }).IsUnique();
            b.HasIndex(x => x.EstadoGestionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DeudaDetalle>(b =>
        {
            b.ToTable("deuda_detalle");
            b.Property(x => x.Concepto).IsRequired().HasMaxLength(100);
            b.Property(x => x.CapitalOriginal).HasPrecision(18, 2);
            b.Property(x => x.CapitalPendiente).HasPrecision(18, 2);
            b.Property(x => x.InteresAcumulado).HasPrecision(18, 2);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LiquidacionUnidad).WithMany().HasForeignKey(x => x.LiquidacionUnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasIndex(x => x.LiquidacionUnidadId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AcuerdoPago>(b =>
        {
            b.ToTable("acuerdos_pago");
            b.Property(x => x.MontoTotal).HasPrecision(18, 2);
            b.Property(x => x.CapitalIncluido).HasPrecision(18, 2);
            b.Property(x => x.InteresesIncluidos).HasPrecision(18, 2);
            b.Property(x => x.InteresesCondonados).HasPrecision(18, 2);
            b.Property(x => x.NotasAdmin).HasMaxLength(1000);
            b.Property(x => x.AceptacionHash).HasMaxLength(255);
            b.Property(x => x.AceptacionIp).HasMaxLength(50);
            b.Property(x => x.AceptacionMetodo).HasMaxLength(50);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.AcuerdoPadre).WithMany().HasForeignKey(x => x.AcuerdoPadreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AcuerdoCuota>(b =>
        {
            b.ToTable("acuerdo_cuotas");
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.Capital).HasPrecision(18, 2);
            b.Property(x => x.Intereses).HasPrecision(18, 2);
            b.HasOne(x => x.Acuerdo).WithMany(a => a.Cuotas).HasForeignKey(x => x.AcuerdoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.AcuerdoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PazSalvoEmitido>(b =>
        {
            b.ToTable("paz_salvos_emitidos");
            b.Property(x => x.Condiciones).HasMaxLength(2000);
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.CodigoVerificacion).IsRequired().HasMaxLength(100);
            b.Property(x => x.MotivoAnulacion).HasMaxLength(500);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CodigoVerificacion).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Condonacion>(b =>
        {
            b.ToTable("condonaciones");
            b.Property(x => x.MontoCondonado).HasPrecision(18, 2);
            b.Property(x => x.Motivo).IsRequired().HasMaxLength(1000);
            b.Property(x => x.DocumentoSoporteUrl).HasMaxLength(500);
            b.Property(x => x.SaldoAntes).HasColumnType("text");
            b.Property(x => x.SaldoDespues).HasColumnType("text");
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraHistorial>(b =>
        {
            b.ToTable("cartera_historial");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(500);
            b.Property(x => x.DatosAdicionales).HasColumnType("text");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.9 PQRSD y Convivencia --------------------

        modelBuilder.Entity<PqrsdCategoria>(b =>
        {
            b.ToTable("pqrsd_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdConfiguracionPlazo>(b =>
        {
            b.ToTable("pqrsd_configuracion_plazos");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Tipo }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdExpediente>(b =>
        {
            b.ToTable("pqrsd_expedientes");
            b.Property(x => x.NumeroRadicado).IsRequired().HasMaxLength(30);
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(2000);
            b.Property(x => x.RespuestaAdmin).HasMaxLength(4000);
            b.Property(x => x.InconformidadTexto).HasMaxLength(2000);
            b.Property(x => x.RespuestaDefinitiva).HasMaxLength(4000);
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RadicadorPersona).WithMany().HasForeignKey(x => x.RadicadorPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.NumeroRadicado }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Tipo });
            b.HasIndex(x => x.RadicadorPersonaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdAdjunto>(b =>
        {
            b.ToTable("pqrsd_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Expediente).WithMany(e => e.Adjuntos).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ExpedienteId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdHistorialEstado>(b =>
        {
            b.ToTable("pqrsd_historial_estados");
            b.Property(x => x.Nota).HasMaxLength(500);
            b.HasOne(x => x.Expediente).WithMany(e => e.Historial).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ExpedienteId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdComiteSesion>(b =>
        {
            b.ToTable("pqrsd_comite_sesiones");
            b.Property(x => x.EnlaceReunion).HasMaxLength(500);
            b.Property(x => x.BorradorActa).HasColumnType("text");
            b.Property(x => x.ActaFinal).HasColumnType("text");
            b.HasOne(x => x.Expediente).WithMany().HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ExpedienteId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdComiteMiembroSesion>(b =>
        {
            b.ToTable("pqrsd_comite_miembros");
            b.HasOne(x => x.Sesion).WithMany(s => s.Miembros).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.8 Asambleas y Organos de Gobierno --------------------

        modelBuilder.Entity<AsambleaConfig>(b =>
        {
            b.ToTable("asamblea_config");
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Sesion>(b =>
        {
            b.ToTable("sesiones");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(255);
            b.Property(x => x.LugarFisico).HasMaxLength(500);
            b.Property(x => x.EnlaceVideo).HasMaxLength(500);
            b.Property(x => x.QuorumRequeridoPct).HasPrecision(5, 2);
            b.HasOne(x => x.SesionPadre).WithMany().HasForeignKey(x => x.SesionPadreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaSesion });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionPunto>(b =>
        {
            b.ToTable("sesion_puntos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(255);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.MayoriaPct).HasPrecision(5, 2);
            b.Property(x => x.OpcionesVoto).HasColumnType("text");
            b.Property(x => x.NarrativaSecretario).HasColumnType("text");
            b.HasOne(x => x.Sesion).WithMany(s => s.Puntos).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionDocumento>(b =>
        {
            b.ToTable("sesion_documentos");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.Property(x => x.TipoArchivo).HasMaxLength(50);
            b.HasOne(x => x.Sesion).WithMany(s => s.Documentos).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Punto).WithMany().HasForeignKey(x => x.PuntoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionParticipante>(b =>
        {
            b.ToTable("sesion_participantes");
            b.Property(x => x.Coeficiente).HasPrecision(10, 6);
            b.HasOne(x => x.Sesion).WithMany(s => s.Participantes).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.UnidadPrivadaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionPoder>(b =>
        {
            b.ToTable("sesion_poderes");
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.HashPoder).HasMaxLength(255);
            b.Property(x => x.FirmanteIp).HasMaxLength(50);
            b.Property(x => x.NotaRechazo).HasMaxLength(500);
            b.HasOne(x => x.Sesion).WithMany(s => s.Poderes).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ApoderadoPersona).WithMany().HasForeignKey(x => x.ApoderadoPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.OtorganteUnidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionQuorumLog>(b =>
        {
            b.ToTable("sesion_quorum_log");
            b.Property(x => x.Coeficiente).HasPrecision(10, 6);
            b.Property(x => x.QuorumAcumuladoPct).HasPrecision(5, 2);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Votacion>(b =>
        {
            b.ToTable("votaciones");
            b.Property(x => x.QuorumAlAbrirPct).HasPrecision(5, 2);
            b.Property(x => x.CoeficienteTotalSala).HasPrecision(10, 6);
            b.Property(x => x.ResultadoOpcion).HasMaxLength(100);
            b.Property(x => x.ResultadoPct).HasPrecision(5, 2);
            b.HasOne(x => x.Sesion).WithMany().HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Punto).WithMany().HasForeignKey(x => x.PuntoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PuntoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Voto>(b =>
        {
            b.ToTable("votos");
            b.Property(x => x.CoeficienteAportado).HasPrecision(10, 6);
            b.Property(x => x.Opcion).IsRequired().HasMaxLength(100);
            b.HasOne(x => x.Votacion).WithMany(v => v.Votos).HasForeignKey(x => x.VotacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.VotacionId, x.UnidadPrivadaId }).IsUnique();  // RN-09: un voto por unidad
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Acta>(b =>
        {
            b.ToTable("actas");
            b.Property(x => x.ContenidoGenerado).HasColumnType("text");
            b.Property(x => x.NarrativaSecretario).HasColumnType("text");
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.HashDocumento).HasMaxLength(255);
            b.Property(x => x.FirmanteIp).HasMaxLength(50);
            b.HasOne(x => x.Sesion).WithOne(s => s.Acta).HasForeignKey<Acta>(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EleccionConsejo>(b =>
        {
            b.ToTable("elecciones_consejo");
            b.Property(x => x.Estado).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EleccionCandidato>(b =>
        {
            b.ToTable("eleccion_candidatos");
            b.Property(x => x.Cargo).HasMaxLength(100);
            b.Property(x => x.VotosCoeficiente).HasPrecision(10, 6);
            b.HasOne(x => x.Eleccion).WithMany(e => e.Candidatos).HasForeignKey(x => x.EleccionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.EleccionId, x.PersonaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.11 Mantenimiento y Activos --------------------

        modelBuilder.Entity<MantenimientoPlan>(b =>
        {
            b.ToTable("mantenimiento_planes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.Disparo).HasConversion<int>();
            b.HasOne(x => x.ProveedorPreferido).WithMany().HasForeignKey(x => x.ProveedorPreferidoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId });
            b.HasIndex(x => new { x.TenantId, x.ProximaEjecucion });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoIntervencion>(b =>
        {
            b.ToTable("mantenimiento_intervenciones");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(20);
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.MotivoCancelacion).HasColumnType("text");
            b.Property(x => x.EstadoActivoNuevo).HasMaxLength(50);
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.Prioridad).HasConversion<int>();
            b.HasOne(x => x.Plan).WithMany(p => p.Intervenciones).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ResponsableInterno).WithMany().HasForeignKey(x => x.ResponsableInternoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Tarea).WithMany().HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();  // RN-15
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaProgramada });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoBitacora>(b =>
        {
            b.ToTable("mantenimiento_bitacora");
            b.Property(x => x.Contenido).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoAutor).HasConversion<int>();
            b.HasOne(x => x.Intervencion).WithMany(i => i.Bitacora).HasForeignKey(x => x.IntervencionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.IntervencionId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoAdjunto>(b =>
        {
            b.ToTable("mantenimiento_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(500);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Bitacora).WithMany(bi => bi.Adjuntos).HasForeignKey(x => x.BitacoraId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.BitacoraId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoHistorialEstado>(b =>
        {
            b.ToTable("mantenimiento_historial_estado");
            b.Property(x => x.EstadoAnterior).IsRequired().HasMaxLength(50);
            b.Property(x => x.EstadoNuevo).IsRequired().HasMaxLength(50);
            b.Property(x => x.Motivo).HasColumnType("text");
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.HasOne(x => x.Intervencion).WithMany().HasForeignKey(x => x.IntervencionId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.14 Comunicaciones --------------------

        modelBuilder.Entity<ComunicadoPlantilla>(b =>
        {
            b.ToTable("comunicado_plantillas");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.AsuntoModelo).IsRequired().HasMaxLength(150);
            b.Property(x => x.CuerpoModelo).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoComunicado).HasConversion<int>();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Query filter: globales (TenantId null) son visibles para todos; tenant solo ve los suyos.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Comunicado>(b =>
        {
            b.ToTable("comunicados");
            b.Property(x => x.Asunto).IsRequired().HasMaxLength(150);
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
            b.Property(x => x.CuerpoTextoPlano).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoComunicado).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Plantilla).WithMany().HasForeignKey(x => x.PlantillaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaProgramada });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoSegmento>(b =>
        {
            b.ToTable("comunicado_segmentos");
            b.Property(x => x.TipoSegmento).HasConversion<int>();
            b.Property(x => x.ValorJson).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Comunicado).WithMany(c => c.Segmentos).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ComunicadoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoAdjunto>(b =>
        {
            b.ToTable("comunicado_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Comunicado).WithMany(c => c.Adjuntos).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ComunicadoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoDestinatario>(b =>
        {
            b.ToTable("comunicado_destinatarios");
            b.Property(x => x.EstadoEntrega).HasConversion<int>();
            b.HasOne(x => x.Comunicado).WithMany(c => c.Destinatarios).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.Token).IsUnique();  // RN-08
            b.HasIndex(x => new { x.ComunicadoId, x.PersonaId }).IsUnique();  // RN-03 deduplicacion
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoAcuse>(b =>
        {
            b.ToTable("comunicado_acuses");
            b.Property(x => x.Dispositivo).HasConversion<int>();
            b.HasOne(x => x.Comunicado).WithMany().HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Destinatario).WithMany(d => d.Acuses).HasForeignKey(x => x.DestinatarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ComunicadoId, x.PersonaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.15 Documentos - vista Expedientes (TRD) --------------------

        modelBuilder.Entity<SerieDocumental>(b =>
        {
            b.ToTable("series_documentales");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieDocumental>(b =>
        {
            b.ToTable("subseries_documentales");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Serie).WithMany(s => s.Subseries).HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SerieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieTipologia>(b =>
        {
            b.ToTable("subserie_tipologias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Subserie).WithMany(s => s.Tipologias).HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SubserieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieCampo>(b =>
        {
            b.ToTable("subserie_campos");
            b.Property(x => x.Clave).IsRequired().HasMaxLength(80);
            b.Property(x => x.Label).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Subserie).WithMany(s => s.Campos).HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SubserieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Expediente>(b =>
        {
            b.ToTable("expedientes");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(40);
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Serie).IsRequired().HasMaxLength(150);
            b.Property(x => x.Subserie).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.Codigo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ExpedienteTipologia>(b =>
        {
            b.ToTable("expediente_tipologias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.ArchivoUrl).HasMaxLength(1024);
            b.Property(x => x.ArchivoNombre).HasMaxLength(255);
            b.Property(x => x.ArchivoMime).HasMaxLength(150);
            b.Property(x => x.MetaJson).HasColumnType("text");
            b.HasOne(x => x.Expediente).WithMany(e => e.Tipologias).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ExpedienteId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ExpedienteCampo>(b =>
        {
            b.ToTable("expediente_campos");
            b.Property(x => x.Clave).IsRequired().HasMaxLength(80);
            b.Property(x => x.Label).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Expediente).WithMany(e => e.Campos).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ExpedienteId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.15 Documentos y Archivo Digital --------------------

        modelBuilder.Entity<DocumentoCategoria>(b =>
        {
            b.ToTable("documento_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.Icono).HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Mezcla base (TenantId null) + tenant - mismo patron que ComunicadoPlantilla.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoCarpeta>(b =>
        {
            b.ToTable("documento_carpetas");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Padre).WithMany().HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CategoriaId });
            b.HasIndex(x => new { x.TenantId, x.PadreId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Documento>(b =>
        {
            b.ToTable("documentos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.NombreArchivoOriginal).IsRequired().HasMaxLength(255);
            b.Property(x => x.Visibilidad).IsRequired().HasMaxLength(20);
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Carpeta).WithMany().HasForeignKey(x => x.CarpetaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VersionActual).WithMany().HasForeignKey(x => x.VersionActualId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CategoriaId });
            b.HasIndex(x => new { x.TenantId, x.CarpetaId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Origen, x.OrigenEntidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoVersion>(b =>
        {
            b.ToTable("documento_versiones");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.Property(x => x.HashSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.NotasCambio).HasMaxLength(1000);
            b.HasOne(x => x.Documento).WithMany(d => d.Versiones).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.Numero }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoEtiquetaCatalogo>(b =>
        {
            b.ToTable("documento_etiquetas_catalogo");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoEtiqueta>(b =>
        {
            b.ToTable("documento_etiqueta_asignaciones");
            b.HasOne(x => x.Documento).WithMany(d => d.Etiquetas).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.EtiquetaCatalogo).WithMany().HasForeignKey(x => x.EtiquetaCatalogoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.EtiquetaCatalogoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoDestacadoPersonal>(b =>
        {
            b.ToTable("documento_destacados_personal");
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.UsuarioId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoAuditoria>(b =>
        {
            b.ToTable("documento_auditoria");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.DetalleJson).HasColumnType("text");
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoConsumo>(b =>
        {
            b.ToTable("documento_consumo");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.Dispositivo).HasConversion<int>();
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.16 Reportes e Indicadores --------------------

        modelBuilder.Entity<ReporteCategoria>(b =>
        {
            b.ToTable("reporte_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Icono).HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.Property(x => x.ModuloOrigen).IsRequired().HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Mezcla base (TenantId null) + tenant.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteCatalogo>(b =>
        {
            b.ToTable("reporte_catalogo");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.ModuloOrigen).IsRequired().HasMaxLength(20);
            b.Property(x => x.Clave).IsRequired().HasMaxLength(120);
            b.Property(x => x.AudienciasJson).IsRequired().HasColumnType("text");
            b.Property(x => x.FiltrosConfigJson).HasColumnType("text");
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Clave });
            b.HasIndex(x => x.CategoriaId);
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteGenerado>(b =>
        {
            b.ToTable("reporte_generados");
            b.Property(x => x.NombreReporte).IsRequired().HasMaxLength(200);
            b.Property(x => x.Categoria).IsRequired().HasMaxLength(100);
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.PromptIa).HasColumnType("text");
            b.Property(x => x.FiltrosAplicadosJson).HasColumnType("text");
            b.Property(x => x.ResultadoJson).HasColumnType("text");
            b.Property(x => x.ErrorMensaje).HasMaxLength(2000);
            b.Property(x => x.UrlPdf).HasMaxLength(1000);
            b.Property(x => x.UrlExcel).HasMaxLength(1000);
            b.HasOne(x => x.ReporteCatalogo).WithMany().HasForeignKey(x => x.ReporteCatalogoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.CompartidoConsejo });
            b.HasIndex(x => new { x.TenantId, x.PeriodoInicio, x.PeriodoFin });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteProgramacion>(b =>
        {
            b.ToTable("reporte_programaciones");
            b.Property(x => x.Nombre).HasMaxLength(200);
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.PeriodoQueCubre).HasConversion<int>();
            b.Property(x => x.Formato).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.CanalesJson).IsRequired().HasColumnType("text");
            b.Property(x => x.FiltrosAplicadosJson).HasColumnType("text");
            b.HasOne(x => x.ReporteCatalogo).WithMany().HasForeignKey(x => x.ReporteCatalogoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => x.ProximoEnvio);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteProgramacionDestinatario>(b =>
        {
            b.ToTable("reporte_programacion_destinatarios");
            b.Property(x => x.EmailExterno).HasMaxLength(200);
            b.Property(x => x.WhatsappExterno).HasMaxLength(30);
            b.HasOne(x => x.Programacion).WithMany(p => p.Destinatarios)
                .HasForeignKey(x => x.ProgramacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ProgramacionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteSemaforoConfig>(b =>
        {
            b.ToTable("reporte_semaforo_config");
            b.Property(x => x.IndicadorKey).IsRequired().HasMaxLength(100);
            b.Property(x => x.UmbralAmarillo).HasColumnType("numeric(18,2)");
            b.Property(x => x.UmbralRojo).HasColumnType("numeric(18,2)");
            b.HasIndex(x => new { x.TenantId, x.IndicadorKey }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.12 Porteria y Control de Acceso --------------------

        modelBuilder.Entity<TurnoPorteria>(b =>
        {
            b.ToTable("turnos_porteria");
            b.Property(x => x.PuntoAcceso).IsRequired().HasMaxLength(100);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.NotaCierre).HasColumnType("text");
            b.HasOne(x => x.GuardaPersona).WithMany().HasForeignKey(x => x.GuardaPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.GuardaPersonaId, x.PuntoAcceso, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<VisitanteFrecuente>(b =>
        {
            b.ToTable("visitantes_frecuentes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Documento).HasMaxLength(30);
            b.Property(x => x.TipoDocumento).HasConversion<int?>();
            b.Property(x => x.FotoUrl).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Documento });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AutorizacionPrevia>(b =>
        {
            b.ToTable("autorizaciones_previa");
            b.Property(x => x.NombreVisitante).IsRequired().HasMaxLength(200);
            b.Property(x => x.DocumentoVisitante).HasMaxLength(30);
            b.Property(x => x.NotaPortero).HasColumnType("text");
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.TipoVisitante).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreadoPorPersona).WithMany().HasForeignKey(x => x.CreadoPorPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CodigoIngreso>(b =>
        {
            b.ToTable("codigos_ingreso");
            b.Property(x => x.CodigoNumerico).IsRequired().HasMaxLength(8);
            b.Property(x => x.QrPayload).IsRequired().HasColumnType("text");
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Autorizacion).WithMany(a => a.Codigos).HasForeignKey(x => x.AutorizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CodigoNumerico }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RegistroVisita>(b =>
        {
            b.ToTable("registros_visita");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Documento).HasMaxLength(30);
            b.Property(x => x.Observacion).HasColumnType("text");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.TipoVisitante).HasConversion<int>();
            b.Property(x => x.TipoDocumento).HasConversion<int?>();
            b.Property(x => x.DestinoTipo).HasConversion<int?>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VisitanteFrecuente).WithMany().HasForeignKey(x => x.VisitanteFrecuenteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Autorizacion).WithMany().HasForeignKey(x => x.AutorizacionId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CodigoIngreso).WithMany().HasForeignKey(x => x.CodigoIngresoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<VehiculoAutorizado>(b =>
        {
            b.ToTable("vehiculos_autorizados");
            b.Property(x => x.Placa).IsRequired().HasMaxLength(10);
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Marca).HasMaxLength(100);
            b.Property(x => x.Modelo).HasMaxLength(100);
            b.Property(x => x.Color).HasMaxLength(50);
            b.Property(x => x.ParqueaderoAsignado).HasMaxLength(20);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Placa });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RegistroVehiculo>(b =>
        {
            b.ToTable("registros_vehiculo");
            b.Property(x => x.Placa).IsRequired().HasMaxLength(10);
            b.Property(x => x.Conductor).HasMaxLength(200);
            b.Property(x => x.Observacion).HasColumnType("text");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.OrigenRegistro).HasConversion<int>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VehiculoAutorizado).WithMany().HasForeignKey(x => x.VehiculoAutorizadoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Correspondencia>(b =>
        {
            b.ToTable("correspondencias");
            b.Property(x => x.Remitente).HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.EntregadoA).HasMaxLength(200);
            b.Property(x => x.MotivoDevolucion).HasColumnType("text");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<NovedadTurno>(b =>
        {
            b.ToTable("novedades_turno");
            b.Property(x => x.Descripcion).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tarea).WithMany().HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PorteriaConfiguracion>(b =>
        {
            b.ToTable("porteria_configuracion");
            b.Property(x => x.CanalNotificacionPaquetes).HasConversion<int>();
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.13 Reservas de Zonas Comunes --------------------

        modelBuilder.Entity<ZonaConfigReserva>(b =>
        {
            b.ToTable("zona_config_reserva");
            b.Property(x => x.ValorTarifa).HasColumnType("numeric(18,2)");
            b.Property(x => x.ValorPenalidadCancelacion).HasColumnType("numeric(18,2)");
            b.Property(x => x.ModalidadCobro).HasConversion<int>();
            b.Property(x => x.PoliticaReembolso).HasConversion<int>();
            b.Property(x => x.CancelacionTardia).HasConversion<int>();
            b.Property(x => x.ReglamentoTexto).HasColumnType("text");
            b.Property(x => x.ReglamentoArchivoUrl).HasMaxLength(500);
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaFranja>(b =>
        {
            b.ToTable("zona_franjas");
            b.Property(x => x.DiaSemana).HasConversion<int>();
            b.HasOne(x => x.ZonaConfigReserva).WithMany(c => c.Franjas).HasForeignKey(x => x.ZonaConfigReservaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ZonaConfigReservaId, x.DiaSemana });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Reserva>(b =>
        {
            b.ToTable("reservas");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(30);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.MotivoCancelacion).HasColumnType("text");
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReservaRecurrente).WithMany().HasForeignKey(x => x.ReservaRecurrenteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReservaPago).WithMany().HasForeignKey(x => x.ReservaPagoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId, x.Fecha });
            b.HasIndex(x => new { x.TenantId, x.PersonaId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Fecha, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReservaRecurrente>(b =>
        {
            b.ToTable("reservas_recurrentes");
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReservaPago>(b =>
        {
            b.ToTable("reserva_pagos");
            b.Property(x => x.Monto).HasColumnType("numeric(18,2)");
            b.Property(x => x.EstadoPago).HasConversion<int>();
            b.Property(x => x.WompiTransactionId).HasMaxLength(100);
            b.Property(x => x.WompiReference).HasMaxLength(100);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaBloqueo>(b =>
        {
            b.ToTable("zona_bloqueos");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Etiqueta).HasConversion<int>();
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.MotivoPersonalizado).HasColumnType("text");
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId, x.FechaInicio, x.FechaFin });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 1.2 Calendario Multi-Copropiedad (global) --------------------

        modelBuilder.Entity<CalendarioEvento>(b =>
        {
            b.ToTable("calendario_eventos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(50);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.FechaInicio });
            b.HasIndex(x => x.TenantId);
            // Calendario es global - NO HasQueryFilter por tenant. Se filtra por OrganizacionId en service.
        });

        modelBuilder.Entity<CalendarioConfigUsuario>(b =>
        {
            b.ToTable("calendario_config_usuarios");
            b.Property(x => x.VistaDefault).HasConversion<int>();
            b.Property(x => x.UltimaVista).HasConversion<int>();
            b.Property(x => x.FiltroCopropiedadesJson).HasColumnType("text");
            b.Property(x => x.FiltroTiposJson).HasColumnType("text");
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.UsuarioId, x.OrganizacionId }).IsUnique();
            b.HasIndex(x => x.IcalToken).IsUnique();
        });

        // -------------------- Modulo 1.4 Reportes Consolidados (global) --------------------

        modelBuilder.Entity<OrgReporte>(b =>
        {
            b.ToTable("org_reportes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Categoria).HasConversion<int>();
            b.Property(x => x.ConfiguracionJson).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.Categoria });
        });

        modelBuilder.Entity<OrgReporteGeneracion>(b =>
        {
            b.ToTable("org_reporte_generaciones");
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.ResultadoJson).HasColumnType("text");
            b.Property(x => x.ErrorDetalle).HasMaxLength(2000);
            b.Property(x => x.UrlPdf).HasMaxLength(1000);
            b.Property(x => x.UrlExcel).HasMaxLength(1000);
            b.HasOne(x => x.Reporte).WithMany().HasForeignKey(x => x.ReporteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.ReporteId);
            b.HasIndex(x => new { x.OrganizacionId, x.CreatedAt });
        });

        // -------------------- Modulo 1.5 Transferencia de Custodia (global) --------------------

        modelBuilder.Entity<TransferenciaCustodia>(b =>
        {
            b.ToTable("transferencias_custodia");
            b.Property(x => x.Escenario).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.SnapshotEstadoJson).HasColumnType("text");
            b.Property(x => x.AjusteFacturacionJson).HasColumnType("text");
            b.HasOne(x => x.Copropiedad).WithMany().HasForeignKey(x => x.CopropiedadId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ActaEntregaDocumento).WithMany().HasForeignKey(x => x.ActaEntregaDocumentoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.CopropiedadId);
            b.HasIndex(x => x.OrganizacionSalienteId);
            b.HasIndex(x => x.OrganizacionEntranteId);
            b.HasIndex(x => x.Estado);
            // RN-16: solo una transferencia activa por copropiedad. Unique parcial en migracion SQL.
        });

        modelBuilder.Entity<TransferenciaDocumento>(b =>
        {
            b.ToTable("transferencia_documentos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(300);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.Property(x => x.HashSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.ResultadoValidacionIa).HasConversion<int>();
            b.Property(x => x.DetalleValidacionIaJson).HasColumnType("text");
            b.HasOne(x => x.Transferencia).WithMany(t => t.Documentos)
                .HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TransferenciaId);
        });

        modelBuilder.Entity<TransferenciaEvento>(b =>
        {
            b.ToTable("transferencia_eventos");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.Canal).HasMaxLength(30);
            b.Property(x => x.DetalleJson).HasColumnType("text");
            b.HasOne(x => x.Transferencia).WithMany(t => t.Eventos)
                .HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TransferenciaId);
            b.HasIndex(x => new { x.TransferenciaId, x.CreatedAt });
        });

        // Festivos colombianos (global, sin tenant) - cache memoria proceso via CalendarioHabilService
        modelBuilder.Entity<FestivoColombiano>(b =>
        {
            b.ToTable("festivos_colombianos");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(200);
            b.HasIndex(x => x.Fecha).IsUnique();
        });

        // Modulo 0.3 Monitoria y Auditoria Global
        modelBuilder.Entity<SistemaLog>(b =>
        {
            b.ToTable("sistema_logs");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.Severidad).HasConversion<int>();
            b.Property(x => x.Mensaje).IsRequired().HasMaxLength(2000);
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(20);
            b.Property(x => x.DetalleJson).HasColumnType("text");
            b.Property(x => x.Ip).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(500);
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => new { x.Severidad, x.CreatedAt });
            b.HasIndex(x => new { x.TipoEvento, x.CreatedAt });
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<SistemaIncidente>(b =>
        {
            b.ToTable("sistema_incidentes");
            b.Property(x => x.Severidad).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(300);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.ServicioAfectado).HasMaxLength(100);
            b.Property(x => x.CausaRaiz).HasColumnType("text");
            b.Property(x => x.SolucionAplicada).HasColumnType("text");
            b.HasIndex(x => new { x.Estado, x.Severidad });
            b.HasIndex(x => x.DetectadoAt);
            b.HasIndex(x => x.AsignadoSuperAdminId);
        });

        modelBuilder.Entity<MetricaUsoDiaria>(b =>
        {
            b.ToTable("metricas_uso_diarias");
            b.HasIndex(x => x.Fecha).IsUnique();
        });

        modelBuilder.Entity<JobEjecucion>(b =>
        {
            b.ToTable("job_ejecuciones");
            b.Property(x => x.JobName).IsRequired().HasMaxLength(100);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.ResultadoJson).HasColumnType("text");
            b.Property(x => x.Error).HasMaxLength(4000);
            b.Property(x => x.EjecutadoPorHost).HasMaxLength(100);
            b.HasIndex(x => new { x.JobName, x.IniciadoAt });
            b.HasIndex(x => new { x.JobName, x.Estado });
        });

        // T.2 Motor de Notificaciones (servicio comun cross-modulo)
        modelBuilder.Entity<Notificacion>(b =>
        {
            b.ToTable("notificaciones");
            b.Property(x => x.Canal).HasConversion<int>();
            b.Property(x => x.Prioridad).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.Destino).IsRequired().HasMaxLength(300);
            b.Property(x => x.Asunto).HasMaxLength(300);
            b.Property(x => x.Cuerpo).IsRequired().HasColumnType("text");
            b.Property(x => x.CuerpoHtml).HasColumnType("text");
            b.Property(x => x.MetadataJson).HasColumnType("text");
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(20);
            b.Property(x => x.UltimoError).HasMaxLength(2000);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UsuarioDestinatarioId);
            b.HasIndex(x => new { x.Estado, x.FechaProximoIntento });
            b.HasIndex(x => new { x.ModuloOrigenCodigo, x.EntidadOrigenId });
        });

        // UsuarioTenant (TenantEntity)
        modelBuilder.Entity<UsuarioTenant>(b =>
        {
            b.Property(x => x.Rol).IsRequired().HasMaxLength(100);
            b.Property(x => x.MotivoRevocacion).HasMaxLength(500);
            b.HasOne(x => x.Persona)
                .WithMany(x => x.VinculosATenants)
                .HasForeignKey(x => x.PersonaId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RolNavigation)
                .WithMany()
                .HasForeignKey(x => x.RolId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.PersonaId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.RolId);

            // Red de seguridad de aplicacion. RLS de Postgres es la red final.
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Etiquetas de usuario (2.5): definicion con color + asignacion N:N. Patron TareaEtiqueta.
        modelBuilder.Entity<EtiquetaUsuario>(b =>
        {
            b.ToTable("etiquetas_usuario");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UsuarioTenantEtiqueta>(b =>
        {
            b.ToTable("usuario_tenant_etiquetas");
            b.HasOne(x => x.UsuarioTenant).WithMany().HasForeignKey(x => x.UsuarioTenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Etiqueta).WithMany().HasForeignKey(x => x.EtiquetaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.UsuarioTenantId, x.EtiquetaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // SuperAdminUsuario
        modelBuilder.Entity<SuperAdminUsuario>(b =>
        {
            b.Property(x => x.Email).IsRequired().HasMaxLength(200).HasColumnType("citext");
            b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(512);
            b.HasIndex(x => x.Email).IsUnique();
        });

        // SuperAdminLog (append-only)
        modelBuilder.Entity<SuperAdminLog>(b =>
        {
            b.Property(x => x.ActorEmail).IsRequired().HasMaxLength(200);
            b.Property(x => x.Accion).IsRequired().HasMaxLength(100);
            b.Property(x => x.EntidadAfectada).HasMaxLength(200);
            b.Property(x => x.Justificacion).HasMaxLength(1000);
            b.Property(x => x.Ip).HasMaxLength(45);  // IPv6 max
            b.Property(x => x.UserAgent).HasMaxLength(500);
            b.HasIndex(x => x.ActorId);
            b.HasIndex(x => x.CreatedAt);
            // Inmutabilidad: trigger PostgreSQL agregado en migracion posterior.
        });

        // -------------------- Integraciones de plataforma (Super Admin) --------------------
        modelBuilder.Entity<EmailConfig>(b =>
        {
            b.Property(x => x.SmtpHost).HasMaxLength(255);
            b.Property(x => x.SmtpUser).HasMaxLength(255);
            b.Property(x => x.SmtpPasswordEncrypted).HasMaxLength(1024);
            b.Property(x => x.FromEmail).HasMaxLength(255);
            b.Property(x => x.FromName).HasMaxLength(255);
        });

        modelBuilder.Entity<GoogleAuthConfig>(b =>
        {
            b.Property(x => x.ClientId).HasMaxLength(255);
            b.Property(x => x.ClientSecretEncrypted).HasMaxLength(1024);
        });

        modelBuilder.Entity<PlatformBranding>(b =>
        {
            b.Property(x => x.PlatformName).IsRequired().HasMaxLength(120);
            b.Property(x => x.Tagline).HasMaxLength(255);
            b.Property(x => x.LoginLogoUrl).HasMaxLength(500);
            b.Property(x => x.LoginHeadline).HasMaxLength(255);
            b.Property(x => x.LoginSubtext).HasMaxLength(1000);
        });

        modelBuilder.Entity<AiProviderConfig>(b =>
        {
            b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.ApiKeyEncrypted).HasMaxLength(1024);
            b.Property(x => x.Model).HasMaxLength(120);
            b.Property(x => x.BaseUrl).HasMaxLength(255);
            b.HasIndex(x => x.Provider).IsUnique();
        });

        // Menu de navegacion configurable (global). Un override por nodo (seccion o item).
        modelBuilder.Entity<MenuOverride>(b =>
        {
            b.Property(x => x.NodeKey).IsRequired().HasMaxLength(80);
            b.Property(x => x.Label).HasMaxLength(120);
            b.Property(x => x.ParentKey).HasMaxLength(80);
            b.Property(x => x.NodeType).HasMaxLength(20);
            b.Property(x => x.Icon).HasMaxLength(60);
            b.Property(x => x.Href).HasMaxLength(300);
            b.HasIndex(x => x.NodeKey).IsUnique();
        });

        modelBuilder.Entity<OcrProviderConfig>(b =>
        {
            b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Endpoint).HasMaxLength(255);
            b.Property(x => x.ApiKeyEncrypted).HasMaxLength(1024);
            b.Property(x => x.ModelId).HasMaxLength(60);
            b.HasIndex(x => x.Provider).IsUnique();
        });

        modelBuilder.Entity<WompiMasterConfig>(b =>
        {
            b.Property(x => x.Environment).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.PublicKey).HasMaxLength(255);
            b.Property(x => x.PrivateKeyEncrypted).HasMaxLength(1024);
            b.Property(x => x.EventsSecretEncrypted).HasMaxLength(1024);
            b.Property(x => x.IntegritySecretEncrypted).HasMaxLength(1024);
            b.Property(x => x.WebhookEndpoint).HasMaxLength(500);
            b.Property(x => x.Currency).HasMaxLength(10);
        });

        modelBuilder.Entity<WompiWebhookEvent>(b =>
        {
            b.Property(x => x.ProviderEventId).IsRequired().HasMaxLength(255);
            b.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.RawPayload).HasColumnType("jsonb");
            b.Property(x => x.TransactionId).HasMaxLength(100);
            b.Property(x => x.Reference).HasMaxLength(255);
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => x.ProviderEventId).IsUnique();
            b.HasIndex(x => x.Reference);
        });

        modelBuilder.Entity<EvolutionMasterConfig>(b =>
        {
            b.Property(x => x.BaseUrl).HasMaxLength(500);
            b.Property(x => x.ApiKeyEncrypted).HasMaxLength(1024);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.WebhookMode).HasMaxLength(20);
            b.Property(x => x.WebhookPublicUrl).HasMaxLength(500);
            b.Property(x => x.WebhookToken).HasMaxLength(1024);
        });

        // ApplicationUser - relacion opcional con Persona del Directorio
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.HasOne(x => x.Persona)
                .WithMany()
                .HasForeignKey(x => x.PersonaId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.PersonaId);
        });

        // -------------------- Infraestructura IA (Capa 2, tenant-scoped) --------------------

        modelBuilder.Entity<WhatsAppLine>(b =>
        {
            b.Property(x => x.InstanceName).IsRequired().HasMaxLength(150);
            b.Property(x => x.PhoneNumber).HasMaxLength(30);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => new { x.TenantId, x.InstanceName }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<NumeroEnListaNegra>(b =>
        {
            b.Property(x => x.Telefono).IsRequired().HasMaxLength(30);
            b.Property(x => x.Nota).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.Telefono }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CuentaBancaria>(b =>
        {
            b.Property(x => x.NumeroCuenta).IsRequired().HasMaxLength(50);
            b.Property(x => x.Banco).IsRequired().HasMaxLength(120);
            b.HasIndex(x => new { x.TenantId, x.NumeroCuenta });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<VentanaDisponibilidad>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.TipoEntidad, x.EntidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EquipoFoto>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<EquipoMejora>(b =>
        {
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(200);
            b.Property(x => x.Valor).HasColumnType("numeric(18,2)");
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<EquipoVinculo>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<EquipoContratoVinculo>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<EquipoCampoPersonalizado>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(120);
            b.Property(x => x.Valor).HasMaxLength(500);
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Conversation>(b =>
        {
            b.Property(x => x.ContactPhone).IsRequired().HasMaxLength(30);
            b.Property(x => x.ContactName).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.ContactPhone }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LastMessageAt });
            b.HasOne(x => x.WhatsAppLine).WithMany().HasForeignKey(x => x.WhatsAppLineId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.Property(x => x.ExternalId).HasMaxLength(255);
            b.Property(x => x.Body).HasColumnType("text");
            b.Property(x => x.MessageType).HasMaxLength(30);
            b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            b.Property(x => x.MediaType).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.SentByName).HasMaxLength(200);
            b.Property(x => x.MediaUrl).HasMaxLength(1024);
            b.Property(x => x.MediaMimeType).HasMaxLength(150);
            b.Property(x => x.Reaction).HasMaxLength(16);
            b.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique().HasFilter("external_id IS NOT NULL");
            b.HasIndex(x => new { x.ConversationId, x.SentAt });
            b.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgent>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Role).HasMaxLength(100);
            b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.SystemPrompt).HasColumnType("text");
            b.Property(x => x.PromptHistoryJson).HasColumnType("text");
            b.Property(x => x.ReactionEmojis).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.IsActive });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentLineBinding>(b =>
        {
            b.ToTable("ai_agent_line_bindings");
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.WhatsAppLine).WithMany().HasForeignKey(x => x.WhatsAppLineId).OnDelete(DeleteBehavior.Cascade);
            // Una linea solo puede tener un binding activo (is_connected = true) a la vez.
            b.HasIndex(x => new { x.TenantId, x.WhatsAppLineId, x.IsConnected }).IsUnique().HasFilter("is_connected");
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentPrompt>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Rule).HasMaxLength(1000);
            b.Property(x => x.Body).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasOne(x => x.Agent).WithMany(a => a.Prompts).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentResource>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.ResourceType).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Detail).HasColumnType("text");
            b.Property(x => x.FileUrl).HasMaxLength(1024);
            b.Property(x => x.FileName).HasMaxLength(255);
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasOne(x => x.Agent).WithMany(a => a.Resources).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentCacheField>(b =>
        {
            b.ToTable("ai_agent_cache_fields");
            b.Property(x => x.FieldKey).HasMaxLength(80).IsRequired();
            b.Property(x => x.Label).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(600);
            b.Property(x => x.IsUpdatable).HasDefaultValue(true);
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.AgentId, x.SortOrder });
            b.HasIndex(x => new { x.AgentId, x.FieldKey }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentCacheValue>(b =>
        {
            b.ToTable("ai_agent_cache_values");
            b.Property(x => x.FieldKey).HasMaxLength(80).IsRequired();
            b.Property(x => x.Value).HasMaxLength(2000);
            b.Property(x => x.Source).HasMaxLength(40);
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.AgentId, x.SessionId });
            b.HasIndex(x => new { x.AgentId, x.SessionId, x.FieldKey }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentMcpTool>(b =>
        {
            b.ToTable("ai_agent_mcp_tools");
            b.Property(x => x.ConnectionCode).IsRequired().HasMaxLength(50);
            b.Property(x => x.ToolName).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasIndex(x => new { x.AgentId, x.ConnectionCode, x.ToolName }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiUsageLog>(b =>
        {
            b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.EstimatedCostUsd).HasPrecision(12, 6);
            b.Property(x => x.Source).HasMaxLength(30);
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AiAgentRunLog>(b =>
        {
            b.ToTable("ai_agent_run_logs");
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Content).HasColumnType("text");
            b.Property(x => x.Response).HasColumnType("text");
            // Consulta principal de la bitacora: por conversacion, en orden cronologico.
            b.HasIndex(x => new { x.TenantId, x.ConversationId, x.OccurredAt });
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AutomationRule>(b =>
        {
            b.ToTable("automation_rules");
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.TimeWindowStart).HasMaxLength(5);
            b.Property(x => x.TimeWindowEnd).HasMaxLength(5);
            b.Property(x => x.MensajePlantilla).HasColumnType("text");
            b.Property(x => x.TareaTitulo).HasMaxLength(200);
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Plantillas globales de agente (Super Admin). SIN tenant_id ni RLS.
        modelBuilder.Entity<AiAgentTemplate>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.Role).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.SystemPrompt).HasColumnType("text");
            b.HasIndex(x => new { x.IsActive, x.IncludeInOnboarding });
        });

        modelBuilder.Entity<AiAgentTemplateMcpTool>(b =>
        {
            b.ToTable("ai_agent_template_mcp_tools");
            b.Property(x => x.ConnectionCode).IsRequired().HasMaxLength(50);
            b.Property(x => x.ToolName).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Template).WithMany(t => t.McpTools).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TemplateId, x.ConnectionCode, x.ToolName }).IsUnique();
        });

        // -------------------- Modulo 0.2 Billing --------------------

        modelBuilder.Entity<Plan>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.FeeBase).HasPrecision(12, 2);
            b.Property(x => x.FeeVariablePorUnidad).HasPrecision(12, 2);
            b.Property(x => x.DescuentoAnualPct).HasPrecision(5, 2);
            b.Property(x => x.ModulosIncluidos).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            b.HasIndex(x => x.Estado);
        });

        modelBuilder.Entity<Cupon>(b =>
        {
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
            b.Property(x => x.Valor).HasPrecision(12, 2);
            b.Property(x => x.PlanesAplicables).HasColumnType("jsonb");
            b.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<MetodoPago>(b =>
        {
            b.Property(x => x.TokenWompi).HasMaxLength(255);
            b.Property(x => x.UltimosDigitos).HasMaxLength(4);
            b.Property(x => x.Marca).HasMaxLength(50);
            b.Property(x => x.Banco).HasMaxLength(100);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Copropiedad).WithMany().HasForeignKey(x => x.CopropiedadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => x.CopropiedadId);
            b.ToTable(t => t.HasCheckConstraint("ck_metodo_pago_owner",
                "(organizacion_id IS NOT NULL AND copropiedad_id IS NULL) OR (organizacion_id IS NULL AND copropiedad_id IS NOT NULL)"));
        });

        modelBuilder.Entity<Suscripcion>(b =>
        {
            b.Property(x => x.CreditoAFavor).HasPrecision(12, 2);
            b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Copropiedad).WithMany().HasForeignKey(x => x.CopropiedadId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.MetodoPago).WithMany().HasForeignKey(x => x.MetodoPagoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Cupon).WithMany().HasForeignKey(x => x.CuponId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => x.CopropiedadId);
            b.HasIndex(x => x.Estado);
            b.HasIndex(x => x.FechaProximoCobro);
            b.ToTable(t => t.HasCheckConstraint("ck_suscripcion_owner",
                "(organizacion_id IS NOT NULL AND copropiedad_id IS NULL) OR (organizacion_id IS NULL AND copropiedad_id IS NOT NULL)"));
        });

        modelBuilder.Entity<Factura>(b =>
        {
            b.Property(x => x.NumeroFactura).HasMaxLength(100);
            b.Property(x => x.Cufe).HasMaxLength(255);
            b.Property(x => x.ReferenciaExterna).HasMaxLength(255);
            b.Property(x => x.WompiTransactionId).HasMaxLength(255);
            b.Property(x => x.Subtotal).HasPrecision(12, 2);
            b.Property(x => x.Descuento).HasPrecision(12, 2);
            b.Property(x => x.ImpuestoPct).HasPrecision(5, 2);
            b.Property(x => x.ImpuestoValor).HasPrecision(12, 2);
            b.Property(x => x.Total).HasPrecision(12, 2);
            b.HasOne(x => x.Suscripcion).WithMany().HasForeignKey(x => x.SuscripcionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.MetodoPago).WithMany().HasForeignKey(x => x.MetodoPagoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.SuscripcionId);
            b.HasIndex(x => x.Estado);
            b.HasIndex(x => x.FechaEmision);
            b.HasIndex(x => x.NumeroFactura).IsUnique().HasFilter("numero_factura IS NOT NULL");
        });

        modelBuilder.Entity<IntentoCobro>(b =>
        {
            b.Property(x => x.CodigoError).HasMaxLength(100);
            b.Property(x => x.DescripcionError).HasMaxLength(2000);
            b.Property(x => x.WompiResponse).HasColumnType("jsonb");
            b.HasOne(x => x.Factura).WithMany().HasForeignKey(x => x.FacturaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Suscripcion).WithMany().HasForeignKey(x => x.SuscripcionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.FacturaId, x.NumeroIntento });
            b.HasIndex(x => x.Resultado);
        });

        modelBuilder.Entity<SuscripcionHistorial>(b =>
        {
            b.Property(x => x.EstadoAnterior).HasMaxLength(50);
            b.Property(x => x.EstadoNuevo).HasMaxLength(50);
            b.Property(x => x.MontoProrrateo).HasPrecision(12, 2);
            b.Property(x => x.CreditoGenerado).HasPrecision(12, 2);
            b.Property(x => x.Notas).HasMaxLength(2000);
            b.HasOne(x => x.Suscripcion).WithMany().HasForeignKey(x => x.SuscripcionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.SuscripcionId);
            b.HasIndex(x => x.CreatedAt);
            // Inmutabilidad: trigger PostgreSQL agregado en la migracion (rechaza UPDATE y DELETE).
        });

        modelBuilder.Entity<BillingConfig>(b =>
        {
            b.Property(x => x.Moneda).IsRequired().HasMaxLength(3);
            b.Property(x => x.ImpuestoPct).HasPrecision(5, 2);
            b.Property(x => x.DiasEntreReintentos).HasColumnType("jsonb").HasDefaultValueSql("'[1,3,7]'::jsonb");
        });
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
