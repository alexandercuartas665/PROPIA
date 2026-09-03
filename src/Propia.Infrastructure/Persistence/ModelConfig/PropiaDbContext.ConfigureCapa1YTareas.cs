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
    // Modulos 1.3 Gestion de Equipo, 1.1 Panel/Dashboard consolidado, 2.2 Dashboard copropiedad y 2.10 Tareas y Proyectos.
    private void ConfigureCapa1YTareas(ModelBuilder modelBuilder)
    {
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
            // Unicidad por (tenant, tablero, nombre): permite el mismo nombre en tableros distintos.
            b.HasIndex(x => new { x.TenantId, x.TableroId, x.Nombre }).IsUnique();
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

        // Campo personalizado del tablero: Activo por defecto (los existentes quedan activos).
        modelBuilder.Entity<TableroCampo>(b =>
        {
            b.Property(x => x.Activo).HasDefaultValue(true);
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
            b.HasOne(x => x.SolicitantePersona).WithMany().HasForeignKey(x => x.SolicitantePersonaId).OnDelete(DeleteBehavior.SetNull);
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

    }
}
