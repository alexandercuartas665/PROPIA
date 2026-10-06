using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Catalogos;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Catalogos;

/// <summary>
/// Escritura del catalogo global desde la consola A&D. Tabla GLOBAL (sin RLS): el gate es la policy
/// SuperAdmin del controller. Cada mutacion invalida el cache del lector en el proceso.
/// </summary>
public sealed class CatalogoListasAdminService : ICatalogoListasAdmin
{
    private readonly PropiaDbContext _db;
    private readonly ICatalogoListas _lector;

    public CatalogoListasAdminService(PropiaDbContext db, ICatalogoListas lector)
    {
        _db = db;
        _lector = lector;
    }

    public async Task<IReadOnlyList<CatalogoListaInfoDto>> ListasAsync(CancellationToken ct = default)
    {
        var conteos = await _db.CatalogoOpciones.AsNoTracking()
            .GroupBy(x => x.Lista)
            .Select(g => new { Lista = g.Key, Total = g.Count(), Activas = g.Count(x => x.Activo) })
            .ToListAsync(ct);

        // Union de las listas conocidas (aunque esten vacias) y las presentes en la tabla.
        var claves = conteos.Select(c => c.Lista)
            .Union(CatalogoListasRegistro.Conocidas.Select(c => c.Lista), StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var res = new List<CatalogoListaInfoDto>();
        foreach (var lista in claves)
        {
            var info = CatalogoListasRegistro.Info(lista);
            var c = conteos.FirstOrDefault(x => string.Equals(x.Lista, lista, StringComparison.OrdinalIgnoreCase));
            res.Add(new CatalogoListaInfoDto(lista, info.Nombre, info.ConLogica, c?.Total ?? 0, c?.Activas ?? 0));
        }
        return res;
    }

    public async Task<IReadOnlyList<CatalogoOpcionAdminDto>> OpcionesAsync(string lista, CancellationToken ct = default)
        => await _db.CatalogoOpciones.AsNoTracking()
            .Where(x => x.Lista == lista)
            .OrderBy(x => x.Orden).ThenBy(x => x.Label)
            .Select(x => new CatalogoOpcionAdminDto(x.Id, x.Lista, x.Clave, x.Label, x.Orden, x.Color, x.Activo, x.EsSemilla, x.Meta))
            .ToListAsync(ct);

    public async Task<CatalogoOpcionAdminDto> CrearAsync(string lista, CrearOpcionCatalogoRequest req, CancellationToken ct = default)
    {
        var label = (req.Label ?? "").Trim();
        if (label.Length == 0) throw new InvalidOperationException("El nombre de la opcion es obligatorio.");

        var existentes = await _db.CatalogoOpciones.Where(x => x.Lista == lista)
            .Select(x => new { x.Clave, x.Orden }).ToListAsync(ct);
        var claves = new HashSet<string>(existentes.Select(e => e.Clave), StringComparer.OrdinalIgnoreCase);
        var clave = ClaveUnica(label, claves);
        var orden = existentes.Count == 0 ? 0 : existentes.Max(e => e.Orden) + 1;

        var op = new CatalogoOpcion
        {
            Lista = lista,
            Clave = clave,
            Label = label,
            Orden = orden,
            Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color!.Trim(),
            Activo = true,
            EsSemilla = false,
            Meta = string.IsNullOrWhiteSpace(req.Meta) ? null : req.Meta,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.CatalogoOpciones.Add(op);
        await _db.SaveChangesAsync(ct);
        _lector.InvalidarCache(lista);

        return new CatalogoOpcionAdminDto(op.Id, op.Lista, op.Clave, op.Label, op.Orden, op.Color, op.Activo, op.EsSemilla, op.Meta);
    }

    public async Task<CatalogoOpcionAdminDto?> ActualizarAsync(Guid id, ActualizarOpcionCatalogoRequest req, CancellationToken ct = default)
    {
        var op = await _db.CatalogoOpciones.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (op is null) return null;

        var label = (req.Label ?? "").Trim();
        if (label.Length == 0) throw new InvalidOperationException("El nombre de la opcion es obligatorio.");

        op.Label = label;
        op.Color = string.IsNullOrWhiteSpace(req.Color) ? null : req.Color!.Trim();
        op.Activo = req.Activo;
        op.Meta = string.IsNullOrWhiteSpace(req.Meta) ? null : req.Meta;
        await _db.SaveChangesAsync(ct);
        _lector.InvalidarCache(op.Lista);

        return new CatalogoOpcionAdminDto(op.Id, op.Lista, op.Clave, op.Label, op.Orden, op.Color, op.Activo, op.EsSemilla, op.Meta);
    }

    public async Task ReordenarAsync(string lista, IReadOnlyList<Guid> ordenIds, CancellationToken ct = default)
    {
        var ops = await _db.CatalogoOpciones.Where(x => x.Lista == lista).ToListAsync(ct);
        var pos = ordenIds.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i);
        foreach (var op in ops)
            if (pos.TryGetValue(op.Id, out var i))
                op.Orden = i;
        await _db.SaveChangesAsync(ct);
        _lector.InvalidarCache(lista);
    }

    // Clave estable para opciones nuevas: slug ASCII del label, unico dentro de la lista.
    private static string ClaveUnica(string label, HashSet<string> usadas)
    {
        var baseSlug = Slug(label);
        if (baseSlug.Length == 0) baseSlug = "opcion";
        var clave = baseSlug;
        var n = 2;
        while (usadas.Contains(clave)) clave = $"{baseSlug}-{n++}";
        usadas.Add(clave);
        return clave;
    }

    private static string Slug(string s)
    {
        // Quita acentos, pasa a minuscula ASCII, reemplaza lo no alfanumerico por '-'.
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else sb.Append('-');
        }
        var slug = sb.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
