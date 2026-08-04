using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Propia.Application.Common;
using Propia.Domain.Enums;
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
        var tail = PhoneTail(call.ContactPhone);
        if (tail is null)
        {
            return Fail("No hay un telefono de contacto en la conversacion; no puedo verificar la residencia.");
        }
        if (tenant.CurrentTenantId is null)
        {
            return Fail("No hay copropiedad activa en el contexto.");
        }

        // 1) Unidades de la copropiedad (RLS) para resolver el texto libre a una unidad concreta.
        var unidades = await db.UnidadesPrivadas.AsNoTracking()
            .Select(u => new UnitRow(u.Id, u.Numero, u.Torre != null ? u.Torre.Nombre : null))
            .ToListAsync(ct);
        var unidadLabel = unidades.ToDictionary(u => u.Id, u => Label(u));
        var resolved = ResolveUnit(unidades, unidad_privada);

        // 2) Vinculos persona/empresa <-> unidad con su rol.
        var vinculos = await db.UnidadPersonas.AsNoTracking()
            .Select(v => new { v.UnidadId, v.EntidadTipo, v.PersonaId, v.EmpresaId, v.Rol })
            .ToListAsync(ct);
        if (vinculos.Count == 0)
        {
            return NotFound(tail, resolved?.Label, "No hay personas vinculadas a unidades en esta copropiedad.");
        }

        var personaIds = vinculos.Where(v => v.PersonaId != null).Select(v => v.PersonaId!.Value).Distinct().ToList();
        var empresaIds = vinculos.Where(v => v.EmpresaId != null).Select(v => v.EmpresaId!.Value).Distinct().ToList();

        // Personas/Empresas son GLOBALES (sin RLS); se consultan por su Id (que salio del vinculo tenant-scoped).
        var personas = await db.Personas.AsNoTracking()
            .Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombres, p.Apellidos, p.Telefono })
            .ToListAsync(ct);
        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => empresaIds.Contains(e.Id))
            .Select(e => new { e.Id, e.RazonSocial, e.Telefono })
            .ToListAsync(ct);

        // Contactos adicionales del directorio (telefono/whatsapp) - tenant-scoped (RLS).
        var contactos = await db.DirectorioContactos.AsNoTracking()
            .Where(c => c.Activo && (c.Tipo == TipoContacto.Telefono || c.Tipo == TipoContacto.Whatsapp))
            .Select(c => new { c.EntidadTipo, c.EntidadId, c.Valor })
            .ToListAsync(ct);

        // 3) Mapa entidad -> conjunto de "colas" de telefono (ultimos 10 digitos, tolera prefijo pais).
        var phonesByEntity = new Dictionary<(EntidadDirectorio, Guid), HashSet<string>>();
        void AddPhone(EntidadDirectorio tipo, Guid id, string? raw)
        {
            var t = PhoneTail(raw);
            if (t is null) { return; }
            var key = (tipo, id);
            if (!phonesByEntity.TryGetValue(key, out var set)) { set = new HashSet<string>(); phonesByEntity[key] = set; }
            set.Add(t);
        }
        foreach (var p in personas) { AddPhone(EntidadDirectorio.Persona, p.Id, p.Telefono); }
        foreach (var e in empresas) { AddPhone(EntidadDirectorio.Empresa, e.Id, e.Telefono); }
        foreach (var c in contactos) { AddPhone(c.EntidadTipo, c.EntidadId, c.Valor); }

        string? NameFor(EntidadDirectorio tipo, Guid? personaId, Guid? empresaId)
        {
            if (tipo == EntidadDirectorio.Persona && personaId is Guid pid)
            {
                var per = personas.FirstOrDefault(x => x.Id == pid);
                return per is null ? null : $"{per.Nombres} {per.Apellidos}".Trim();
            }
            if (tipo == EntidadDirectorio.Empresa && empresaId is Guid eid)
            {
                return empresas.FirstOrDefault(x => x.Id == eid)?.RazonSocial;
            }
            return null;
        }

        // 4) Coincidencias: vinculos cuya entidad tiene un telefono que coincide con el numero real.
        var matches = new List<Match>();
        foreach (var v in vinculos)
        {
            Guid? entId = v.EntidadTipo == EntidadDirectorio.Persona ? v.PersonaId : v.EmpresaId;
            if (entId is null) { continue; }
            if (!phonesByEntity.TryGetValue((v.EntidadTipo, entId.Value), out var set)) { continue; }
            if (!set.Contains(tail)) { continue; }
            matches.Add(new Match(v.UnidadId, v.Rol, NameFor(v.EntidadTipo, v.PersonaId, v.EmpresaId)));
        }

        if (matches.Count == 0)
        {
            return NotFound(tail, resolved?.Label,
                $"El numero terminado en {Last4(tail)} no figura vinculado a ninguna unidad de la copropiedad.");
        }

        var residentMatches = matches.Where(m => EsRolResidente(m.Rol)).ToList();
        var esResidente = residentMatches.Count > 0;

        // Mejor coincidencia para reportar nombre/rol: prioriza la unidad resuelta y el rol de residente.
        Match? best = resolved is not null
            ? matches.Where(m => m.UnidadId == resolved.Id).OrderByDescending(m => EsRolResidente(m.Rol)).FirstOrDefault()
            : null;
        best ??= residentMatches.FirstOrDefault() ?? matches[0];

        var coincide = resolved is not null && matches.Any(m => m.UnidadId == resolved.Id && EsRolResidente(m.Rol));

        // Unidades reales del numero (para explicar cuando no coincide o no se resolvio la unidad).
        var unidadesDelNumero = (residentMatches.Count > 0 ? residentMatches : matches)
            .Select(m => unidadLabel.TryGetValue(m.UnidadId, out var lbl) ? lbl : "unidad desconocida")
            .Distinct()
            .ToList();
        var unidadesTxt = string.Join(", ", unidadesDelNumero);

        string mensaje;
        if (coincide)
        {
            mensaje = $"Verificado: {best!.Nombre} figura como {best.Rol} de {resolved!.Label} y escribe desde el numero registrado (termina en {Last4(tail)}).";
        }
        else if (resolved is not null)
        {
            mensaje = esResidente
                ? $"El numero (termina en {Last4(tail)}) corresponde a {best!.Nombre} ({best.Rol}) de {unidadesTxt}, NO de {resolved.Label}."
                : $"El numero (termina en {Last4(tail)}) corresponde a {best!.Nombre} con rol {best.Rol} en {unidadesTxt}, que no cuenta como propietario/residente.";
        }
        else
        {
            mensaje = $"El numero (termina en {Last4(tail)}) corresponde a {best!.Nombre} ({best.Rol}) de {unidadesTxt}. No pude resolver la unidad '{unidad_privada}' que mencionaste; confirma el dato de la unidad.";
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

    private sealed record UnitRow(Guid Id, string Numero, string? Torre);
    private sealed record ResolvedUnit(Guid Id, string Label);
    private sealed record Match(Guid UnidadId, RolUnidadPersona Rol, string? Nombre);

    private static bool EsRolResidente(RolUnidadPersona r)
        => r == RolUnidadPersona.Propietario || r == RolUnidadPersona.Residente;

    private static string Label(UnitRow u)
        => string.IsNullOrWhiteSpace(u.Torre) ? u.Numero : $"{u.Torre} - {u.Numero}";

    private static VerificacionResidenciaDto Fail(string mensaje)
        => new() { Encontrado = false, EsResidente = false, CoincideUnidad = false, Mensaje = mensaje };

    private static VerificacionResidenciaDto NotFound(string tail, string? unidadResuelta, string mensaje)
        => new() { Encontrado = false, EsResidente = false, UnidadResuelta = unidadResuelta, CoincideUnidad = false, Mensaje = mensaje };

    /// <summary>Ultimos 10 digitos del telefono (quita +, espacios, guiones y tolera el prefijo pais 57). null si no hay digitos.</summary>
    private static string? PhoneTail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return null; }
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) { return null; }
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    private static string Last4(string tail)
        => tail.Length >= 4 ? tail[^4..] : tail;

    /// <summary>
    /// Resuelve la unidad indicada en texto libre. Estrategia: (1) match exacto por "torre+numero" o
    /// por numero; (2) el texto contiene el numero de la unidad (unico); (3) desambigua por torre.
    /// Devuelve null si no logra una resolucion inequivoca (mejor no verificar que verificar mal).
    /// </summary>
    private static ResolvedUnit? ResolveUnit(IReadOnlyList<UnitRow> units, string input)
    {
        static string Norm(string? s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var q = Norm(input);
        if (q.Length == 0 || units.Count == 0) { return null; }

        var exact = units.FirstOrDefault(u => Norm($"{u.Torre}{u.Numero}") == q || Norm(u.Numero) == q);
        if (exact is not null) { return new ResolvedUnit(exact.Id, Label(exact)); }

        var byNumero = units.Where(u => Norm(u.Numero).Length > 0 && q.Contains(Norm(u.Numero))).ToList();
        if (byNumero.Count == 1) { return new ResolvedUnit(byNumero[0].Id, Label(byNumero[0])); }
        if (byNumero.Count > 1)
        {
            var conTorre = byNumero.Where(u => !string.IsNullOrWhiteSpace(u.Torre) && q.Contains(Norm(u.Torre))).ToList();
            if (conTorre.Count == 1) { return new ResolvedUnit(conTorre[0].Id, Label(conTorre[0])); }
        }
        return null;
    }
}
