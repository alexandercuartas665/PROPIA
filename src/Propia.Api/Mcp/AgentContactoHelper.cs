using Microsoft.EntityFrameworkCore;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Api.Mcp;

/// <summary>
/// Helper compartido de las tools de PORTERIA: resuelve el numero de WhatsApp REAL del contacto
/// (IAgentCallContext.ContactPhone, NO un argumento del LLM) a la persona/empresa vinculada a una
/// unidad, con su rol, y resuelve texto libre de unidad a una unidad concreta. Extraido de
/// VerificarResidenciaTools para que verificar_residencia, crear_pqr y estado_pqr compartan
/// exactamente la misma logica (PhoneTail + match por DirectorioContacto/Persona/Empresa).
///
/// Todo corre tenant-scoped (RLS) sobre el PropiaDbContext del request; Personas/Empresas son
/// GLOBALES (sin RLS) y se consultan por Id salido de un vinculo tenant-scoped.
/// </summary>
public static class AgentContactoHelper
{
    /// <summary>Unidad de la copropiedad (para resolver texto libre y etiquetar coincidencias).</summary>
    public sealed record UnidadInfo(Guid Id, string Numero, string? Torre);

    /// <summary>Unidad resuelta desde texto libre.</summary>
    public sealed record UnidadResuelta(Guid Id, string Label);

    /// <summary>Un vinculo persona/empresa &lt;-&gt; unidad cuyo telefono coincide con el numero real.</summary>
    public sealed record ContactoMatch(
        Guid UnidadId, string UnidadLabel, RolUnidadPersona Rol,
        EntidadDirectorio EntidadTipo, Guid EntidadId, string? Nombre);

    /// <summary>Resultado de resolver el numero real: todas las coincidencias (vinculos) del contacto.</summary>
    public sealed class ContactoResolucion
    {
        /// <summary>Habia un telefono en el contexto de la conversacion.</summary>
        public bool TelefonoPresente { get; init; }
        /// <summary>Ultimos 10 digitos del numero real (null si no habia telefono).</summary>
        public string? Tail { get; init; }
        /// <summary>Vinculos (unidad+rol) cuya entidad tiene un telefono que coincide con el numero real.</summary>
        public IReadOnlyList<ContactoMatch> Matches { get; init; } = Array.Empty<ContactoMatch>();

        /// <summary>El numero figura vinculado a alguna unidad (cualquier rol).</summary>
        public bool Encontrado => Matches.Count > 0;
        /// <summary>El numero corresponde a un PROPIETARIO o RESIDENTE (Familiar/arrendatario/apoderado NO cuentan).</summary>
        public bool EsResidente => Matches.Any(m => EsRolResidente(m.Rol));
        /// <summary>Coincidencias que cuentan como propietario/residente.</summary>
        public IReadOnlyList<ContactoMatch> ResidentMatches => Matches.Where(m => EsRolResidente(m.Rol)).ToList();

        /// <summary>Mejor persona (natural) propietaria/residente para radicar en su nombre (null si el numero solo matchea empresas o roles no-residentes).</summary>
        public ContactoMatch? MejorPersonaResidente =>
            Matches.Where(m => m.EntidadTipo == EntidadDirectorio.Persona && EsRolResidente(m.Rol))
                   .OrderByDescending(m => m.Rol == RolUnidadPersona.Propietario)
                   .FirstOrDefault();
    }

    public static bool EsRolResidente(RolUnidadPersona r)
        => r == RolUnidadPersona.Propietario || r == RolUnidadPersona.Residente;

    public static string Label(UnidadInfo u)
        => string.IsNullOrWhiteSpace(u.Torre) ? u.Numero : $"{u.Torre} - {u.Numero}";

