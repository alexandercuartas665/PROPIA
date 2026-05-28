using System.ComponentModel;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;

namespace Propia.Api.Mcp;

/// <summary>
/// Capa MCP de 2.3 Mi Copropiedad - tools de CONSULTA (read-only).
///
/// Cada tool corre dentro del request HTTP autenticado del agente: el JWT trae el
/// claim tenant_id, el TenantMiddleware lo setea en ITenantContext y RLS aisla los
/// datos. Resultado: el agente NUNCA ve mas que la copropiedad activa del usuario y
/// hereda exactamente sus permisos (RBAC). No hay bypass: si el usuario no puede ver
/// algo, el agente tampoco.
///
/// Todas las tools son estaticas; el SDK resuelve IMiCopropiedadService e ITenantContext
/// desde el scope DI de la request. Devuelven los DTOs del modulo tal cual (serializados
/// a JSON con enums como string, ver Program.cs).
/// </summary>
[McpServerToolType]
public sealed class MiCopropiedadConsultaTools
{
    [McpServerTool(Name = "micopropiedad_resumen")]
    [Description("Resumen de la copropiedad activa: identidad, conteos (torres, unidades, zonas, equipos, contratos, consejo), suma de coeficientes y porcentaje de completitud por seccion. Util como primer vistazo del estado de la ficha.")]
    public static async Task<ResumenMiCopropiedadDto> Resumen(
        IMiCopropiedadService svc, ITenantContext tenant, CancellationToken ct)
    {
        var tid = RequireTenant(tenant);
        return await svc.GetResumenAsync(tid, ct)
            ?? throw new InvalidOperationException("No existe la copropiedad activa.");
    }

