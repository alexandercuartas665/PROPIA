using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Plantillas;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Plantillas;

/// <summary>
/// Catalogo central de plantillas de documentos (Configuracion > Plantillas de documentos). Corre en el
/// tenant del request (query filter + RLS). La primera vez que una copropiedad lista sus plantillas y no
/// tiene NINGUNA, copia las semillas globales de PQRSD (Super Admin) con categoria "pqrsd" (mismo
/// comportamiento que tenia PqrsdService): asi nace con las 5 de respuesta.
/// </summary>
public sealed class PlantillasDocumentoService : IPlantillasDocumentoService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PlantillasDocumentoService(PropiaDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    private static PlantillaDocumentoDto ToDto(PlantillaDocumento p)
        => new(p.Id, p.Nombre, p.Categoria, p.CuerpoHtml, p.UpdatedAt ?? p.CreatedAt, p.Orden);

    public async Task<IReadOnlyList<PlantillaDocumentoDto>> ListarAsync(string? categoria, CancellationToken ct)
    {
        await SembrarDesdeSemillaSiVacioAsync(ct);
        var q = _db.PlantillasDocumento.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var k = categoria.Trim().ToLowerInvariant();
            q = q.Where(p => p.Categoria == k);
        }
        var lista = await q.OrderBy(p => p.Orden).ThenBy(p => p.Nombre).ToListAsync(ct);
        return lista.Select(ToDto).ToList();
    }

    public async Task<PlantillaDocumentoDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PlantillasDocumento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<PlantillaDocumentoDto> CrearAsync(GuardarPlantillaDocumentoRequest req, CancellationToken ct)
    {
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new InvalidOperationException("Escribe el nombre de la plantilla.");
        var categoria = NormalizarCategoria(req.Categoria) ?? PlantillasCatalogo.Pqrsd;
        var orden = req.Orden ?? await _db.PlantillasDocumento.CountAsync(ct);
        var p = new PlantillaDocumento
        {
            Nombre = Recortar(nombre),
            Categoria = categoria,
            CuerpoHtml = Common.HtmlSanitization.Clean(req.CuerpoHtml) ?? "",  // S-19
            Orden = orden
        };
        _db.PlantillasDocumento.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PlantillaDocumentoDto?> ActualizarAsync(Guid id, GuardarPlantillaDocumentoRequest req, CancellationToken ct)
    {
        var p = await _db.PlantillasDocumento.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        // PUT MERGE: solo se tocan los campos enviados.
        if (req.Nombre is not null)
        {
            var nombre = req.Nombre.Trim();
            if (nombre.Length == 0) throw new InvalidOperationException("El nombre no puede quedar vacío.");
            p.Nombre = Recortar(nombre);
        }
        if (req.Categoria is not null) p.Categoria = NormalizarCategoria(req.Categoria) ?? p.Categoria;
        if (req.CuerpoHtml is not null) p.CuerpoHtml = Common.HtmlSanitization.Clean(req.CuerpoHtml) ?? "";  // S-19
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.PlantillasDocumento.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        _db.PlantillasDocumento.Remove(p);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? NormalizarCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return null;
        if (!PlantillasCatalogo.EsCategoriaValida(categoria))
            throw new InvalidOperationException("La categoría no existe.");
        return PlantillasCatalogo.Categoria(categoria).Clave;
    }

    private static string Recortar(string s) => s.Length > 200 ? s[..200] : s;

    // "Nace con" las plantillas: si la copropiedad no tiene NINGUNA, copia las semillas activas de PQRSD
    // del catalogo global. Idempotente: solo actua si esta vacio. Corre en el tenant actual (RLS ok).
    private async Task SembrarDesdeSemillaSiVacioAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        if (tenantId is null) return;
        if (await _db.PlantillasDocumento.AnyAsync(ct)) return;   // ya tiene (query filter -> del tenant)

        var semillas = await _db.PqrsdPlantillasSemilla.AsNoTracking()
            .Where(s => s.Activa).OrderBy(s => s.Orden).ThenBy(s => s.Nombre).ToListAsync(ct);
        if (semillas.Count == 0) return;

        foreach (var s in semillas)
            _db.PlantillasDocumento.Add(new PlantillaDocumento
            {
                TenantId = tenantId.Value,
                Nombre = s.Nombre,
                Categoria = PlantillasCatalogo.Pqrsd,
                CuerpoHtml = s.CuerpoHtml,
                Orden = s.Orden
            });
        await _db.SaveChangesAsync(ct);
    }
}
