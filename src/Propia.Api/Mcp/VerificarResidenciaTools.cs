using System.ComponentModel;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Tool MCP de PORTERIA: verifica si el numero de WhatsApp REAL desde el que escribe el contacto
/// pertenece a un propietario o residente de una unidad. Corre dentro del request /mcp autenticado:
/// el tenant/RLS lo fija el TenantMiddleware (del JWT del usuario en el playground, o del token de
/// servicio del dispatcher en el chat real). El telefono NO es un argumento del LLM: se toma de
/// IAgentCallContext, que el AgentCallContextMiddleware llena desde el header X-Contact-Phone que pone
/// el McpGateway a partir de conv.ContactPhone. Anti-suplantacion: que el usuario "diga" otro numero
/// no sirve; se verifica contra el numero real desde el que escribe.
///
/// La resolucion telefono -> persona + unidades + rol vive en AgentContactoHelper y la comparten
/// tambien crear_pqr y estado_pqr; esta tool es solo el formateador de la verificacion.
/// </summary>
[McpServerToolType]
public sealed class VerificarResidenciaTools
{
    /// <summary>Resultado de la verificacion de residencia.</summary>
    public sealed class VerificacionResidenciaDto
    {
        /// <summary>El numero real figura vinculado a alguna unidad (cualquier rol).</summary>
        [JsonPropertyName("encontrado")] public bool Encontrado { get; init; }

        /// <summary>El numero corresponde a un PROPIETARIO o RESIDENTE (Familiar/arrendatario/apoderado NO cuentan).</summary>
        [JsonPropertyName("es_residente")] public bool EsResidente { get; init; }

        /// <summary>Nombre de la persona/empresa a la que pertenece el numero (mejor coincidencia).</summary>
        [JsonPropertyName("nombre")] public string? Nombre { get; init; }

        /// <summary>Rol del vinculo (Propietario, Residente, Familiar, Arrendatario, Apoderado).</summary>
        [JsonPropertyName("rol")] public string? Rol { get; init; }

        /// <summary>La unidad que se logro resolver del texto indicado (null si no se pudo resolver).</summary>
        [JsonPropertyName("unidad_resuelta")] public string? UnidadResuelta { get; init; }

        /// <summary>El numero pertenece a un propietario/residente de la unidad indicada.</summary>
        [JsonPropertyName("coincide_unidad")] public bool CoincideUnidad { get; init; }

        /// <summary>Explicacion legible del resultado (el telefono va enmascarado).</summary>
        [JsonPropertyName("mensaje")] public string Mensaje { get; init; } = "";
    }

    [McpServerTool(Name = "verificar_residencia")]
    [Description("Verifica si el numero de WhatsApp REAL desde el que escribe el contacto corresponde a un PROPIETARIO o RESIDENTE de la unidad indicada. NO recibe el telefono como argumento: usa el numero real de la conversacion (anti-suplantacion: que el contacto 'diga' otro numero no sirve). Usalo en porteria para autenticar antes de entregar datos sensibles o autorizar accesos. Familiar/arrendatario/apoderado NO cuentan como residente.")]
    public static async Task<VerificacionResidenciaDto> VerificarResidencia(
        PropiaDbContext db,
        ITenantContext tenant,
        IAgentCallContext call,
        [Description("Unidad a verificar en texto libre: torre/bloque + apartamento/casa o su codigo (ej. 'Torre 1 Apto 302', 'A-203', 'Casa 15', '101').")] string unidad_privada,
        CancellationToken ct)
    {
        if (tenant.CurrentTenantId is null)
        {
            return Fail("No hay copropiedad activa en el contexto.");
        }

        var unidades = await AgentContactoHelper.CargarUnidadesAsync(db, ct);
        var resol = await AgentContactoHelper.ResolverContactoAsync(db, call.ContactPhone, unidades, ct);
        if (!resol.TelefonoPresente)
        {
            return Fail("No hay un telefono de contacto en la conversacion; no puedo verificar la residencia.");
        }

        var resolved = AgentContactoHelper.ResolverUnidad(unidades, unidad_privada);
        var last4 = AgentContactoHelper.Last4(resol.Tail!);

        if (!resol.Encontrado)
        {
            return new VerificacionResidenciaDto
            {
                Encontrado = false,
                EsResidente = false,
                UnidadResuelta = resolved?.Label,
                CoincideUnidad = false,
                Mensaje = $"El numero terminado en {last4} no figura vinculado a ninguna unidad de la copropiedad."
            };
        }

        var matches = resol.Matches;
        var residentMatches = resol.ResidentMatches;
        var esResidente = resol.EsResidente;

        // Mejor coincidencia para reportar nombre/rol: prioriza la unidad resuelta y el rol de residente.
        AgentContactoHelper.ContactoMatch? best = resolved is not null
            ? matches.Where(m => m.UnidadId == resolved.Id).OrderByDescending(m => AgentContactoHelper.EsRolResidente(m.Rol)).FirstOrDefault()
            : null;
        best ??= residentMatches.FirstOrDefault() ?? matches[0];

        var coincide = resolved is not null && matches.Any(m => m.UnidadId == resolved.Id && AgentContactoHelper.EsRolResidente(m.Rol));

        // Unidades reales del numero (para explicar cuando no coincide o no se resolvio la unidad).
        var unidadesTxt = string.Join(", ", (residentMatches.Count > 0 ? residentMatches : matches)
            .Select(m => m.UnidadLabel).Distinct());

        string mensaje;
        if (coincide)
        {
            mensaje = $"Verificado: {best!.Nombre} figura como {best.Rol} de {resolved!.Label} y escribe desde el numero registrado (termina en {last4}).";
        }
        else if (resolved is not null)
        {
            mensaje = esResidente
                ? $"El numero (termina en {last4}) corresponde a {best!.Nombre} ({best.Rol}) de {unidadesTxt}, NO de {resolved.Label}."
                : $"El numero (termina en {last4}) corresponde a {best!.Nombre} con rol {best.Rol} en {unidadesTxt}, que no cuenta como propietario/residente.";
        }
        else
        {
            mensaje = $"El numero (termina en {last4}) corresponde a {best!.Nombre} ({best.Rol}) de {unidadesTxt}. No pude resolver la unidad '{unidad_privada}' que mencionaste; confirma el dato de la unidad.";
        }

        return new VerificacionResidenciaDto
        {
            Encontrado = true,
            EsResidente = esResidente,
            Nombre = best!.Nombre,
            Rol = best.Rol.ToString(),
            UnidadResuelta = resolved?.Label,
            CoincideUnidad = coincide,
            Mensaje = mensaje
        };
    }

    private static VerificacionResidenciaDto Fail(string mensaje)
        => new() { Encontrado = false, EsResidente = false, CoincideUnidad = false, Mensaje = mensaje };
}
