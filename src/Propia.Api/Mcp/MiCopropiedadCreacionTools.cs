using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Capa MCP de 2.3 Mi Copropiedad - tools de CREACION (mutaciones).
///
/// Patron de seguridad de cada tool:
/// 1. dryRun=true por defecto -> valida y simula SIN persistir nada (rollback de la
///    transaccion). El agente debe llamar explicitamente con dryRun=false para confirmar.
/// 2. Toda la operacion corre dentro de una transaccion sobre el PropiaDbContext del
///    request. El tenant lo fija el TenantMiddleware (RLS) y los permisos los hereda del
///    JWT del usuario: el agente no puede crear en una copropiedad que no sea la activa.
/// 3. Al confirmar (dryRun=false) se registra una entrada en la bitacora marcada como
///    "(agente MCP)" para auditoria (RN-06).
/// 4. Los errores de validacion del dominio se devuelven como Exito=false con el mensaje,
///    no como excepcion: el agente puede corregir y reintentar.
/// </summary>
[McpServerToolType]
public sealed class MiCopropiedadCreacionTools
{
    [McpServerTool(Name = "micopropiedad_crear_torre")]
    [Description("Crea una torre/agrupacion en la copropiedad activa. Por defecto es dry-run (valida sin guardar); pasa dryRun=false para confirmar la creacion.")]
    public static Task<ResultadoCreacionMcp> CrearTorre(
        [Description("Datos de la torre a crear.")] CrearTorreRequest torre,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, svc, tenant, dryRun, "Torre",
            r => $"Torre '{((TorreDto)r!).Nombre}' creada (agente MCP).",
            async () => await svc.CrearTorreAsync(torre, ct), ct);

    [McpServerTool(Name = "micopropiedad_crear_unidad")]
    [Description("Crea una unidad privada (apartamento, local, parqueadero, etc.) en la copropiedad activa. El coeficiente debe estar entre 0 y 100. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearUnidad(
        [Description("Datos de la unidad a crear (numero, tipo, coeficiente, etc.).")] CrearUnidadRequest unidad,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, svc, tenant, dryRun, "Unidad",
            r => $"Unidad '{((UnidadDto)r!).Numero}' creada (agente MCP).",
            async () => await svc.CrearUnidadAsync(unidad, ct), ct);

    [McpServerTool(Name = "micopropiedad_crear_contrato")]
    [Description("Crea un contrato de servicios (vigilancia, aseo, ascensores, etc.) en la copropiedad activa. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearContrato(
        [Description("Datos del contrato a crear (tipo, proveedor, fechas, valor, etc.).")] CrearContratoServicioRequest contrato,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, svc, tenant, dryRun, "Servicio",
            r => $"Contrato con '{((ContratoServicioDto)r!).Proveedor}' creado (agente MCP).",
            async () => await svc.CrearContratoAsync(contrato, ct), ct);

    [McpServerTool(Name = "micopropiedad_crear_zona")]
    [Description("Crea una zona comun (salon social, piscina, gimnasio, etc.) en la copropiedad activa. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearZona(
        [Description("Datos de la zona comun a crear.")] CrearZonaComunRequest zona,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, svc, tenant, dryRun, "Zona",
            r => $"Zona '{((ZonaComunDto)r!).Nombre}' creada (agente MCP).",
            async () => await svc.CrearZonaComunAsync(zona, ct), ct);

    [McpServerTool(Name = "micopropiedad_crear_equipo")]
    [Description("Crea un equipo/activo (bomba, ascensor, planta electrica, etc.) en la copropiedad activa. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearEquipo(
        [Description("Datos del equipo/activo a crear.")] CrearEquipoActivoRequest equipo,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, svc, tenant, dryRun, "Equipo",
            r => $"Equipo '{((EquipoActivoDto)r!).Nombre}' creado (agente MCP).",
            async () => await svc.CrearEquipoAsync(equipo, ct), ct);

    // ---------- Motor comun ----------

    /// <summary>
    /// Ejecuta una creacion dentro de una transaccion. En dry-run hace rollback (valida
    /// sin persistir). Al confirmar, registra en bitacora y commitea. Captura los errores
    /// de validacion del dominio y los devuelve como Exito=false (el agente reintenta).
    /// </summary>
    private static async Task<ResultadoCreacionMcp> EjecutarAsync<T>(
        PropiaDbContext db, IMiCopropiedadService svc, ITenantContext tenant,
        bool dryRun, string categoriaBitacora, Func<object?, string> descripcionBitacora,
        Func<Task<T>> crear, CancellationToken ct)
    {
        RequireTenant(tenant);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var recurso = await crear();
            if (dryRun)
            {
                await tx.RollbackAsync(ct);
                return new ResultadoCreacionMcp(
                    DryRun: true, Exito: true,
                    Mensaje: "Validacion OK. No se persistio nada (dry-run). Vuelve a llamar con dryRun=false para confirmar.",
                    Recurso: recurso);
            }

            await svc.RegistrarBitacoraAsync(categoriaBitacora, descripcionBitacora(recurso), ct);
            await tx.CommitAsync(ct);
            return new ResultadoCreacionMcp(
                DryRun: false, Exito: true,
                Mensaje: "Creado y registrado en la bitacora.",
                Recurso: recurso);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return new ResultadoCreacionMcp(
                DryRun: dryRun, Exito: false,
                Mensaje: $"Validacion fallida: {ex.Message}",
                Recurso: null);
        }
    }

    private static Guid RequireTenant(ITenantContext tenant)
        => tenant.CurrentTenantId
           ?? throw new InvalidOperationException(
               "No hay copropiedad activa en el contexto. El token del agente debe incluir el claim tenant_id.");
}

/// <summary>Resultado uniforme de una tool de creacion MCP.</summary>
public sealed record ResultadoCreacionMcp(
    [property: Description("True si fue una simulacion (no se persistio nada).")] bool DryRun,
    [property: Description("True si la validacion/creacion fue exitosa.")] bool Exito,
    [property: Description("Mensaje legible del resultado.")] string Mensaje,
    [property: Description("El recurso creado (o el que se habria creado en dry-run). Null si fallo la validacion.")] object? Recurso);
