using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Application.Plantillas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
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
        => new(p.Id, p.Nombre, p.Categoria, p.CuerpoHtml, p.UpdatedAt ?? p.CreatedAt, p.Orden,
            Eleccion(p.EncabezadoModo, p.EncabezadoId), Eleccion(p.PieModo, p.PieId));

    private static string Eleccion(ModoMembrete modo, Guid? id) => modo switch
    {
        ModoMembrete.Ninguno => MembreteEleccion.Ninguno,
        ModoMembrete.Fijo when id is { } g => g.ToString(),
        _ => MembreteEleccion.Predeterminado
    };

    // "pred" | "ninguno" | Id de un membrete del tipo (de esta copropiedad). Otro valor = error.
    private async Task<(ModoMembrete Modo, Guid? Id)> LeerEleccionAsync(string valor, TipoMembrete tipo, CancellationToken ct)
    {
        var v = valor.Trim();
        if (v.Length == 0 || v.Equals(MembreteEleccion.Predeterminado, StringComparison.OrdinalIgnoreCase)) return (ModoMembrete.Predeterminado, null);
        if (v.Equals(MembreteEleccion.Ninguno, StringComparison.OrdinalIgnoreCase)) return (ModoMembrete.Ninguno, null);
        if (Guid.TryParse(v, out var id) && await _db.MembretesDocumento.AnyAsync(m => m.Id == id && m.Tipo == tipo, ct)) return (ModoMembrete.Fijo, id);
        throw new InvalidOperationException(tipo == TipoMembrete.Encabezado ? "El encabezado no existe." : "El pie de página no existe.");
    }

    private async Task AplicarMembretesAsync(PlantillaDocumento p, GuardarPlantillaDocumentoRequest req, CancellationToken ct)
    {
        if (req.Encabezado is not null) (p.EncabezadoModo, p.EncabezadoId) = await LeerEleccionAsync(req.Encabezado, TipoMembrete.Encabezado, ct);
        if (req.Pie is not null) (p.PieModo, p.PieId) = await LeerEleccionAsync(req.Pie, TipoMembrete.Pie, ct);
    }

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
        await AplicarMembretesAsync(p, req, ct);
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
        await AplicarMembretesAsync(p, req, ct);
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

    // ===================== Campos propios (columna "+"), espejo de Zonas =====================
    private static PlantillaCampoDefinicionDto ToDto(PlantillaCampoDefinicion d)
        => new(d.Id, d.Label, d.Orden, d.Tipo, d.Opciones, d.Descripcion);

    public async Task<IReadOnlyList<PlantillaCampoDefinicionDto>> ListarCamposAsync(CancellationToken ct)
        => (await _db.PlantillaCamposDefiniciones.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Label).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<PlantillaCampoDefinicionDto> CrearCampoAsync(CrearCampoDefinicionRequest req, CancellationToken ct)
    {
        if (_tenantContext.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        var label = LabelCampo(req.Label);
        var existente = await _db.PlantillaCamposDefiniciones.FirstOrDefaultAsync(d => d.Label.ToLower() == label.ToLower(), ct);
        if (existente is not null) return ToDto(existente);
        var maxOrden = await _db.PlantillaCamposDefiniciones.AnyAsync(ct) ? await _db.PlantillaCamposDefiniciones.MaxAsync(d => d.Orden, ct) : 0;
        var def = new PlantillaCampoDefinicion
        {
            TenantId = tid,
            Label = label,
            Orden = maxOrden + 1,
            Tipo = TipoPermitido(req.Tipo),
            Opciones = NormalizarOpciones(req.Tipo, req.Opciones),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim()
        };
        _db.PlantillaCamposDefiniciones.Add(def);
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    public async Task<bool> ActualizarCampoAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct)
    {
        var def = await _db.PlantillaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        def.Label = LabelCampo(req.Label);
        def.Tipo = TipoPermitido(req.Tipo);
        def.Opciones = NormalizarOpciones(req.Tipo, req.Opciones);
        def.Orden = req.Orden;
        def.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarCampoAsync(Guid definicionId, CancellationToken ct)
    {
        var def = await _db.PlantillaCamposDefiniciones.FirstOrDefaultAsync(d => d.Id == definicionId, ct);
        if (def is null) return false;
        _db.PlantillaCamposDefiniciones.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PlantillaCampoValorFlatDto>> ListarValoresAsync(CancellationToken ct)
        => await _db.PlantillaCamposValores.AsNoTracking().Where(v => v.Valor != null && v.Valor != "")
            .Select(v => new PlantillaCampoValorFlatDto(v.PlantillaDocumentoId, v.DefinicionId, v.Valor)).ToListAsync(ct);

    public async Task<bool> SetValorAsync(Guid plantillaId, Guid definicionId, string? valor, CancellationToken ct)
    {
        if (_tenantContext.CurrentTenantId is not Guid tid) throw new InvalidOperationException("Sin copropiedad activa.");
        // Plantilla y campo deben ser del tenant (el query filter ya lo garantiza; sin esto un id ajeno crearia huerfanos).
        if (!await _db.PlantillasDocumento.AnyAsync(p => p.Id == plantillaId, ct)) return false;
        if (!await _db.PlantillaCamposDefiniciones.AnyAsync(d => d.Id == definicionId, ct)) return false;
        var v = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        var existente = await _db.PlantillaCamposValores.FirstOrDefaultAsync(x => x.DefinicionId == definicionId && x.PlantillaDocumentoId == plantillaId, ct);
        if (existente is null)
            _db.PlantillaCamposValores.Add(new PlantillaCampoValor { TenantId = tid, DefinicionId = definicionId, PlantillaDocumentoId = plantillaId, Valor = v });
        else existente.Valor = v;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string LabelCampo(string? label)
    {
        var l = (label ?? "").Trim();
        if (l.Length == 0) throw new InvalidOperationException("El nombre del campo es obligatorio.");
        return l.Length > 80 ? l[..80] : l;
    }

    // Plantillas no calcula formulas ni totales (no hay motor de calculo para este modulo): caen a Texto.
    private static TipoCampoTablero TipoPermitido(TipoCampoTablero t)
        => t is TipoCampoTablero.Formula or TipoCampoTablero.Total or TipoCampoTablero.Separador ? TipoCampoTablero.Texto : t;

    // Mismo criterio que Zonas/Equipos: solo una lista lleva opciones (una por linea).
    private static string? NormalizarOpciones(TipoCampoTablero tipo, string? opciones)
    {
        if (tipo != TipoCampoTablero.Seleccion || string.IsNullOrWhiteSpace(opciones)) return null;
        var limpias = opciones.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return limpias.Length == 0 ? null : string.Join('\n', limpias);
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
