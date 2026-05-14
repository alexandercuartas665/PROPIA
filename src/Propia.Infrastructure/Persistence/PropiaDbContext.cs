using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
///
/// Hereda de IdentityDbContext para integrar ASP.NET Core Identity (paso 6).
/// Las tablas de Identity (asp_net_users, asp_net_roles, ...) son GLOBALES,
/// no llevan tenant_id. La gestion de quien tiene acceso a que copropiedad
/// vive en UsuarioTenant (modulo 2.5).
/// </summary>
public class PropiaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
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

    // Modulo 2.3 Mi Copropiedad - todas TenantEntity (RLS + tenant_id)
    public DbSet<Torre> Torres => Set<Torre>();
    public DbSet<UnidadPrivada> UnidadesPrivadas => Set<UnidadPrivada>();
    public DbSet<ZonaComun> ZonasComunes => Set<ZonaComun>();
    public DbSet<EquipoActivo> EquiposActivos => Set<EquipoActivo>();
    public DbSet<ContratoServicio> ContratosServicio => Set<ContratoServicio>();
    public DbSet<MiembroConsejo> MiembrosConsejo => Set<MiembroConsejo>();

    // Modulo 0.2 - Billing y Suscripciones (todo GLOBAL, sin tenant_id)
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<Cupon> Cupones => Set<Cupon>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<IntentoCobro> IntentosCobro => Set<IntentoCobro>();
    public DbSet<SuscripcionHistorial> SuscripcionHistorial => Set<SuscripcionHistorial>();
    public DbSet<BillingConfig> BillingConfig => Set<BillingConfig>();

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

        // -------------------- Modulo 2.3 Mi Copropiedad --------------------

        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Ciudad).HasMaxLength(100);
            b.Property(x => x.Departamento).HasMaxLength(100);
            b.Property(x => x.DigitoVerificacion).HasMaxLength(2);
            b.Property(x => x.FotoFachadaUrl).HasMaxLength(500);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
        });

        modelBuilder.Entity<Torre>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPrivada>(b =>
        {
            b.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            b.Property(x => x.Estado).HasMaxLength(50);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.CoeficientePropiedad).HasPrecision(7, 4);
            b.Property(x => x.AreaM2).HasPrecision(10, 2);
            b.HasOne(x => x.Torre).WithMany().HasForeignKey(x => x.TorreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaComun>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.HorariosUso).HasMaxLength(200);
            b.Property(x => x.ReglasUso).HasMaxLength(2000);
            b.Property(x => x.TarifaReserva).HasPrecision(12, 2);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EquipoActivo>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Marca).HasMaxLength(100);
            b.Property(x => x.Modelo).HasMaxLength(100);
            b.Property(x => x.NumeroSerie).HasMaxLength(100);
            b.Property(x => x.Ubicacion).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ContratoServicio>(b =>
        {
            b.Property(x => x.Proveedor).IsRequired().HasMaxLength(200);
            b.Property(x => x.NitProveedor).HasMaxLength(20);
            b.Property(x => x.Contacto).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.ValorMensual).HasPrecision(12, 2);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.FechaFin);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MiembroConsejo>(b =>
        {
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Cargo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
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

        // ApplicationUser - relacion opcional con Persona del Directorio
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.HasOne(x => x.Persona)
                .WithMany()
                .HasForeignKey(x => x.PersonaId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.PersonaId);
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
