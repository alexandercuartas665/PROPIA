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

// Configuracion de modelo EF Core extraida de PropiaDbContext.OnModelCreating.
// Clase parcial: conserva acceso a _tenantContext (los HasQueryFilter siguen
// capturando la misma instancia). El orden de invocacion lo fija OnModelCreating.
public partial class PropiaDbContext
{
    // Modulo 0.3 Monitoria/Auditoria, etiquetas de usuario, integraciones de plataforma, menu, infraestructura IA (2.x) y 0.2 Billing.
    private void ConfigureAuditoriaIaBilling(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<GmailEnvioAppConfig>(b =>
        {
            b.ToTable("gmail_envio_app_configs");
            b.Property(x => x.ClientId).HasMaxLength(255);
            b.Property(x => x.ClientSecretEncrypted).HasMaxLength(1024);
        });

        modelBuilder.Entity<GmailEnvioConexion>(b =>
        {
            b.ToTable("gmail_envio_conexiones");
            b.Property(x => x.Email).IsRequired().HasMaxLength(320);
            b.Property(x => x.RefreshTokenEncrypted).HasMaxLength(2048);
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
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
}
