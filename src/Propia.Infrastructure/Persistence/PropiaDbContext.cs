using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Propia.Application.Common;
using Propia.Domain.Common;
using Propia.Domain.Entities;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de PROPIA. Aplica:
/// 1. Auditoria automatica (CreatedAt / UpdatedAt) en SaveChangesAsync.
/// 2. Asignacion automatica de TenantId a entidades TenantEntity nuevas.
/// 3. HasQueryFilter por tenant en todas las TenantEntity como red de seguridad.
///    (La red final es Row-Level Security de PostgreSQL, configurada en migracion posterior.)
/// </summary>
public class PropiaDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public PropiaDbContext(DbContextOptions<PropiaDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Entidades globales (sin tenant_id)
    public DbSet<Organizacion> Organizaciones => Set<Organizacion>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<SuperAdminUsuario> SuperAdminUsuarios => Set<SuperAdminUsuario>();
    public DbSet<SuperAdminLog> SuperAdminLogs => Set<SuperAdminLog>();

    // Entidades de tenant (con tenant_id + RLS + HasQueryFilter)
    public DbSet<UsuarioTenant> UsuariosTenant => Set<UsuarioTenant>();

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

        // UsuarioTenant (TenantEntity)
        modelBuilder.Entity<UsuarioTenant>(b =>
        {
            b.Property(x => x.Rol).IsRequired().HasMaxLength(100);
            b.HasOne(x => x.Persona)
                .WithMany(x => x.VinculosATenants)
                .HasForeignKey(x => x.PersonaId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.PersonaId }).IsUnique();
            b.HasIndex(x => x.TenantId);

            // Red de seguridad de aplicacion. RLS de Postgres es la red final.
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
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
