using Microsoft.EntityFrameworkCore;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Persistence;

// Modulo Siniestros (2026-11-16). Clase parcial: conserva acceso a _tenantContext para el HasQueryFilter.
// El bloque RLS (ENABLE + FORCE + policy + GRANT propia_app) va en la migracion AddSiniestros; RlsCoverageTests
// lo exige por ser TenantEntity con tenant_id.
public partial class PropiaDbContext
{
    private void ConfigureSiniestros(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Siniestro>(b =>
        {
            b.ToTable("siniestros");
            b.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(4000);
            b.Property(x => x.TipoClave).HasMaxLength(120);
            b.Property(x => x.ElementoOtro).HasMaxLength(300);
            b.Property(x => x.NumeroReclamacion).HasMaxLength(80);
            b.Property(x => x.MontoEstimado).HasPrecision(14, 2);
            b.Property(x => x.MontoReclamado).HasPrecision(14, 2);
            b.Property(x => x.Deducible).HasPrecision(14, 2);
            b.Property(x => x.MontoReconocido).HasPrecision(14, 2);
            b.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.EstadoId });
            b.HasIndex(x => new { x.TenantId, x.ElementoTipo, x.ElementoId });
            b.HasIndex(x => x.PolizaId);
            b.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Poliza).WithMany().HasForeignKey(x => x.PolizaId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SiniestroEstado>(b =>
        {
            b.ToTable("siniestro_estados");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.Property(x => x.Clave).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            // Un solo estado base por clave y copropiedad (frena la siembra concurrente duplicada).
            b.HasIndex(x => new { x.TenantId, x.Clave }).IsUnique().HasFilter("clave IS NOT NULL");
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SiniestroDocumento>(b =>
        {
            b.ToTable("siniestro_documentos");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.Url).IsRequired();
            b.HasOne(x => x.Siniestro).WithMany().HasForeignKey(x => x.SiniestroId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SiniestroId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SiniestroComentario>(b =>
        {
            b.ToTable("siniestro_comentarios");
            b.Property(x => x.Texto).IsRequired().HasMaxLength(4000);
            b.Property(x => x.AutorNombre).HasMaxLength(200);
            b.HasOne(x => x.Siniestro).WithMany().HasForeignKey(x => x.SiniestroId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SiniestroId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SiniestroCampo>(b =>
        {
            b.ToTable("siniestro_campos");
            b.Property(x => x.Label).IsRequired().HasMaxLength(120);
            b.Property(x => x.Opciones).HasMaxLength(2000);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SiniestroCampoValor>(b =>
        {
            b.ToTable("siniestro_campo_valores");
            b.Property(x => x.Valor).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.SiniestroId });
            b.HasIndex(x => new { x.SiniestroId, x.SiniestroCampoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
    }
}
