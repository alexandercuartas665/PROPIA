using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Application.Billing;
using Propia.Application.Common;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Infrastructure.Auth;
using Propia.Infrastructure.Billing;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;
using Propia.Infrastructure.SuperAdmin;

namespace Propia.Infrastructure;

/// <summary>
/// Extension para registrar todos los servicios de Infrastructure en DI.
/// Llamado desde Program.cs de Propia.Api y Propia.Workers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Propia")
            ?? throw new InvalidOperationException("Falta connection string 'Propia' en configuracion.");

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<Persistence.TenantConnectionInterceptor>();
        // Necesario para EquipoOrgService que lee claims del JWT en runtime.
        services.AddHttpContextAccessor();

        services.AddDbContext<PropiaDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PropiaDbContext).Assembly.FullName);
            });
            options.AddInterceptors(sp.GetRequiredService<Persistence.TenantConnectionInterceptor>());
        });

        // ASP.NET Core Identity - solo el core (sin cookies, JWT only)
        services.AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.Password.RequiredLength = 10;
                opts.Password.RequireDigit = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequireNonAlphanumeric = false;
                opts.User.RequireUniqueEmail = true;
                opts.SignIn.RequireConfirmedEmail = false;  // Para MVP - habilitar en Fase 2 con email service
                opts.Lockout.MaxFailedAccessAttempts = 5;
                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Super Admin (modulo 0.1) - usa tabla separada super_admin_usuarios
        services.AddScoped<IPasswordHasher<SuperAdminUsuario>, PasswordHasher<SuperAdminUsuario>>();
        services.AddSingleton<ITotpService, TotpService>();  // sin estado
        services.AddScoped<ISuperAdminAuthService, SuperAdminAuthService>();
        services.AddScoped<ISuperAdminService, SuperAdminService>();

        // Modulo 0.2 Billing y Suscripciones
        services.AddScoped<IBillingService, BillingService>();

        // ---- Integraciones de plataforma (Super Admin) - portadas de CUBOT.travels ----
        // Data Protection cifra secretos en reposo (SMTP, API keys, OAuth). En produccion
        // (Railway/Linux) las llaves DEBEN persistirse fuera del proceso para sobrevivir redeploys:
        // sin esto, cada redeploy/reinicio regenera llaves y los secretos cifrados dejan de
        // descifrarse (ej. el login Google mostraba "no habilitado"). Se persisten en la BD via
        // PersistKeysToDbContext (sobrevive redeploys y multi-instancia, sin necesidad de volumen).
        services.AddDataProtection()
            .SetApplicationName("Propia")
            .PersistKeysToDbContext<PropiaDbContext>();
        services.AddSingleton<Application.Common.ISecretProtector, Security.SecretProtector>();
        services.AddScoped<Application.Common.IEmailSender, Email.SmtpEmailSender>();
        services.AddScoped<Application.Integraciones.IEmailConfigService, Integraciones.EmailConfigService>();
        services.AddScoped<Application.Integraciones.IPlatformBrandingService, Integraciones.PlatformBrandingService>();
        services.AddScoped<Application.Integraciones.IAiServerConfigService, Integraciones.AiServerConfigService>();
        services.AddScoped<Application.Integraciones.IOcrServerConfigService, Integraciones.OcrServerConfigService>();
        services.AddScoped<Ocr.AzureDocumentExtractionService>();
        services.AddScoped<Ocr.AzureComputerVisionExtractionService>();
        services.AddScoped<Application.Ocr.IDocumentExtractionService, Ocr.OcrDispatcherService>();
        services.AddScoped<Application.Integraciones.IWompiConfigService, Integraciones.WompiConfigService>();
        services.AddScoped<Application.Integraciones.IWompiWebhookService, Integraciones.WompiWebhookService>();
        services.AddScoped<Application.Integraciones.IEvolutionMasterConfigService, Integraciones.EvolutionMasterConfigService>();
        services.AddScoped<Application.Integraciones.IGoogleAuthConfigService, Integraciones.GoogleAuthConfigService>();
        services.AddHttpClient<Application.Auth.IGoogleOAuthClient, Auth.GoogleOAuthClient>();
        services.AddScoped<Application.Auth.IGoogleSignInService, Auth.GoogleSignInService>();

        // ---- Infraestructura IA (Capa 2, tenant-scoped) - portado de CUBOT.travels ----
        // Clientes HTTP externos (Evolution WhatsApp + proveedores de IA). Tipados via HttpClientFactory.
        services.AddHttpClient<Application.InfraestructuraIa.IEvolutionApiClient, InfraestructuraIa.EvolutionApiClient>();
        services.AddHttpClient<Application.InfraestructuraIa.IWhatsAppCloudClient, InfraestructuraIa.WhatsAppCloudClient>();
        services.AddHttpClient<Application.InfraestructuraIa.IAiProviderClient, InfraestructuraIa.AiProviderClient>();
        services.AddScoped<Application.InfraestructuraIa.IWhatsAppLineService, InfraestructuraIa.WhatsAppLineService>();
        services.AddScoped<Application.InfraestructuraIa.IListaNegraService, InfraestructuraIa.ListaNegraService>();
        services.AddScoped<Application.InfraestructuraIa.IConversacionService, InfraestructuraIa.ConversacionService>();
        services.AddScoped<Application.InfraestructuraIa.IChatIngestService, InfraestructuraIa.ChatIngestService>();
        services.AddScoped<Application.InfraestructuraIa.IWhatsAppConnectorService, InfraestructuraIa.WhatsAppConnectorService>();
        services.AddScoped<Application.InfraestructuraIa.IAiAgentService, InfraestructuraIa.AiAgentService>();
        services.AddScoped<Application.InfraestructuraIa.IAiAgentLineBindingService, InfraestructuraIa.AiAgentLineBindingService>();
        services.AddScoped<Application.InfraestructuraIa.IAiAgentCacheService, InfraestructuraIa.AiAgentCacheService>();
        services.AddScoped<Application.InfraestructuraIa.IAiUsageService, InfraestructuraIa.AiUsageService>();
        services.AddScoped<Application.InfraestructuraIa.IAiInferenceService, InfraestructuraIa.AiInferenceService>();
        services.AddScoped<Application.InfraestructuraIa.IAiAgentTemplateService, InfraestructuraIa.AiAgentTemplateService>();

        // Gateway MCP: el agente como cliente MCP. HttpClient "Mcp" acepta el cert self-signed
        // SOLO para localhost (dev); contra cualquier host real exige certificado valido.
        services.AddHttpClient("Mcp").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None
                || request.RequestUri?.Host is "localhost" or "127.0.0.1"
        });
        services.AddScoped<Application.InfraestructuraIa.IMcpGateway, InfraestructuraIa.McpGateway>();

        // Modulo 2.3 Mi Copropiedad
        services.AddScoped<Application.MiCopropiedad.IMiCopropiedadService, MiCopropiedad.MiCopropiedadService>();
        services.AddScoped<Application.MiCopropiedad.IDistribucionImportService, MiCopropiedad.DistribucionImportService>();

        // Modulo 2.17 Servicios publicos
        services.AddScoped<Application.ServiciosPublicos.IServiciosPublicosService, ServiciosPublicos.ServiciosPublicosService>();

        // Servicios y contratos (Finanzas)
        services.AddScoped<Application.Servicios.IServiciosService, Servicios.ServiciosService>();

        // Programador de tareas (2.10)
        services.AddScoped<Application.Programaciones.IProgramacionTareasService, Programaciones.ProgramacionTareasService>();

        // Modulo 2.1 Onboarding y Activacion
        services.AddScoped<Application.Onboarding.IOnboardingService, Onboarding.OnboardingService>();

        // Modulo 2.4 Directorio
        services.AddScoped<Application.Directorio.IDirectorioService, Directorio.DirectorioService>();

        // Modulo 2.5 Usuarios, Roles y Accesos
        services.AddScoped<Application.UsuariosAccesos.IUsuariosService, UsuariosAccesos.UsuariosService>();
        services.AddScoped<Application.UsuariosAccesos.IRolesService, UsuariosAccesos.RolesService>();

        // Modulo 2.6 Presupuesto, Cuotas y Pagos
        services.AddScoped<Application.Presupuesto.IPresupuestoService, Presupuesto.PresupuestoService>();

        // Modulo 1.3 Gestion de Equipo
        services.AddScoped<Application.EquipoOrg.IEquipoOrgService, EquipoOrg.EquipoOrgService>();

        // Modulo 1.1 Panel y Dashboard Consolidado
        services.AddScoped<Application.PanelConsolidado.IPanelConsolidadoService, PanelConsolidado.PanelConsolidadoService>();

        // Modulo 2.2 Dashboard de la Copropiedad
        services.AddScoped<Application.DashboardCopropiedad.IDashboardCopropiedadService, DashboardCopropiedad.DashboardCopropiedadService>();

        // Modulo 2.10 Tareas y Proyectos
        services.AddScoped<Application.Tareas.ITareasService, Tareas.TareasService>();

        // Modulo 2.7 Cartera y Estado de Cuenta
        services.AddScoped<Application.Cartera.ICarteraService, Cartera.CarteraService>();
        services.AddScoped<Application.Cartera.IComprobantePdfService, Cartera.ComprobantePdfService>();

        // Modulo 2.9 PQRSD y Convivencia
        services.AddScoped<Application.Pqrsd.IPqrsdService, Pqrsd.PqrsdService>();

        // Modulo 2.8 Asambleas y Organos de Gobierno
        services.AddScoped<Application.Asambleas.IAsambleaService, Asambleas.AsambleaService>();

        // Modulo 2.11 Mantenimiento y Activos
        services.AddScoped<Application.Mantenimiento.IMantenimientoService, Mantenimiento.MantenimientoService>();

        // Modulo 2.14 Comunicaciones
        services.AddScoped<Application.Comunicaciones.IComunicacionesService, Comunicaciones.ComunicacionesService>();

        // Modulo 2.15 Documentos y Archivo Digital
        services.AddScoped<Application.Documentos.IDocumentosService, Documentos.DocumentosService>();
        services.AddScoped<Application.Documentos.IExpedientesService, Documentos.ExpedientesService>();

        // Modulo 2.16 Reportes e Indicadores (consumidor puro - depende de IndicadoresService cross-modulo)
        services.AddScoped<Application.Reportes.IIndicadoresService, Reportes.IndicadoresService>();
        services.AddScoped<Application.Reportes.IReportesService, Reportes.ReportesService>();

        // Modulo 2.12 Porteria y Control de Acceso
        services.AddScoped<Application.Porteria.IPorteriaService, Porteria.PorteriaService>();

        // Modulo 2.13 Reservas de Zonas Comunes
        services.AddScoped<Application.Reservas.IReservasService, Reservas.ReservasService>();

        // Modulo 1.2 Calendario Multi-Copropiedad (agregador cross-modulo)
        services.AddScoped<Application.Calendario.ICalendarioService, Calendario.CalendarioService>();

        // Modulo 1.4 Reportes Consolidados de la Organizacion
        services.AddScoped<Application.ReportesConsolidados.IReportesConsolidadosService,
            ReportesConsolidados.ReportesConsolidadosService>();

        // Modulo 1.5 Transferencia de Custodia
        services.AddScoped<Application.TransferenciaCustodia.ITransferenciaCustodiaService,
            TransferenciasCustodia.TransferenciaCustodiaService>();

        // T.2 Motor de Notificaciones (servicio comun consumido por 1.2, 1.5, 2.7, 2.9, 2.11,
        // 2.12, 2.13, 2.14, 2.16). Provider stub default - reemplaza los 9 simulados dispersos.
        services.AddScoped<Application.Notificaciones.INotificacionDispatcher,
            Notificaciones.NotificacionDispatcher>();
        services.AddScoped<Application.Notificaciones.INotificacionesService,
            Notificaciones.NotificacionesService>();

        // Calendario habil colombiano (cache estatico de festivos en memoria del proceso).
        services.AddScoped<Application.Common.ICalendarioHabilService, Common.CalendarioHabilService>();
        services.AddScoped<Pqrsd.PqrsdMantenimientoService>();

        // Modulo 0.3 Monitoria y Auditoria Global
        services.AddScoped<Application.Monitoria.IMonitoriaService, Monitoria.MonitoriaService>();

        // Background jobs - registro de scheduler + jobs concretos.
        // Cada job se inyecta como IBackgroundJob (multiples implementaciones).
        // El BackgroundJobScheduler es Singleton (BackgroundService) que cada tick
        // abre un scope nuevo para resolver los jobs scoped.
        services.AddScoped<Jobs.IBackgroundJob, Jobs.PqrsdCierreNocturnoJob>();
        services.AddScoped<Jobs.IBackgroundJob, Jobs.MetricasDiariasJob>();
        services.AddScoped<Jobs.IBackgroundJob, Jobs.CobroRecurrenteJob>();
        services.AddScoped<Jobs.IBackgroundJob, Jobs.ProgramacionTareasJob>();

        // Storage de blobs (logos, fachadas, portadas, futuros adjuntos).
        // Provider seleccionado por config: "R2" en produccion, cualquier otro valor (o ausente)
        // cae a filesystem local (wwwroot/uploads). Asi Development y tests no requieren credenciales.
        services.Configure<R2Options>(configuration.GetSection(R2Options.SectionName));
        var storageProvider = configuration["Storage:Provider"] ?? "Local";
        if (string.Equals(storageProvider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IBlobStorage, R2BlobStorage>();
        }
        else
        {
            services.AddSingleton<IBlobStorage, LocalBlobStorage>();
        }

        return services;
    }
}