    [McpServerTool(Name = "micopropiedad_listar_torres")]
    [Description("Lista las torres/agrupaciones de la copropiedad activa, con su cantidad de unidades.")]
    public static async Task<IReadOnlyList<TorreDto>> ListarTorres(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListTorresAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_unidades")]
    [Description("Lista todas las unidades privadas (apartamentos, locales, parqueaderos, etc.) de la copropiedad activa, con tipo, torre, piso, coeficiente, area y estado.")]
    public static async Task<IReadOnlyList<UnidadDto>> ListarUnidades(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListUnidadesAsync(ct);

    [McpServerTool(Name = "micopropiedad_obtener_unidad")]
    [Description("Obtiene el detalle de una unidad por su identificador (Guid). Devuelve null si no existe en la copropiedad activa.")]
    public static async Task<UnidadDto?> ObtenerUnidad(
        IMiCopropiedadService svc,
        [Description("Identificador (Guid) de la unidad.")] Guid unidadId,
        CancellationToken ct)
        => await svc.ObtenerUnidadAsync(unidadId, ct);

    [McpServerTool(Name = "micopropiedad_listar_consejo")]
    [Description("Lista los miembros del Consejo de Administracion de la copropiedad activa, con su cargo y vigencia.")]
    public static async Task<IReadOnlyList<MiembroConsejoDto>> ListarConsejo(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListMiembrosConsejoAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_contratos")]
    [Description("Lista los contratos de servicios (vigilancia, aseo, ascensores, etc.) de la copropiedad activa. Cada contrato incluye estado, dias para vencer y un flag de alerta de vencimiento.")]
    public static async Task<IReadOnlyList<ContratoServicioDto>> ListarContratos(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListContratosAsync(ct);

    [McpServerTool(Name = "micopropiedad_contratos_por_vencer")]
    [Description("Devuelve solo los contratos con alerta de vencimiento activa (estan dentro de su ventana de anticipacion o ya vencidos). Util para responder a que contratos estan por vencer.")]
    public static async Task<IReadOnlyList<ContratoServicioDto>> ContratosPorVencer(
        IMiCopropiedadService svc, CancellationToken ct)
    {
        var todos = await svc.ListContratosAsync(ct);
        return todos.Where(c => c.AlertaVencimiento).ToList();
    }

    [McpServerTool(Name = "micopropiedad_listar_zonas")]
    [Description("Lista las zonas comunes de la copropiedad activa (salon social, piscina, gimnasio, etc.) con su categoria, estado operativo y reglas de reserva.")]
    public static async Task<IReadOnlyList<ZonaComunDto>> ListarZonas(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListZonasComunesAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_equipos")]
    [Description("Lista los equipos/activos de la copropiedad activa (bombas, ascensores, plantas electricas, etc.) con su categoria, ubicacion, garantia y estado operativo.")]
    public static async Task<IReadOnlyList<EquipoActivoDto>> ListarEquipos(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListEquiposAsync(ct);

    [McpServerTool(Name = "micopropiedad_finanzas")]
    [Description("Parametros financieros de la copropiedad activa: moneda, dia de corte, tasa de mora (legal o fija), periodo de gracia y si ya fueron configurados. NO incluye saldos; el resumen financiero en tiempo real lo orquesta el modulo de cartera/presupuesto.")]
    public static async Task<FinanzasParametrosDto> Finanzas(
        IMiCopropiedadService svc, ITenantContext tenant, CancellationToken ct)
    {
        var tid = RequireTenant(tenant);
        return await svc.GetFinanzasParametrosAsync(tid, ct);
    }

    [McpServerTool(Name = "micopropiedad_bitacora")]
    [Description("Bitacora de cambios recientes de la copropiedad activa (cambios de moneda, dia de corte, coeficientes, estados de zonas/equipos, etc.). Util para auditoria y para entender 'que cambio ultimamente'.")]
    public static async Task<IReadOnlyList<BitacoraEntradaDto>> Bitacora(
        IMiCopropiedadService svc,
        [Description("Maximo de entradas a devolver (por defecto 50).")] int limit,
        CancellationToken ct)
        => await svc.ListBitacoraAsync(limit <= 0 ? 50 : limit, ct);

    [McpServerTool(Name = "micopropiedad_identidad")]
    [Description("Identidad de la copropiedad activa: nombre, NIT, direccion, ciudad, tipo, estrato, datos registrales (reglamento PH, notaria, matricula) y contacto.")]
    public static async Task<IdentidadDto> Identidad(
        IMiCopropiedadService svc, ITenantContext tenant, CancellationToken ct)
    {
        var tid = RequireTenant(tenant);
        var r = await svc.GetResumenAsync(tid, ct)
            ?? throw new InvalidOperationException("No existe la copropiedad activa.");
        return r.Identidad;
    }

    [McpServerTool(Name = "micopropiedad_listar_equipo_trabajo")]
    [Description("Lista el equipo de trabajo de la copropiedad (administrador, contador, vigilancia, aseo, etc.): persona, rol, tipo de vinculacion y vigencia. (Seccion 3)")]
    public static async Task<IReadOnlyList<MiembroEquipoDto>> ListarEquipoTrabajo(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListEquipoAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_comites")]
    [Description("Lista los comites de la copropiedad (convivencia, deportes, etc.) con su fecha de conformacion y cantidad de miembros. (Seccion 4 - Gobierno)")]
    public static async Task<IReadOnlyList<ComiteDto>> ListarComites(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListComitesAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_miembros_comite")]
    [Description("Lista los miembros de un comite por su identificador (Guid).")]
    public static async Task<IReadOnlyList<ComiteMiembroDto>> ListarMiembrosComite(
        IMiCopropiedadService svc,
        [Description("Identificador (Guid) del comite.")] Guid comiteId,
        CancellationToken ct)
        => await svc.ListMiembrosComiteAsync(comiteId, ct);

    [McpServerTool(Name = "micopropiedad_revisor_fiscal")]
    [Description("Devuelve el Revisor Fiscal activo de la copropiedad (persona, tarjeta profesional, fecha de posesion) o null si no hay. (Seccion 4 - Gobierno)")]
    public static async Task<RevisorFiscalDto?> RevisorFiscal(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.GetRevisorFiscalActivoAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_tipos_coeficiente")]
    [Description("Lista los tipos de coeficiente de propiedad de la copropiedad (RN-02), con si es principal y la suma actual de cada uno. (Seccion 2)")]
    public static async Task<IReadOnlyList<TipoCoeficienteDto>> ListarTiposCoeficiente(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListTiposCoeficienteAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_coeficientes_unidad")]
    [Description("Lista los coeficientes (por tipo) de una unidad por su identificador (Guid).")]
    public static async Task<IReadOnlyList<UnidadCoeficienteDto>> ListarCoeficientesUnidad(
        IMiCopropiedadService svc,
        [Description("Identificador (Guid) de la unidad.")] Guid unidadId,
        CancellationToken ct)
        => await svc.ListCoeficientesUnidadAsync(unidadId, ct);

    [McpServerTool(Name = "micopropiedad_listar_vinculos_unidad")]
    [Description("Lista las unidades asociadas (parqueaderos, depositos, etc.) vinculadas a una unidad principal por su identificador (Guid). (RN-09)")]
    public static async Task<IReadOnlyList<UnidadVinculoDto>> ListarVinculosUnidad(
        IMiCopropiedadService svc,
        [Description("Identificador (Guid) de la unidad principal.")] Guid unidadPrincipalId,
        CancellationToken ct)
        => await svc.ListVinculosAsync(unidadPrincipalId, ct);

    [McpServerTool(Name = "micopropiedad_listar_tipos_unidad")]
    [Description("Lista los tipos de unidad personalizados definidos en la copropiedad (ademas de los estandar como apartamento/local).")]
    public static async Task<IReadOnlyList<TipoUnidadCustomDto>> ListarTiposUnidad(
        IMiCopropiedadService svc, CancellationToken ct)
        => await svc.ListTiposUnidadCustomAsync(ct);

    [McpServerTool(Name = "micopropiedad_listar_monedas")]
    [Description("Catalogo de monedas (ISO 4217) disponibles para configurar la copropiedad (COP, USD, EUR, etc.).")]
    public static IReadOnlyList<MonedaDto> ListarMonedas(IMiCopropiedadService svc)
        => svc.ListMonedas();

    // ---------- Helpers ----------

    /// <summary>
    /// Garantiza que haya una copropiedad activa en el contexto. Si el JWT no trae
    /// tenant_id, el agente no esta operando sobre ninguna copropiedad y la tool falla
    /// con un mensaje claro (en vez de devolver datos vacios silenciosamente).
    /// </summary>
    private static Guid RequireTenant(ITenantContext tenant)
        => tenant.CurrentTenantId
           ?? throw new InvalidOperationException(
               "No hay copropiedad activa en el contexto. El token del agente debe incluir el claim tenant_id.");
}
