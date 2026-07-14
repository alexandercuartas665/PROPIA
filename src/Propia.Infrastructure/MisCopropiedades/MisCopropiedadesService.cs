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

        // Guarda 1: solo un Administrador de la copropiedad activa puede dar de alta otra.
        var vinculo = await _db.UsuariosTenant.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PersonaId == personaId && u.TenantId == tenantActivo, ct);
        if (vinculo is null || !string.Equals(vinculo.Rol, RolAdministrador, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solo un Administrador puede crear una copropiedad nueva.");

        // Guarda 2: la nueva copropiedad cuelga de la organizacion y hereda su suscripcion.
        // Sin organizacion no hay de que colgarla (la suscripcion estaria atada a la copropiedad sola).
        var actual = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantActivo, ct);
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
            FechaActivacion = DateTimeOffset.UtcNow
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
}
