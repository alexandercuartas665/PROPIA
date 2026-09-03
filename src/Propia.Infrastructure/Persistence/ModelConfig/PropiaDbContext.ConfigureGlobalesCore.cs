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
    // Entidades globales core: Organizacion, Tenant, Persona, Empresa, contactos de notificacion.
    private void ConfigureGlobalesCore(ModelBuilder modelBuilder)
    {
        // Organizacion
        modelBuilder.Entity<Organizacion>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Nit).HasMaxLength(20);
            b.Property(x => x.Email).HasMaxLength(200).HasColumnType("citext");
            b.Property(x => x.Estado).HasConversion<int>().HasDefaultValue(EstadoOrganizacion.Activa);
            b.HasIndex(x => x.Nit).IsUnique().HasFilter("nit IS NOT NULL");
        });

        // Tenant (Copropiedad)
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Nit).HasMaxLength(20);
            b.Property(x => x.CodigoPropia).HasMaxLength(20);
            b.HasIndex(x => x.CodigoPropia).IsUnique().HasFilter("codigo_propia IS NOT NULL");
            b.Property(x => x.CodigoCorto).HasMaxLength(6);
            b.HasIndex(x => x.CodigoCorto).IsUnique().HasFilter("codigo_corto IS NOT NULL");
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

        // Contactos de notificacion del usuario (global por persona, sin tenant/RLS).
        modelBuilder.Entity<UsuarioContactoNotificacion>(b =>
        {
            b.Property(x => x.Valor).IsRequired().HasMaxLength(200);
            b.Property(x => x.Canal).HasConversion<int>();
            b.HasIndex(x => x.PersonaId);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Cascade);
        });

        // Empresa
        modelBuilder.Entity<Empresa>(b =>
        {
            b.Property(x => x.Nit).IsRequired().HasMaxLength(20);
            b.Property(x => x.RazonSocial).IsRequired().HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(200).HasColumnType("citext");
            b.HasIndex(x => x.Nit).IsUnique();
        });

    }
}
