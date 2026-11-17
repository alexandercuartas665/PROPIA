using Microsoft.EntityFrameworkCore;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Persistence;

// Lector de buzon (Ola 1, 2026-11-17). El bloque RLS (ENABLE + FORCE + policy + GRANT propia_app) va en la migracion
// AddLectorBuzon; RlsCoverageTests lo exige por ser TenantEntity con tenant_id.
public partial class PropiaDbContext
{
    private void ConfigureCorreosRecibidos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CorreoRecibido>(b =>
        {
            b.ToTable("correos_recibidos");
            b.Property(x => x.MessageId).IsRequired().HasMaxLength(500);
            b.Property(x => x.RemitenteCorreo).HasMaxLength(320);
            b.Property(x => x.RemitenteNombre).HasMaxLength(200);
            b.Property(x => x.Para).HasMaxLength(1000);
            b.Property(x => x.Asunto).HasMaxLength(500);
            b.Property(x => x.Resumen).HasMaxLength(1000);
            b.Property(x => x.AccionSugerida).HasMaxLength(1000);
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.Confianza).HasPrecision(4, 3);
            b.HasIndex(x => new { x.TenantId, x.MessageId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.FechaCorreo });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<TenantEmailConfig>(b =>
        {
            b.Property(x => x.ImapHost).HasMaxLength(200);
            b.Property(x => x.LecturaUltimoError).HasMaxLength(1000);
        });
    }
}
