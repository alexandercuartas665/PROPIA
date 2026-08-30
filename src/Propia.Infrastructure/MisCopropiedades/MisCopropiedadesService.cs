using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.MisCopropiedades;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MisCopropiedades;

public class MisCopropiedadesService : IMisCopropiedadesService
{
    private const string RolAdministrador = "Administrador";

    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAiAgentTemplateService _aiAgentTemplates;
    private readonly ILogger<MisCopropiedadesService> _logger;

    public MisCopropiedadesService(
        PropiaDbContext db,
        ITenantContext tenantContext,
        IAiAgentTemplateService aiAgentTemplates,
        ILogger<MisCopropiedadesService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _aiAgentTemplates = aiAgentTemplates;
        _logger = logger;
    }

    public async Task<CopropiedadCreadaDto> CrearAsync(CrearCopropiedadRequest req, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la copropiedad es obligatorio.");

        var tenantActivo = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa en la sesion.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.PersonaId is not Guid personaId)
            throw new InvalidOperationException("El usuario no tiene una persona asociada.");

        // Guarda 1: el usuario debe ser Administrador en ALGUNA de sus copropiedades (capacidad de
        // organizacion, no solo la activa). Se usa la funcion SECURITY DEFINER get_tenants_for_persona
        // porque la RLS de usuarios_tenant solo deja ver el vinculo del tenant activo.
        var tenantsAdmin = await GetTenantsAdministradosAsync(personaId, ct);
        if (tenantsAdmin.Count == 0)
            throw new InvalidOperationException("Solo un Administrador puede crear una copropiedad nueva.");

