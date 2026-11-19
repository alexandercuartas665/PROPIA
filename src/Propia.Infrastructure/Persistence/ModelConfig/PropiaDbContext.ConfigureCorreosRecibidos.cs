using Microsoft.EntityFrameworkCore;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Persistence;

// Lector de buzon (Ola 1, 2026-11-17). El bloque RLS (ENABLE + FORCE + policy + GRANT propia_app) va en las migraciones
// AddLectorBuzon (correos_recibidos) y AddBuzonesCorreo (buzones_correo); RlsCoverageTests lo exige por ser TenantEntity.
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
            b.Property(x => x.TemaClave).HasMaxLength(100);
            b.Property(x => x.Resumen).HasMaxLength(1000);
            b.Property(x => x.AccionSugerida).HasMaxLength(1000);
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.Confianza).HasPrecision(4, 3);
            b.HasIndex(x => new { x.TenantId, x.MessageId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.FechaCorreo });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => x.BuzonId);
            b.HasOne<BuzonCorreo>().WithMany().HasForeignKey(x => x.BuzonId).OnDelete(DeleteBehavior.SetNull);
            // PQRSD por correo (Ola P1): el expediente radicado desde el correo. Si el expediente se borra, el correo queda.
            b.Property(x => x.PqrsdRadicado).HasMaxLength(60);
            b.HasIndex(x => x.PqrsdExpedienteId);
            b.HasOne<PqrsdExpediente>().WithMany().HasForeignKey(x => x.PqrsdExpedienteId).OnDelete(DeleteBehavior.SetNull);
            // Ola P2: respuesta de radicado enviada desde la cuenta del buzon.
            b.Property(x => x.AcuseDestino).HasMaxLength(320);
            b.Property(x => x.AcuseError).HasMaxLength(1000);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<BuzonCorreo>(b =>
        {
            b.ToTable("buzones_correo");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Cuenta).IsRequired().HasMaxLength(320);
            b.Property(x => x.ImapHost).IsRequired().HasMaxLength(200);
            b.Property(x => x.UltimoError).HasMaxLength(1000);
            b.Property(x => x.UmbralAutoPqrsd).HasPrecision(4, 3);
            // Ola P2: plantilla de la respuesta de radicado (si se borra la plantilla, se usa la predeterminada).
            b.HasOne<PlantillaDocumento>().WithMany().HasForeignKey(x => x.PlantillaAcuseId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TenantId, x.Cuenta }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
    }
}