    /// <summary>Ultimos 10 digitos del telefono (quita +, espacios, guiones y tolera el prefijo pais 57). null si no hay digitos.</summary>
    public static string? PhoneTail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return null; }
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) { return null; }
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    public static string Last4(string tail)
        => tail.Length >= 4 ? tail[^4..] : tail;

    /// <summary>
    /// Resuelve texto libre a un item de un catalogo por su nombre (ej. tipo/categoria de PQR, tablero de
    /// tareas). Prioriza match exacto normalizado; si no, contiene/contenido y SOLO devuelve si es unico
    /// (evita elegir mal cuando el texto es ambiguo). null si no hay coincidencia inequivoca.
    /// </summary>
    public static T? MatchByName<T>(IEnumerable<T> items, Func<T, string> name, string? query) where T : class
    {
        static string N(string? s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var q = N(query);
        if (q.Length == 0) { return null; }
        var list = items.ToList();
        var exact = list.FirstOrDefault(x => N(name(x)) == q);
        if (exact is not null) { return exact; }
        var contains = list.Where(x => { var n = N(name(x)); return n.Length > 0 && (n.Contains(q) || q.Contains(n)); }).ToList();
        return contains.Count == 1 ? contains[0] : null;
    }

    /// <summary>Unidades de la copropiedad activa (RLS), para resolver texto libre y etiquetar.</summary>
    public static async Task<IReadOnlyList<UnidadInfo>> CargarUnidadesAsync(PropiaDbContext db, CancellationToken ct)
        => await db.UnidadesPrivadas.AsNoTracking()
            .Select(u => new UnidadInfo(u.Id, u.Numero, u.Torre != null ? u.Torre.Nombre : null))
            .ToListAsync(ct);

    /// <summary>
    /// Resuelve el numero real del contacto a sus vinculos (unidad+rol) via DirectorioContacto/Persona/Empresa.
    /// <paramref name="unidades"/> se pasa ya cargada para no consultarla dos veces (etiquetas de las coincidencias).
    /// </summary>
    public static async Task<ContactoResolucion> ResolverContactoAsync(
        PropiaDbContext db, string? contactPhone, IReadOnlyList<UnidadInfo> unidades, CancellationToken ct)
    {
        var tail = PhoneTail(contactPhone);
        if (tail is null) { return new ContactoResolucion { TelefonoPresente = false }; }

        var unidadLabel = unidades.ToDictionary(u => u.Id, Label);

        var vinculos = await db.UnidadPersonas.AsNoTracking()
            .Select(v => new { v.UnidadId, v.EntidadTipo, v.PersonaId, v.EmpresaId, v.Rol })
            .ToListAsync(ct);
        if (vinculos.Count == 0) { return new ContactoResolucion { TelefonoPresente = true, Tail = tail }; }

        var personaIds = vinculos.Where(v => v.PersonaId != null).Select(v => v.PersonaId!.Value).Distinct().ToList();
        var empresaIds = vinculos.Where(v => v.EmpresaId != null).Select(v => v.EmpresaId!.Value).Distinct().ToList();

        // Personas/Empresas GLOBALES (sin RLS): por Id salido del vinculo tenant-scoped.
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

        string? NameFor(EntidadDirectorio tipo, Guid entId)
        {
            if (tipo == EntidadDirectorio.Persona)
            {
                var per = personas.FirstOrDefault(x => x.Id == entId);
                return per is null ? null : $"{per.Nombres} {per.Apellidos}".Trim();
            }
            return empresas.FirstOrDefault(x => x.Id == entId)?.RazonSocial;
        }

        var matches = new List<ContactoMatch>();
        foreach (var v in vinculos)
        {
            Guid? entId = v.EntidadTipo == EntidadDirectorio.Persona ? v.PersonaId : v.EmpresaId;
            if (entId is null) { continue; }
            if (!phonesByEntity.TryGetValue((v.EntidadTipo, entId.Value), out var set)) { continue; }
            if (!set.Contains(tail)) { continue; }
            var lbl = unidadLabel.TryGetValue(v.UnidadId, out var l) ? l : "unidad desconocida";
            matches.Add(new ContactoMatch(v.UnidadId, lbl, v.Rol, v.EntidadTipo, entId.Value, NameFor(v.EntidadTipo, entId.Value)));
        }

        return new ContactoResolucion { TelefonoPresente = true, Tail = tail, Matches = matches };
    }

    /// <summary>
    /// Resuelve la unidad indicada en texto libre. Estrategia: (1) match exacto por "torre+numero" o
    /// por numero; (2) el texto contiene el numero de la unidad (unico); (3) desambigua por torre.
    /// Devuelve null si no logra una resolucion inequivoca (mejor no resolver que resolver mal).
    /// </summary>
    public static UnidadResuelta? ResolverUnidad(IReadOnlyList<UnidadInfo> units, string? input)
    {
        static string Norm(string? s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var q = Norm(input);
        if (q.Length == 0 || units.Count == 0) { return null; }

        var exact = units.FirstOrDefault(u => Norm($"{u.Torre}{u.Numero}") == q || Norm(u.Numero) == q);
        if (exact is not null) { return new UnidadResuelta(exact.Id, Label(exact)); }

        var byNumero = units.Where(u => Norm(u.Numero).Length > 0 && q.Contains(Norm(u.Numero))).ToList();
        if (byNumero.Count == 1) { return new UnidadResuelta(byNumero[0].Id, Label(byNumero[0])); }
        if (byNumero.Count > 1)
        {
            var conTorre = byNumero.Where(u => !string.IsNullOrWhiteSpace(u.Torre) && q.Contains(Norm(u.Torre))).ToList();
            if (conTorre.Count == 1) { return new UnidadResuelta(conTorre[0].Id, Label(conTorre[0])); }
        }
        return null;
    }
}