        // Guarda 2: la nueva copropiedad cuelga de una organizacion que el usuario ADMINISTRA (no de
        // una donde solo es residente). Preferimos la organizacion de la copropiedad activa si la
        // administra; si no, la de la primera que administre. Hereda su suscripcion.
        var tenantParaOrg = tenantsAdmin.Contains(tenantActivo) ? tenantActivo : tenantsAdmin[0];
        var actual = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantParaOrg, ct);
        var organizacionId = actual?.OrganizacionId
            ?? throw new InvalidOperationException(
                "Esta copropiedad es autoadministrada (no pertenece a una organizacion), " +
                "por lo que no se pueden agregar mas copropiedades. Contacta a soporte.");

        var nombre = req.Nombre.Trim();
        var duplicada = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.OrganizacionId == organizacionId && t.Nombre == nombre, ct);
        if (duplicada)
            throw new InvalidOperationException($"Ya existe una copropiedad llamada '{nombre}' en tu organizacion.");

        // La tabla tenants no tiene RLS: se puede insertar con otro tenant activo en la sesion.
        var nuevo = new Tenant
        {
            Nombre = nombre,
            Nit = req.Nit,
            DigitoVerificacion = req.DigitoVerificacion,
            Direccion = req.Direccion,
            Departamento = req.Departamento,
            Ciudad = req.Ciudad,
            Pais = "Colombia",
            TipoCopropiedad = req.Tipo,
            Estrato = req.Estrato,
            OrganizacionId = organizacionId,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.ConAdmin,
            FechaActivacion = DateTimeOffset.UtcNow,
            CodigoCorto = await GenerarCodigoCortoUnicoAsync(ct)
        };
        _db.Tenants.Add(nuevo);
        await _db.SaveChangesAsync(ct);

        // usuarios_tenant SI tiene RLS (tenant_id = current_tenant_id()), asi que el INSERT del
        // vinculo solo pasa si app.tenant_id apunta al tenant NUEVO. Cambiamos el contexto en la
        // misma sesion SQL y lo devolvemos al tenant activo al terminar, porque la conexion es
        // compartida por el resto del request.
        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    SELECT set_config('app.tenant_id', '{nuevo.Id}', false);
                    INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                    VALUES ('{Guid.NewGuid()}', '{nuevo.Id}', '{personaId}', '{RolAdministrador}', 1, now(), now());";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Plantillas de agentes IA (best-effort, igual que en el onboarding): si falla, la
            // copropiedad queda creada y el usuario puede crear sus agentes a mano.
            try
            {
                var org = await _db.Organizaciones.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == organizacionId, ct);
                await _aiAgentTemplates.DeployToTenantAsync(nuevo.Id, nuevo.Nombre, org?.Nombre, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo desplegando plantillas de agente al tenant {TenantId}", nuevo.Id);
            }
        }
        finally
        {
            await using var restore = conn.CreateCommand();
            restore.CommandText = $"SELECT set_config('app.tenant_id', '{tenantActivo}', false);";
            await restore.ExecuteNonQueryAsync(ct);
            if (abiertaAqui) await conn.CloseAsync();
        }

        _logger.LogInformation("Copropiedad {Nombre} ({TenantId}) creada por persona {PersonaId} en la organizacion {OrgId}",
            nuevo.Nombre, nuevo.Id, personaId, organizacionId);

        return new CopropiedadCreadaDto(nuevo.Id, nuevo.Nombre);
    }

    /// <summary>
    /// Alta desde el onboarding de bienvenida (/bienvenida). A diferencia de CrearAsync:
    /// - NO exige tenant activo (el usuario recien llega y su JWT no tiene tenant_id).
    /// - Si el usuario no administra ninguna copropiedad, crea la ORGANIZACION segun su perfil
    ///   (empresa administradora con nombre/NIT, o autoadministrada a nombre de la persona).
    /// - Guarda ademas contacto y descripcion (la ficha del paso 3 del onboarding es mas completa).
    /// </summary>
    public async Task<CopropiedadCreadaDto> CrearPrimeraAsync(CrearPrimeraCopropiedadRequest req, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("El nombre de la copropiedad es obligatorio.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.PersonaId is not Guid personaId)
            throw new InvalidOperationException("El usuario no tiene una persona asociada.");
        var persona = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct)
            ?? throw new InvalidOperationException("No se encontro la persona del usuario.");

        // Organizacion: si ya administra copropiedades, reusa la organizacion de la primera que
        // tenga una (mismo criterio de CrearAsync). Si no, la crea segun el perfil elegido.
        var tenantsAdmin = await GetTenantsAdministradosAsync(personaId, ct);
        Guid? organizacionId = null;
        if (tenantsAdmin.Count > 0)
        {
            organizacionId = await _db.Tenants.AsNoTracking()
                .Where(t => tenantsAdmin.Contains(t.Id) && t.OrganizacionId != null)
                .Select(t => t.OrganizacionId)
                .FirstOrDefaultAsync(ct);
        }
        string? nombreOrg = null;
        if (organizacionId is null || organizacionId == Guid.Empty)
        {
            nombreOrg = req.EsEmpresa && !string.IsNullOrWhiteSpace(req.EmpresaNombre)
                ? req.EmpresaNombre!.Trim()
                : $"Administracion de {persona.Nombres} {persona.Apellidos}".Trim();
            var org = new Organizacion
            {
                Nombre = nombreOrg,
                Tipo = req.EsEmpresa ? TipoOrganizacion.Administradora : TipoOrganizacion.Autoadministrada,
                Nit = string.IsNullOrWhiteSpace(req.EmpresaNit) ? null : req.EmpresaNit!.Trim(),
                Email = user.Email,
                FechaActivacion = DateTimeOffset.UtcNow
            };
            _db.Organizaciones.Add(org);
            await _db.SaveChangesAsync(ct);
            organizacionId = org.Id;
        }

        var nombre = req.Nombre.Trim();
        var duplicada = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.OrganizacionId == organizacionId && t.Nombre == nombre, ct);
        if (duplicada)
            throw new InvalidOperationException($"Ya existe una copropiedad llamada '{nombre}' en tu organizacion.");

        // La tabla tenants no tiene RLS: se puede insertar sin tenant activo en la sesion.
        var nuevo = new Tenant
        {
            Nombre = nombre,
            Nit = string.IsNullOrWhiteSpace(req.Nit) ? null : req.Nit!.Trim(),
            DigitoVerificacion = string.IsNullOrWhiteSpace(req.DigitoVerificacion) ? null : req.DigitoVerificacion!.Trim(),
            Direccion = req.Direccion,
            Departamento = req.Departamento,
            Ciudad = req.Ciudad,
            Pais = "Colombia",
            TipoCopropiedad = req.Tipo,
            Estrato = req.Estrato,
            TelefonoContacto = req.Telefono,
            EmailContacto = req.Email,
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion!.Trim(),
            OrganizacionId = organizacionId,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.ConAdmin,
            FechaActivacion = DateTimeOffset.UtcNow,
            CodigoCorto = await GenerarCodigoCortoUnicoAsync(ct)
        };
        _db.Tenants.Add(nuevo);
        await _db.SaveChangesAsync(ct);

        // Vinculo usuarios_tenant bajo RLS: set_config al tenant NUEVO y restaurar al salir.
        // En el primer onboarding no hay tenant activo: se restaura a cadena vacia.
        var tenantActivo = _tenantContext.CurrentTenantId;
        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $@"
                    SELECT set_config('app.tenant_id', '{nuevo.Id}', false);
                    INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, fecha_activacion, created_at)
                    VALUES ('{Guid.NewGuid()}', '{nuevo.Id}', '{personaId}', '{RolAdministrador}', 1, now(), now());";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            try
            {
                if (nombreOrg is null)
                {
                    var org = await _db.Organizaciones.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == organizacionId, ct);
                    nombreOrg = org?.Nombre;
                }
                await _aiAgentTemplates.DeployToTenantAsync(nuevo.Id, nuevo.Nombre, nombreOrg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo desplegando plantillas de agente al tenant {TenantId}", nuevo.Id);
            }
        }
        finally
        {
            await using var restore = conn.CreateCommand();
            restore.CommandText = $"SELECT set_config('app.tenant_id', '{(tenantActivo?.ToString() ?? string.Empty)}', false);";
            await restore.ExecuteNonQueryAsync(ct);
            if (abiertaAqui) await conn.CloseAsync();
        }

        _logger.LogInformation("Primera copropiedad {Nombre} ({TenantId}) creada por persona {PersonaId} en la organizacion {OrgId}",
            nuevo.Nombre, nuevo.Id, personaId, organizacionId);

        return new CopropiedadCreadaDto(nuevo.Id, nuevo.Nombre);
    }

    /// <summary>
    /// Ids de las copropiedades donde la persona es Administrador (rol exacto), leidas via la
    /// funcion SECURITY DEFINER get_tenants_for_persona para saltar la RLS de usuarios_tenant (que
    /// solo deja ver el tenant activo). Habilita crear copropiedades desde cualquier contexto.
    /// </summary>
    private async Task<List<Guid>> GetTenantsAdministradosAsync(Guid personaId, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var conn = _db.Database.GetDbConnection();
        var abiertaAqui = conn.State != System.Data.ConnectionState.Open;
        if (abiertaAqui) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT tenant_id, rol FROM get_tenants_for_persona(@p_persona_id)";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p_persona_id";
            p.Value = personaId;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var rol = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (string.Equals(rol, RolAdministrador, StringComparison.OrdinalIgnoreCase))
                    ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }
        return ids;
    }

    // Alfabeto sin caracteres ambiguos (sin O/0, I/1, L) para un codigo legible de 6 chars.
    private const string CodigoAlfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private async Task<string> GenerarCodigoCortoUnicoAsync(CancellationToken ct)
    {
        for (var intento = 0; intento < 20; intento++)
        {
            var chars = new char[6];
            for (var i = 0; i < 6; i++) chars[i] = CodigoAlfabeto[Random.Shared.Next(CodigoAlfabeto.Length)];
            var code = new string(chars);
            if (!await _db.Tenants.AsNoTracking().AnyAsync(t => t.CodigoCorto == code, ct))
                return code;
        }
        // Extremadamente improbable; fallback con mas entropia.
        return "C" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
    }
}
