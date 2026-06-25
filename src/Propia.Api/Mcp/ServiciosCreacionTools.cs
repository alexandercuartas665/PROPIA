using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Application.Servicios;
using Propia.Application.ServiciosPublicos;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Capa MCP del modulo de Servicios - tools de CREACION/ACCION (mutaciones). Mismo patron de
/// seguridad que Mi Copropiedad:
/// 1. dryRun=true por defecto -> valida y simula SIN persistir (rollback). El agente debe llamar
///    explicitamente con dryRun=false para confirmar (UX de "proponer y confirmar").
/// 2. Todo corre en una transaccion sobre el PropiaDbContext del request; el tenant lo fija el
///    TenantMiddleware (RLS) y los permisos los hereda del JWT del usuario.
/// 3. Al confirmar se registra en la bitacora de la copropiedad marcado "(agente documental)".
/// 4. Errores de validacion del dominio -> Exito=false con el mensaje (el agente corrige y reintenta).
///
/// Estas tools son la base del Agente Documental: tras leer un recibo/factura/poliza con OCR,
/// propone el registro de consumo o el alta del servicio y el usuario confirma.
/// </summary>
[McpServerToolType]
public sealed class ServiciosCreacionTools
{
    [McpServerTool(Name = "serviciospublicos_registrar_consumo")]
    [Description("Registra el consumo de un periodo en una cuenta de servicio publico (el caso principal de un recibo: energia, agua, gas, internet). Requiere el Guid de la cuenta (obtenlo con serviciospublicos_listar_cuentas) y los datos del periodo: anio, mes (1-12), consumo (kWh/m3/etc), valor a pagar y estado (Confirmado o Estimado). Por defecto es dry-run; pasa dryRun=false para confirmar el registro.")]
    public static Task<ResultadoCreacionMcp> RegistrarConsumo(
        [Description("Identificador (Guid) de la cuenta de servicio publico a la que pertenece el recibo.")] Guid cuentaId,
        [Description("Datos del periodo: Anio, Mes (1-12), Consumo, Valor, Estado (Confirmado|Estimado), NotaAdmin opcional.")] CrearRegistroConsumoRequest registro,
        IServiciosPublicosService sp, IMiCopropiedadService bitacora, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, bitacora, tenant, dryRun, "ServiciosPublicos",
            r => $"Consumo registrado por agente documental ({((RegistroConsumoDto)r!).PeriodoLabel}: {((RegistroConsumoDto)r!).Consumo}, valor {((RegistroConsumoDto)r!).Valor}).",
            async () => await sp.AgregarRegistroAsync(cuentaId, registro, ct), ct);

    [McpServerTool(Name = "serviciospublicos_crear_cuenta")]
    [Description("Crea una cuenta de servicio publico (energia, agua, gas, internet) cuando el recibo es de un prestador que aun no tiene cuenta. Requiere tipo, alias, prestador y opcionalmente numero de cuenta, metodo de pago, unidad de medida y umbral de alerta (%). Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearCuentaServicioPublico(
        [Description("Datos de la cuenta: Tipo (Electricidad|Agua|Gas|Internet|Otros), Alias, Prestador, NumeroCuenta?, MetodoPago?, UnidadMedida?, UmbralAlertaPct.")] CrearCuentaServicioRequest cuenta,
        IServiciosPublicosService sp, IMiCopropiedadService bitacora, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, bitacora, tenant, dryRun, "ServiciosPublicos",
            r => $"Cuenta de servicio publico '{((CuentaServicioDto)r!).Alias}' creada (agente documental).",
            async () => await sp.CrearCuentaAsync(cuenta, ct), ct);

    [McpServerTool(Name = "serviciospublicos_agregar_reclamacion")]
    [Description("Radica una reclamacion sobre una cuenta de servicio publico (ej. cobro indebido detectado en un recibo). Requiere el Guid de la cuenta, el motivo y opcionalmente radicado y descripcion. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> AgregarReclamacion(
        [Description("Identificador (Guid) de la cuenta de servicio publico.")] Guid cuentaId,
        [Description("Datos de la reclamacion: Motivo, Radicado?, Descripcion?.")] CrearReclamacionRequest reclamacion,
        IServiciosPublicosService sp, IMiCopropiedadService bitacora, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, bitacora, tenant, dryRun, "ServiciosPublicos",
            r => $"Reclamacion radicada por agente documental: {((ReclamacionServicioDto)r!).Motivo}.",
            async () => await sp.AgregarReclamacionAsync(cuentaId, reclamacion, ct), ct);

    [McpServerTool(Name = "servicios_crear")]
    [Description("Crea un servicio contratado de la copropiedad (incluye polizas de seguro: usa Tipo=SeguroPH). Util cuando una factura o poliza corresponde a un servicio que aun no existe. Requiere tipo, nombre y opcionalmente ejecutor (tercero del Directorio), costo mensual y anual. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> CrearServicio(
        [Description("Datos del servicio: Tipo (Aseo|Seguridad|Mantenimiento|...|SeguroPH|...), Nombre, Descripcion?, EjecutorPersonaId?, EjecutorEmpresaId?, EjecutorNombre?, CostoMensual?, CostoAnual?.")] CrearServicioRequest servicio,
        IServiciosService svc, IMiCopropiedadService bitacora, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, bitacora, tenant, dryRun, "Servicios",
            r => $"Servicio '{((ServicioDto)r!).Nombre}' creado (agente documental).",
            async () => await svc.CrearAsync(servicio, ct), ct);

    [McpServerTool(Name = "servicios_agregar_alerta")]
    [Description("Agrega una alerta a un servicio (ej. un documento indica un vencimiento o una incidencia). Requiere el Guid del servicio, el titulo y la severidad (Info|Advertencia|Critica). La alerta sale en la banda del dashboard. Por defecto es dry-run; pasa dryRun=false para confirmar.")]
    public static Task<ResultadoCreacionMcp> AgregarAlertaServicio(
        [Description("Identificador (Guid) del servicio.")] Guid servicioId,
        [Description("Datos de la alerta: Titulo, Descripcion?, Severidad (Info|Advertencia|Critica).")] AgregarAlertaServicioRequest alerta,
        IServiciosService svc, IMiCopropiedadService bitacora, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) solo valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => EjecutarAsync(db, bitacora, tenant, dryRun, "Servicios",
            r => $"Alerta '{((AlertaServicioDto)r!).Titulo}' agregada a un servicio (agente documental).",
            async () => await svc.AgregarAlertaAsync(servicioId, alerta, ct), ct);

    // ---------- Motor comun (mismo patron que MiCopropiedadCreacionTools) ----------

    private static async Task<ResultadoCreacionMcp> EjecutarAsync<T>(
        PropiaDbContext db, IMiCopropiedadService bitacora, ITenantContext tenant,
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

            await bitacora.RegistrarBitacoraAsync(categoriaBitacora, descripcionBitacora(recurso), ct);
            await tx.CommitAsync(ct);
            return new ResultadoCreacionMcp(
                DryRun: false, Exito: true,
                Mensaje: "Guardado y registrado en la bitacora.",
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
