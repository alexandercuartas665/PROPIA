using System.ComponentModel;
using ModelContextProtocol.Server;
using Propia.Application.Servicios;
using Propia.Application.ServiciosPublicos;

namespace Propia.Api.Mcp;

/// <summary>
/// Capa MCP del modulo de Servicios (Finanzas - Servicios y contratos + Servicios publicos 2.17)
/// - tools de CONSULTA (solo lectura). El tenant lo fija el TenantMiddleware (RLS) a partir del
/// JWT del agente: solo ve los servicios/cuentas de la copropiedad activa. Pensadas para que el
/// Agente Documental ubique la cuenta o servicio correcto antes de proponer un registro.
/// </summary>
[McpServerToolType]
public sealed class ServiciosConsultaTools
{
    [McpServerTool(Name = "servicios_listar")]
    [Description("Lista los servicios contratados de la copropiedad activa (tipo, ejecutor, costos, estado, cantidad de contratos). Util para ubicar un servicio existente antes de crear uno nuevo.")]
    public static async Task<IReadOnlyList<ServicioDto>> ListarServicios(
        IServiciosService svc, CancellationToken ct)
        => await svc.ListarAsync(ct);

    [McpServerTool(Name = "servicios_obtener")]
    [Description("Obtiene el detalle 360 de un servicio por su identificador (Guid): datos, contactos, adjuntos, contratos y alertas.")]
    public static async Task<ServicioDetalleDto?> ObtenerServicio(
        IServiciosService svc,
        [Description("Identificador (Guid) del servicio.")] Guid servicioId,
        CancellationToken ct)
        => await svc.GetAsync(servicioId, ct);

    [McpServerTool(Name = "serviciospublicos_listar_cuentas")]
    [Description("Lista las cuentas de servicios publicos de la copropiedad activa (energia, agua, gas, internet) con su alias, prestador, numero de cuenta, unidad de medida y ultimo valor. El agente la usa para encontrar la cuenta que corresponde a un recibo (por prestador o numero de cuenta) antes de registrar el consumo.")]
    public static async Task<IReadOnlyList<CuentaServicioDto>> ListarCuentasServiciosPublicos(
        IServiciosPublicosService svc, CancellationToken ct)
        => await svc.ListCuentasAsync(ct);

    [McpServerTool(Name = "serviciospublicos_obtener_cuenta")]
    [Description("Obtiene el detalle de una cuenta de servicio publico (Guid): datos, historico de registros de consumo y reclamaciones. Util para conocer el ultimo periodo registrado y no duplicar.")]
    public static async Task<CuentaServicioDetalleDto?> ObtenerCuentaServicioPublico(
        IServiciosPublicosService svc,
        [Description("Identificador (Guid) de la cuenta de servicio publico.")] Guid cuentaId,
        CancellationToken ct)
        => await svc.GetCuentaAsync(cuentaId, ct);

    [McpServerTool(Name = "serviciospublicos_resumen")]
    [Description("Resumen de KPIs de servicios publicos de la copropiedad activa: cuentas activas, gasto del ultimo mes, cuentas con alerta y reclamaciones abiertas.")]
    public static async Task<ServiciosPublicosResumenDto> ResumenServiciosPublicos(
        IServiciosPublicosService svc, CancellationToken ct)
        => await svc.GetResumenAsync(ct);
}
