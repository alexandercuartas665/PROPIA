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
    public DbSet<TipoUnidadCustom> TiposUnidadCustom => Set<TipoUnidadCustom>();
    public DbSet<TipoCoeficiente> TiposCoeficiente => Set<TipoCoeficiente>();
    public DbSet<UnidadCoeficiente> UnidadCoeficientes => Set<UnidadCoeficiente>();
    public DbSet<Comite> Comites => Set<Comite>();
    public DbSet<ComiteMiembro> ComiteMiembros => Set<ComiteMiembro>();
    public DbSet<RevisorFiscal> RevisoresFiscales => Set<RevisorFiscal>();
    public DbSet<MiembroEquipo> MiembrosEquipo => Set<MiembroEquipo>();

    // Modulo 2.4 Directorio
    public DbSet<EtiquetaCatalogo> EtiquetasCatalogo => Set<EtiquetaCatalogo>();  // global+tenant mezclados via TenantId nullable
    public DbSet<DirectorioVinculo> DirectorioVinculos => Set<DirectorioVinculo>();
    public DbSet<DirectorioContacto> DirectorioContactos => Set<DirectorioContacto>();
    public DbSet<DirectorioEtiqueta> DirectorioEtiquetas => Set<DirectorioEtiqueta>();
    public DbSet<PersonaEmpresa> PersonaEmpresas => Set<PersonaEmpresa>();

    // Modulo 2.5 Usuarios, Roles y Accesos
    // (RolesCopropiedad para no colisionar con IdentityDbContext.Roles de IdentityRole)
    public DbSet<Rol> RolesCopropiedad => Set<Rol>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioInvitacion> UsuarioInvitaciones => Set<UsuarioInvitacion>();
    public DbSet<UsuarioAuthMetodo> UsuarioAuthMetodos => Set<UsuarioAuthMetodo>();
    public DbSet<UsuarioSesion> UsuarioSesiones => Set<UsuarioSesion>();
    public DbSet<AccesoAuditoria> AccesoAuditorias => Set<AccesoAuditoria>();

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
            // Identidad registral (modulo 2.3 spec v1.0)
            b.Property(x => x.NumeroReglamentoPh).HasMaxLength(100);
            b.Property(x => x.NotariaRegistro).HasMaxLength(200);
            b.Property(x => x.MatriculaInmobiliaria).HasMaxLength(50);
            b.Property(x => x.LicenciaConstruccion).HasMaxLength(50);
            // Labels personalizables (spec v1.0 - "Sector"/"Planta")
            b.Property(x => x.LabelAgrupacion).HasMaxLength(30);
            b.Property(x => x.LabelPiso).HasMaxLength(30);
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

        // Tipo unidad custom (spec 2.3 - tipos personalizables por copropiedad)
        modelBuilder.Entity<TipoUnidadCustom>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Tipos de coeficiente PH (spec 2.3 - RN-02 multiples tipos por copropiedad)
        modelBuilder.Entity<TipoCoeficiente>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Valores de coeficientes por unidad (relacion M-N + Valor)
        modelBuilder.Entity<UnidadCoeficiente>(b =>
        {
            b.Property(x => x.Valor).HasPrecision(9, 6);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.TipoCoeficiente).WithMany().HasForeignKey(x => x.TipoCoeficienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.UnidadId, x.TipoCoeficienteId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Comites y miembros (spec 2.3 - seccion 4)
        modelBuilder.Entity<Comite>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComiteMiembro>(b =>
        {
            b.Property(x => x.CargoEnComite).HasMaxLength(80);
            b.HasOne(x => x.Comite).WithMany(c => c.Miembros).HasForeignKey(x => x.ComiteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ComiteId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RevisorFiscal>(b =>
        {
            b.Property(x => x.NumeroTarjetaProfesional).HasMaxLength(50);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Equipo de trabajo (spec 2.3 - seccion 3)
        modelBuilder.Entity<MiembroEquipo>(b =>
        {
            b.Property(x => x.RolPersonalizado).HasMaxLength(80);
            b.Property(x => x.Telefono).HasMaxLength(30);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PersonaId, x.Rol });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

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
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
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

        // RLS lo cubre TenantConnectionInterceptor.ConnectionOpenedAsync, que
        // setea app.tenant_id en cada conexion abierta por EF. Aqui solo
        // delegamos al base SaveChangesAsync.
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
