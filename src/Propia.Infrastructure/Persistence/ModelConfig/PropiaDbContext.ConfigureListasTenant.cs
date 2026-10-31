using Microsoft.EntityFrameworkCore;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Persistence;

// Ola 4: copia POR TENANT de las listas del sistema (lista_opciones). Clase parcial: conserva acceso a
// _tenantContext para el HasQueryFilter. El bloque RLS (ENABLE + FORCE + policy + GRANT propia_app) va en la
// migracion (pendiente de OK de Alex); RlsCoverageTests lo exige por ser TenantEntity con tenant_id.
public partial class PropiaDbContext
{
    private void ConfigureListasTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ListaOpcion>(b =>
        {
            b.ToTable("lista_opciones");
            b.Property(x => x.Lista).IsRequired().HasMaxLength(80);
            b.Property(x => x.Clave).IsRequired().HasMaxLength(120);
            b.Property(x => x.Label).IsRequired().HasMaxLength(200);
            b.Property(x => x.Color).HasMaxLength(20);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => new { x.TenantId, x.Lista });
            b.HasIndex(x => new { x.TenantId, x.Lista, x.Clave }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
    }
}
