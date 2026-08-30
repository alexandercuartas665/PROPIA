using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Tools de PLATAFORMA (conexion MCP "plataforma", prefijo "plataforma_"): las usa el agente de
/// bienvenida del Super Admin durante el onboarding /bienvenida. Corren con el JWT del usuario
/// (que puede NO tener tenant activo) y solo tocan tablas globales sin RLS (tenants,
/// organizaciones) o la funcion SECURITY DEFINER get_tenants_for_persona. El McpGateway
/// garantiza que estas tools jamas se listan ni ejecutan por la conexion "copropiedades"
/// de los agentes de tenant (particion por prefijo del catalogo).
/// </summary>
[McpServerToolType]
public sealed class PlataformaBienvenidaTools
{
    public sealed class NombreDisponibleDto
    {
        [JsonPropertyName("disponible")] public bool Disponible { get; init; }
        [JsonPropertyName("mensaje")] public string Mensaje { get; init; } = "";
    }

    [McpServerTool(Name = "plataforma_verificar_nombre")]
    [Description("Verifica si un nombre de copropiedad esta disponible para el usuario actual (no puede repetirse dentro de su organizacion). Usalo cuando el usuario proponga un nombre en el paso de la ficha, antes de crear.")]
    public static async Task<NombreDisponibleDto> VerificarNombre(
        PropiaDbContext db,
        IHttpContextAccessor http,
        [Description("Nombre de la copropiedad a verificar, tal cual lo escribio el usuario.")] string nombre,
        CancellationToken ct)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length == 0)
        {
            return new NombreDisponibleDto { Disponible = false, Mensaje = "El nombre esta vacio." };
        }

        var personaId = await PersonaIdActualAsync(db, http, ct);
        if (personaId is null)
        {
            return new NombreDisponibleDto { Disponible = true, Mensaje = "No pude identificar al usuario; el nombre se validara al crear." };
        }

        var orgIds = await OrganizacionesAdministradasAsync(db, personaId.Value, ct);
        if (orgIds.Count == 0)
        {
            // Usuario nuevo sin organizacion: su organizacion se crea junto con la copropiedad.
            return new NombreDisponibleDto { Disponible = true, Mensaje = $"'{nombre}' esta disponible." };
        }

        var duplicada = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.OrganizacionId != null && orgIds.Contains(t.OrganizacionId.Value) && t.Nombre == nombre, ct);
        return duplicada
            ? new NombreDisponibleDto { Disponible = false, Mensaje = $"Ya existe una copropiedad llamada '{nombre}' en la organizacion del usuario. Sugierele un nombre distinto." }
            : new NombreDisponibleDto { Disponible = true, Mensaje = $"'{nombre}' esta disponible." };
    }

    public sealed class EstadoCuentaDto
    {
        [JsonPropertyName("tiene_organizacion")] public bool TieneOrganizacion { get; init; }
        [JsonPropertyName("organizacion")] public string? Organizacion { get; init; }
        [JsonPropertyName("copropiedades_administradas")] public int CopropiedadesAdministradas { get; init; }
        [JsonPropertyName("nombres")] public List<string> Nombres { get; init; } = new();
        [JsonPropertyName("mensaje")] public string Mensaje { get; init; } = "";
    }

    [McpServerTool(Name = "plataforma_estado_cuenta")]
    [Description("Resumen de lo que el usuario actual ya tiene en la plataforma: si pertenece a una organizacion y cuantas copropiedades administra (con sus nombres). Usalo para orientar el recorrido de bienvenida (ej. si ya administra otras, la nueva copropiedad colgara de su misma organizacion).")]
    public static async Task<EstadoCuentaDto> EstadoCuenta(
        PropiaDbContext db,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var personaId = await PersonaIdActualAsync(db, http, ct);
        if (personaId is null)
        {
            return new EstadoCuentaDto { Mensaje = "No pude identificar al usuario." };
        }

        var tenantIds = await TenantsAdministradosAsync(db, personaId.Value, ct);
        if (tenantIds.Count == 0)
        {
            return new EstadoCuentaDto
            {
                TieneOrganizacion = false,
                CopropiedadesAdministradas = 0,
                Mensaje = "El usuario no administra ninguna copropiedad todavia: este recorrido crea la primera (y su organizacion)."
            };
        }

        var tenants = await db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Nombre, t.OrganizacionId })
            .ToListAsync(ct);
        var orgId = tenants.Select(t => t.OrganizacionId).FirstOrDefault(o => o != null);
        var orgNombre = orgId is null
            ? null
            : await db.Organizaciones.AsNoTracking().Where(o => o.Id == orgId).Select(o => o.Nombre).FirstOrDefaultAsync(ct);

        return new EstadoCuentaDto
        {
            TieneOrganizacion = orgId is not null,
            Organizacion = orgNombre,
            CopropiedadesAdministradas = tenants.Count,
            Nombres = tenants.Select(t => t.Nombre).OrderBy(n => n).ToList(),
            Mensaje = $"El usuario administra {tenants.Count} copropiedad(es){(orgNombre is null ? "" : $" en la organizacion {orgNombre}")}."
        };
    }

    // ---------- helpers ----------

    private static async Task<Guid?> PersonaIdActualAsync(PropiaDbContext db, IHttpContextAccessor http, CancellationToken ct)
    {
        var user = http.HttpContext?.User;
        var raw = user?.FindFirstValue("user_id") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) return null;
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.PersonaId).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Copropiedades donde la persona es Administrador, via la funcion SECURITY DEFINER
    /// get_tenants_for_persona (la RLS de usuarios_tenant solo deja ver el tenant activo,
    /// y en bienvenida normalmente NO hay tenant activo).
    /// </summary>
    private static async Task<List<Guid>> TenantsAdministradosAsync(PropiaDbContext db, Guid personaId, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var conn = db.Database.GetDbConnection();
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
                if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
                    ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            if (abiertaAqui) await conn.CloseAsync();
        }
        return ids;
    }

    private static async Task<List<Guid>> OrganizacionesAdministradasAsync(PropiaDbContext db, Guid personaId, CancellationToken ct)
    {
        var tenantIds = await TenantsAdministradosAsync(db, personaId, ct);
        if (tenantIds.Count == 0) return new List<Guid>();
        return await db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id) && t.OrganizacionId != null)
            .Select(t => t.OrganizacionId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }
}
