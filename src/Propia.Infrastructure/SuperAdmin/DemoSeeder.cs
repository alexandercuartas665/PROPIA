using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Siembra un set de datos de ejemplo PEQUENO y reproducible para Development:
/// 4 planes, 10 organizaciones, 10 copropiedades, una suscripcion por copropiedad y 3 usuarios
/// demo de cliente (Administrador Delegado, Consejo, Personal Interno) sobre la 1a copropiedad.
/// IDEMPOTENTE y SEGURO: cada bloque tiene su propio guard; nunca borra ni modifica datos
/// existentes. Para regenerar todo: resetear el schema y reiniciar la API.
/// </summary>
public static class DemoSeeder
{
    /// <summary>Clave comun de los 3 usuarios demo de cliente (cumple la politica: >=10, mayus, digito).</summary>
    public const string DemoUserPassword = "PropiaDemo2026!";

    public static async Task EnsureDemoDataAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureBaseDataAsync(db, logger);
        await EnsureDemoUsersAsync(db, userManager, logger);
    }

    private static async Task EnsureBaseDataAsync(PropiaDbContext db, ILogger logger)
    {
        // Solo en BD fresca: si ya hay organizaciones, no tocar nada.
        if (await db.Organizaciones.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // ---- 4 planes (escalera comercial) ----
        var planes = new[]
        {
            NuevoPlan("Basico", "Para copropiedades pequenas", 80000m, 0m, 50, 10, 1, 100, 30, now),
            NuevoPlan("Estandar", "El plan mas popular", 150000m, 10m, 150, 30, 3, 500, 30, now),
            NuevoPlan("Profesional", "Para administradoras de varias PH", 280000m, 15m, 400, 80, 10, 2000, 15, now),
            NuevoPlan("Enterprise", "Sin limites + soporte prioritario", 500000m, 20m, null, null, null, null, 0, now),
        };
        db.Planes.AddRange(planes);

        // ---- 10 organizaciones administradoras ----
        var nombresOrg = new[]
        {
            "Helena Inmobiliaria SAS", "Administraciones del Valle SAS", "Gestion PH Andina SAS",
            "Cubot Administradora SAS", "Vivienda Activa SAS", "Propiedad Horizontal Pacifico SAS",
            "AdminColombia SAS", "Conjuntos del Norte SAS", "Servicios PH Centro SAS",
            "Habitat Administradora SAS",
        };
        var ciudades = new[] { "Cali", "Medellin", "Bogota", "Barranquilla", "Bucaramanga", "Pereira", "Manizales", "Cartagena", "Cucuta", "Ibague" };
        var orgs = new List<Organizacion>();
        for (var i = 0; i < nombresOrg.Length; i++)
        {
            orgs.Add(new Organizacion
            {
                Nombre = nombresOrg[i],
                Tipo = TipoOrganizacion.Administradora,
                Nit = $"9{(i + 10):D2}123456",
                Email = $"contacto{i + 1}@{nombresOrg[i].Split(' ')[0].ToLowerInvariant()}.com",
                Telefono = $"+57 3{(i + 10):D2} 555 0{i:D3}",
                FechaActivacion = now,
                CreatedAt = now,
            });
        }
        db.Organizaciones.AddRange(orgs);

        // ---- 10 copropiedades (round-robin sobre las orgs) ----
        var nombresPH = new[]
        {
            "Edificio Portal del Sol", "Conjunto Altos del Bosque", "Edificio Mirador del Rio",
            "Conjunto Villa Verde", "Edificio Torres del Parque", "Conjunto La Castellana",
            "Edificio Brisas del Mar", "Conjunto Jardines del Norte", "Edificio Plaza Central",
            "Conjunto Camino Real",
        };
        var tipos = new[] { TipoCopropiedad.Residencial, TipoCopropiedad.Conjunto, TipoCopropiedad.Mixto, TipoCopropiedad.Comercial };
        var estratos = new[] { Estrato.Tres, Estrato.Cuatro, Estrato.Cinco, Estrato.Seis };
        var tenants = new List<Tenant>();
        for (var i = 0; i < nombresPH.Length; i++)
        {
            tenants.Add(new Tenant
            {
                Nombre = nombresPH[i],
                Nit = $"8{(i + 10):D2}654321",
                DigitoVerificacion = $"{i % 10}",
                Ciudad = ciudades[i],
                Departamento = "Colombia",
                Direccion = $"Calle {10 + i} # {20 + i}-{30 + i}",
                CodigoPropia = $"PROPIA-{(i + 1):D4}",
                TipoCopropiedad = tipos[i % tipos.Length],
                Estrato = estratos[i % estratos.Length],
                Estado = EstadoCopropiedad.Activa,
                EstadoCustodia = EstadoCustodia.SinAdmin,
                FechaActivacion = now,
                OrganizacionId = orgs[i % orgs.Count].Id,
                CreatedAt = now,
            });
        }
        db.Tenants.AddRange(tenants);

        // ---- 1 suscripcion por copropiedad (round-robin sobre los 4 planes) ----
        for (var i = 0; i < tenants.Count; i++)
        {
            db.Suscripciones.Add(new Suscripcion
            {
                CopropiedadId = tenants[i].Id,
                OrganizacionId = null, // XOR: pertenece a la copropiedad
                PlanId = planes[i % planes.Length].Id,
                Ciclo = CicloFacturacion.Mensual,
                Estado = EstadoSuscripcion.Activa,
                FechaInicio = hoy,
                FechaAniversario = 1,
                FechaProximoCobro = hoy.AddMonths(1),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        logger.LogWarning("[DEV] DemoSeeder: sembrados 4 planes, {Orgs} organizaciones, {Tenants} copropiedades y {Subs} suscripciones.",
            orgs.Count, tenants.Count, tenants.Count);
    }

    /// <summary>
    /// Crea 3 usuarios demo de cliente sobre la primera copropiedad: Administrador Delegado,
    /// Consejo y Personal Interno. Idempotente (guard por email). Mismo patron que el onboarding:
    /// Persona + ApplicationUser (Identity) + UsuarioTenant via SQL con set_config (RLS).
    /// </summary>
    private static async Task EnsureDemoUsersAsync(PropiaDbContext db, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        const string adminEmail = "admin@demo.propia";
        if (await db.Users.AnyAsync(u => u.Email == adminEmail)) return;

        // Primera copropiedad (la de codigo PROPIA-0001) como contexto de los usuarios demo.
        var tenant = await db.Tenants.OrderBy(t => t.CodigoPropia).FirstOrDefaultAsync();
        if (tenant is null) return;

        var now = DateTimeOffset.UtcNow;
        var perfiles = new[]
        {
            (Email: adminEmail,            Nombres: "Admin",    Apellidos: "Delegado Demo", Rol: "Administrador"),
            (Email: "consejo@demo.propia",  Nombres: "Carlos",   Apellidos: "Consejo Demo",  Rol: "Consejero"),
            (Email: "personal@demo.propia", Nombres: "Lucia",    Apellidos: "Personal Demo", Rol: "Operario"),
        };

        foreach (var p in perfiles)
        {
            if (await db.Users.AnyAsync(u => u.Email == p.Email)) continue;

            var persona = new Persona
            {
                TipoDocumento = TipoDocumento.CC,
                Documento = $"DEMO-{Guid.NewGuid().ToString("N")[..8]}",
                Nombres = p.Nombres,
                Apellidos = p.Apellidos,
                Email = p.Email,
                CreatedAt = now,
            };
            db.Personas.Add(persona);
            await db.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = p.Email,
                Email = p.Email,
                EmailConfirmed = true,
                PersonaId = persona.Id,
            };
            var created = await userManager.CreateAsync(user, DemoUserPassword);
            if (!created.Succeeded)
            {
                logger.LogWarning("[DEV] DemoSeeder: no se pudo crear usuario {Email}: {Errores}",
                    p.Email, string.Join(", ", created.Errors.Select(e => e.Description)));
                continue;
            }

            // UsuarioTenant via SQL: RLS exige app.tenant_id seteado en la misma sesion.
            var conn = db.Database.GetDbConnection();
            var opened = conn.State != System.Data.ConnectionState.Open;
            if (opened) await conn.OpenAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT set_config('app.tenant_id', '{tenant.Id}', false);
                    INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                    VALUES ('{Guid.NewGuid()}', '{tenant.Id}', '{persona.Id}', '{p.Rol}', 1, now(), now());";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (opened) await conn.CloseAsync();
            }
        }

        logger.LogWarning("[DEV] DemoSeeder: creados 3 usuarios demo de cliente sobre '{Tenant}' (clave: {Pwd}).",
            tenant.Nombre, DemoUserPassword);
    }

    private static Plan NuevoPlan(string nombre, string desc, decimal feeBase, decimal descAnual,
        int? limUnidades, int? limUsuarios, int? limWhatsapp, int? limIa, int diasTrial, DateTimeOffset now) => new()
    {
        Nombre = nombre,
        Descripcion = desc,
        FeeBase = feeBase,
        FeeVariablePorUnidad = 0m,
        CicloMensual = true,
        CicloAnual = true,
        DescuentoAnualPct = descAnual,
        LimiteUnidades = limUnidades,
        LimiteUsuarios = limUsuarios,
        LimiteLineasWhatsapp = limWhatsapp,
        LimiteLlamadasIaMensual = limIa,
        DiasTrial = diasTrial,
        Estado = EstadoPlan.Activo,
        CreatedAt = now,
    };
}
