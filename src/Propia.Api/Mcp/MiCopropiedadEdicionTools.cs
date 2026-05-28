using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Capa MCP de 2.3 - tools de EDICION (mutaciones puntuales y seguras) sobre las 8 secciones.
///
/// Mismo patron de seguridad que creacion:
/// - dryRun=true por defecto: valida dentro de una transaccion y hace rollback (no persiste).
///   El agente debe pasar dryRun=false para confirmar el cambio.
/// - Hereda tenant (RLS) y permisos del JWT del usuario.
/// - Solo ediciones ACOTADAS (estado, parametros, coeficiente, contacto) - NO borrados, NO
///   reemplazos ciegos: la identidad se actualiza por MERGE (conserva los campos no provistos).
/// - Bitacora: finanzas/estado-zona/estado-equipo/coeficiente ya se auto-registran en el
///   servicio; aqui solo registramos las que NO lo hacen (contrato, identidad).
/// </summary>
[McpServerToolType]
public sealed class MiCopropiedadEdicionTools
{
    [McpServerTool(Name = "micopropiedad_actualizar_finanzas")]
    [Description("Actualiza los parametros financieros de la copropiedad activa (moneda, dia de corte, tasa de mora legal/fija, periodo de gracia). Provee el set COMPLETO. Dry-run por defecto; pasa dryRun=false para confirmar. (Seccion 8)")]
    public static Task<ResultadoCreacionMcp> ActualizarFinanzas(
        [Description("Parametros financieros completos a guardar.")] ActualizarFinanzasRequest finanzas,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: null, bitacoraDesc: null,
            accion: tid => svc.ActualizarFinanzasAsync(tid, finanzas, ct), ct);

    [McpServerTool(Name = "micopropiedad_actualizar_contrato")]
    [Description("Actualiza el estado (Vigente/EnRenovacion/Vencido) y los dias de anticipacion de alerta de un contrato de servicios. Dry-run por defecto. (Seccion 5)")]
    public static Task<ResultadoCreacionMcp> ActualizarContrato(
        [Description("Identificador (Guid) del contrato.")] Guid contratoId,
        [Description("Nuevo estado y dias de anticipacion de alerta.")] ActualizarContratoRequest cambios,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: "Servicio",
            bitacoraDesc: $"Contrato actualizado: estado {cambios.Estado}, alerta {cambios.DiasAnticipacionAlerta} dias (agente MCP).",
            accion: async _ => await svc.ActualizarContratoAsync(contratoId, cambios, ct), ct);

    [McpServerTool(Name = "micopropiedad_cambiar_estado_zona")]
    [Description("Cambia el estado operativo de una zona comun (Disponible/EnMantenimiento/Inhabilitada). Dry-run por defecto. (Seccion 6)")]
    public static Task<ResultadoCreacionMcp> CambiarEstadoZona(
        [Description("Identificador (Guid) de la zona comun.")] Guid zonaId,
        [Description("Nuevo estado operativo de la zona.")] EstadoZonaComunMantenimiento estado,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: null, bitacoraDesc: null,
            accion: async _ => await svc.CambiarEstadoZonaAsync(zonaId, new CambiarEstadoZonaRequest(estado), ct), ct);

    [McpServerTool(Name = "micopropiedad_cambiar_estado_equipo")]
    [Description("Cambia el estado operativo de un equipo/activo (Operativo/EnMantenimiento/FueraDeServicio/etc.). Dry-run por defecto. (Seccion 7)")]
    public static Task<ResultadoCreacionMcp> CambiarEstadoEquipo(
        [Description("Identificador (Guid) del equipo.")] Guid equipoId,
        [Description("Nuevo estado operativo del equipo.")] EstadoEquipoActivo estado,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: null, bitacoraDesc: null,
            accion: async _ => await svc.CambiarEstadoEquipoAsync(equipoId, new CambiarEstadoEquipoRequest(estado), ct), ct);

    [McpServerTool(Name = "micopropiedad_set_coeficiente_unidad")]
    [Description("Asigna el valor de un tipo de coeficiente a una unidad. Dry-run por defecto. (Seccion 2 - RN-02)")]
    public static Task<ResultadoCreacionMcp> SetCoeficienteUnidad(
        [Description("Identificador (Guid) de la unidad.")] Guid unidadId,
        [Description("Identificador (Guid) del tipo de coeficiente.")] Guid tipoCoeficienteId,
        [Description("Valor del coeficiente a asignar (>= 0).")] decimal valor,
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: null, bitacoraDesc: null,
            accion: async _ => await svc.SetCoeficienteUnidadAsync(unidadId, new SetCoeficienteUnidadRequest(tipoCoeficienteId, valor), ct), ct);

