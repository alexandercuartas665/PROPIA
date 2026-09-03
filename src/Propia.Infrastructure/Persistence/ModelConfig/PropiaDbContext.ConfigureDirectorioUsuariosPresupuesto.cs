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
    // Modulos 2.4 Directorio, 2.5 Usuarios/Roles/Accesos y 2.6 Presupuesto/Cuotas/Pagos.
    private void ConfigureDirectorioUsuariosPresupuesto(ModelBuilder modelBuilder)
    {
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

        // Documentos adjuntos de la identidad (RUT, camara, certificados). GLOBAL como los contactos.
        modelBuilder.Entity<DirectorioAdjunto>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(300);
            b.Property(x => x.Url).IsRequired().HasMaxLength(600);
            b.Property(x => x.ContentType).HasMaxLength(120);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
            // Sin HasQueryFilter: mismo criterio global que DirectorioContacto.
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

    }
}