    [McpServerTool(Name = "micopropiedad_actualizar_identidad")]
    [Description("Actualiza datos de identidad de la copropiedad por MERGE: solo cambia los campos que envies; el resto se conserva. Campos editables: nombre, nit, direccion, ciudad, departamento, descripcion, telefono y email de contacto. Dry-run por defecto. (Seccion 1)")]
    public static Task<ResultadoCreacionMcp> ActualizarIdentidad(
        IMiCopropiedadService svc, PropiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [Description("Nuevo nombre (opcional).")] string? nombre = null,
        [Description("Nuevo NIT (opcional).")] string? nit = null,
        [Description("Nueva direccion (opcional).")] string? direccion = null,
        [Description("Nueva ciudad (opcional).")] string? ciudad = null,
        [Description("Nuevo departamento (opcional).")] string? departamento = null,
        [Description("Nueva descripcion (opcional).")] string? descripcion = null,
        [Description("Nuevo telefono de contacto (opcional).")] string? telefonoContacto = null,
        [Description("Nuevo email de contacto (opcional).")] string? emailContacto = null,
        [Description("Si true (por defecto) valida sin persistir. Pasa false para guardar.")] bool dryRun = true)
        => RunAsync(db, svc, tenant, dryRun, bitacoraCategoria: "Identidad",
            bitacoraDesc: "Identidad actualizada (agente MCP).",
            accion: async tid =>
            {
                var r = await svc.GetResumenAsync(tid, ct)
                    ?? throw new InvalidOperationException("No existe la copropiedad activa.");
                var c = r.Identidad;
                var req = new ActualizarIdentidadRequest(
                    Nombre: nombre ?? c.Nombre,
                    Nit: nit ?? c.Nit,
                    DigitoVerificacion: c.DigitoVerificacion,
                    Direccion: direccion ?? c.Direccion,
                    Ciudad: ciudad ?? c.Ciudad,
                    Departamento: departamento ?? c.Departamento,
                    Tipo: c.Tipo,
                    Estrato: c.Estrato,
                    FotoFachadaUrl: c.FotoFachadaUrl,
                    LogoUrl: c.LogoUrl,
                    Descripcion: descripcion ?? c.Descripcion,
                    NumeroReglamentoPh: c.NumeroReglamentoPh,
                    NotariaRegistro: c.NotariaRegistro,
                    MatriculaInmobiliaria: c.MatriculaInmobiliaria,
                    LicenciaConstruccion: c.LicenciaConstruccion,
                    FechaConstitucion: c.FechaConstitucion,
                    LabelAgrupacion: c.LabelAgrupacion,
                    LabelPiso: c.LabelPiso,
                    TelefonoContacto: telefonoContacto ?? c.TelefonoContacto,
                    EmailContacto: emailContacto ?? c.EmailContacto);
                return await svc.ActualizarIdentidadAsync(tid, req, ct);
            }, ct);

    // ---------- Motor comun de edicion (dry-run via transaccion + rollback) ----------

    private static async Task<ResultadoCreacionMcp> RunAsync<T>(
        PropiaDbContext db, IMiCopropiedadService svc, ITenantContext tenant,
        bool dryRun, string? bitacoraCategoria, string? bitacoraDesc,
        Func<Guid, Task<T>> accion, CancellationToken ct)
    {
        var tid = tenant.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa en el contexto. El token debe incluir tenant_id.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var resultado = await accion(tid);

            // Metodos que devuelven bool=false => no encontrado / no aplicable.
            if (resultado is bool ok && !ok)
            {
                await tx.RollbackAsync(ct);
                return new ResultadoCreacionMcp(dryRun, false, "No se encontro el registro o no se pudo aplicar el cambio.", null);
            }

            if (dryRun)
            {
                await tx.RollbackAsync(ct);
                return new ResultadoCreacionMcp(
                    DryRun: true, Exito: true,
                    Mensaje: "Validacion OK. No se persistio nada (dry-run). Vuelve a llamar con dryRun=false para confirmar.",
                    Recurso: resultado);
            }

            if (bitacoraDesc is not null)
            {
                await svc.RegistrarBitacoraAsync(bitacoraCategoria ?? "Cambio", bitacoraDesc, ct);
            }
            await tx.CommitAsync(ct);
            return new ResultadoCreacionMcp(
                DryRun: false, Exito: true,
                Mensaje: "Cambio aplicado correctamente.",
                Recurso: resultado);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return new ResultadoCreacionMcp(dryRun, false, $"Validacion fallida: {ex.Message}", null);
        }
    }
}
