using Microsoft.EntityFrameworkCore;
using Propia.Application.Catalogos;
using Propia.Application.Common;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Catalogos;

/// <summary>
/// Copia POR TENANT de las listas del sistema (Ola 4, modelo PQRSD generalizado). Materializa la copia de
/// la copropiedad desde el catalogo global (<see cref="ICatalogoListas"/>) de forma perezosa, la sirve ya
/// compuesta (resolver) y la reaplica desde el catalogo respetando lo propio (re-sembrar por-tenant).
/// Implementa <see cref="IListasTenant"/> (mantenimiento) y <see cref="IOpcionesEfectivas"/> (lectura).
/// </summary>
public sealed class ListasTenantService : IListasTenant, IOpcionesEfectivas
{
    private readonly PropiaDbContext _db;
    private readonly ICatalogoListas _catalogo;
    private readonly ITenantContext _tenant;

    public ListasTenantService(PropiaDbContext db, ICatalogoListas catalogo, ITenantContext tenant)
    {
        _db = db;
        _catalogo = catalogo;
        _tenant = tenant;
    }

    // Compara dos Meta JSON por VALOR, no por texto: jsonb normaliza el almacenamiento (p.ej. agrega un espacio
    // tras ':'), asi que comparar strings crudos marcaria como "cambiada" una opcion que no cambio (y pondria
    // OverrideMeta en todas). Normaliza reserializando el documento; si alguno no parsea, cae a comparar texto.
    private static bool MetaIgual(string? a, string? b)
    {
        var na = string.IsNullOrWhiteSpace(a);
        var nb = string.IsNullOrWhiteSpace(b);
        if (na && nb) return true;
        if (na || nb) return false;
        try
        {
            using var da = System.Text.Json.JsonDocument.Parse(a!);
            using var db = System.Text.Json.JsonDocument.Parse(b!);
            return System.Text.Json.JsonSerializer.Serialize(da.RootElement)
                == System.Text.Json.JsonSerializer.Serialize(db.RootElement);
        }
        catch { return string.Equals(a, b, StringComparison.Ordinal); }
    }

    // 42P01 = "relation does not exist": la migracion de lista_opciones aun no esta aplicada en este
    // entorno. Durante la transicion de Ola 4 el resolver degrada a vacio (cada modulo cae a su fallback)
    // en vez de 500. Es temporal: quitar este salvavidas cuando la migracion este universal.
    private static bool EsTablaAusente(Exception ex)
        => (ex as Npgsql.PostgresException ?? ex.InnerException as Npgsql.PostgresException)?.SqlState == "42P01";

    public async Task AsegurarListaAsync(string lista, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        try
        {
            // Idempotente: si el tenant ya tiene su copia de esta lista, no se toca.
            if (await _db.ListaOpciones.AnyAsync(x => x.Lista == lista, ct)) return;

            // Semilla = opciones ACTIVAS del catalogo global. Una base desactivada por A&D no se copia:
            // el tenant nace sin ella (independencia).
            var baseCat = await _catalogo.OpcionesAsync(lista, incluirInactivas: false, ct);
            if (baseCat.Count == 0) return;

            var creadas = new List<ListaOpcion>(baseCat.Count);
            foreach (var o in baseCat)
            {
                var row = new ListaOpcion
                {
                    TenantId = tenantId,
                    Lista = lista,
                    Clave = o.Clave,
                    Label = o.Label,
                    Color = o.Color,
                    Orden = o.Orden,
                    Activo = true,
                    EsBase = true,
                    OverrideVisual = false,
                    Meta = o.Meta,          // Ola 4c: hereda la metadata de negocio del catalogo global.
                    OverrideMeta = false,
                };
                creadas.Add(row);
                _db.ListaOpciones.Add(row);
            }

            // Ola 4 (fold perezoso): al materializar por primera vez, importa el override que ESTA copropiedad
            // ya tenia en unidad_campos_config (Ola 3: color/ocultar; listas de texto libre: opciones propias)
            // para esta lista, para no perder lo que configuro antes de Ola 4. Best-effort: si falla, quedan las
            // bases. Se hace una sola vez (AsegurarLista es idempotente por el AnyAsync de arriba).
            try { await FoldOverridePrevioAsync(lista, tenantId, creadas, ct); } catch { /* solo bases */ }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (EsTablaAusente(ex)) { /* tabla aun no migrada: no-op (fallback) */ }
    }

    // Mapa lista -> (entidad, campo_clave) en unidad_campos_config, donde vivia el override por copropiedad antes
    // de Ola 4. Solo las listas que Ola 4 materializa en lista_opciones; unidad.tipo/unidad.estado NO estan
    // (conservan su gestor propio basado en config, con tipos_unidad_custom y avisos de uso).
    private static readonly IReadOnlyDictionary<string, (string Entidad, string Clave)> MapaOverridePrevio =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["vehiculo.tipovehiculo"] = ("vehiculos", "tipovehiculo"),
            ["zona.categoria"] = ("zona", "categoria"),
            ["zona.estado"] = ("zona", "estado"),
            ["equipo.categoria"] = ("equipo", "categoria"),
            ["equipo.tipo"] = ("equipo", "tipo"),
            ["equipo.estado"] = ("equipo", "estado"),
            ["mascota.tipo"] = ("mascotas", "tipo"),
            ["usuario.cargo"] = ("usuario", "cargo"),
            ["directorio.etiqueta"] = ("personas", "etiquetas"),
            ["persona.sexo"] = ("personas", "genero"),
            ["contrato.tipocontrato"] = ("contrato", "tipocontrato"),
            ["contrato.categoria"] = ("contrato", "categoria"),
            ["contrato.formapago"] = ("contrato", "formapago"),
            ["contrato.tiposervicio"] = ("contrato", "tipo"),
        };

    // Formato del override por campo (unidad_campos_config.Opciones): JSON [{K,Oculta,Color}]. El formato legado
    // (una opcion por linea, string[]) no se foldea: el Deserialize falla y se cae a solo-bases (aceptable).
    private sealed record OpcCfgPrevio(string? K, bool Oculta, string? Color);

    private async Task FoldOverridePrevioAsync(string lista, Guid tenantId, List<ListaOpcion> creadas, CancellationToken ct)
    {
        if (!MapaOverridePrevio.TryGetValue(lista, out var map)) return;

        // El query filter de tenant (TenantEntity) acota a la copropiedad activa; no hace falta filtrar por tenant aqui.
        var cfg = await _db.UnidadCamposConfig.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Entidad == map.Entidad && c.CampoClave == map.Clave, ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.Opciones)) return;

        List<OpcCfgPrevio>? ops;
        try { ops = System.Text.Json.JsonSerializer.Deserialize<List<OpcCfgPrevio>>(cfg.Opciones!); }
        catch { return; }
        if (ops is null || ops.Count == 0) return;

        var porClave = new Dictionary<string, ListaOpcion>(StringComparer.OrdinalIgnoreCase);
        var porLabel = new Dictionary<string, ListaOpcion>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in creadas)
        {
            porClave[r.Clave] = r;
            porLabel[(r.Label ?? "").Trim()] = r;
        }

        var orden = creadas.Count;
        foreach (var op in ops)
        {
            var k = (op.K ?? "").Trim();
            if (k.Length == 0) continue;
            var esEnum = k.StartsWith("e:", StringComparison.Ordinal);

            ListaOpcion? row = null;
            if (esEnum) porClave.TryGetValue(k, out row);
            else porLabel.TryGetValue(k, out row);

            if (row is not null)
            {
                // Base del catalogo que la copropiedad habia coloreado/ocultado: se marca como override para que
                // el re-sembrar por-tenant ya no la pise.
                var toco = false;
                if (!string.IsNullOrWhiteSpace(op.Color) && !string.Equals(row.Color, op.Color, StringComparison.Ordinal))
                { row.Color = op.Color; toco = true; }
                if (op.Oculta && row.Activo) { row.Activo = false; toco = true; }
                if (toco && row.EsBase) row.OverrideVisual = true;
            }
            else if (!esEnum)
            {
                // Opcion propia (texto libre) que no esta en el catalogo global: se preserva como custom del tenant.
                _db.ListaOpciones.Add(new ListaOpcion
                {
                    TenantId = tenantId,
                    Lista = lista,
                    Clave = "c:" + Guid.NewGuid().ToString("N"),
                    Label = k,
                    Color = string.IsNullOrWhiteSpace(op.Color) ? null : op.Color,
                    Orden = orden++,
                    Activo = !op.Oculta,
                    EsBase = false,
                    OverrideVisual = false,
                });
            }
        }
    }

    public async Task<IReadOnlyList<OpcionEfectivaDto>> OpcionesAsync(
        string lista, bool incluirOcultas = false, CancellationToken ct = default)
    {
        try
        {
            await AsegurarListaAsync(lista, ct);

            var q = _db.ListaOpciones.AsNoTracking().Where(x => x.Lista == lista);
            if (!incluirOcultas) q = q.Where(x => x.Activo);
            return await q
                .OrderBy(x => x.Orden).ThenBy(x => x.Label)
                .Select(x => new OpcionEfectivaDto(x.Clave, x.Label, x.Orden, x.Color, x.Activo, x.EsBase, x.Meta))
                .ToListAsync(ct);
        }
        catch (Exception ex) when (EsTablaAusente(ex)) { return System.Array.Empty<OpcionEfectivaDto>(); }
    }

    public async Task<int> ReaplicarBasesAsync(string? lista = null, CancellationToken ct = default)
    {
        _ = _tenant.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        var listas = lista is not null
            ? new List<string> { lista }
            : await _db.ListaOpciones.Select(x => x.Lista).Distinct().ToListAsync(ct);

        var cambios = 0;
        foreach (var l in listas)
        {
            var baseCat = await _catalogo.OpcionesAsync(l, incluirInactivas: false, ct);
            if (baseCat.Count == 0) continue;

            var existentes = await _db.ListaOpciones.Where(x => x.Lista == l).ToListAsync(ct);
            var porClave = existentes.ToDictionary(x => x.Clave, StringComparer.OrdinalIgnoreCase);
            var clavesActivasCat = new HashSet<string>(baseCat.Select(o => o.Clave), StringComparer.OrdinalIgnoreCase);

            // Agrega bases que falten y realinea las no-overrideadas.
            foreach (var o in baseCat)
            {
                if (porClave.TryGetValue(o.Clave, out var row))
                {
                    // Lo VISUAL (label/color/orden/activo) solo se realinea si la copropiedad no lo overrideo.
                    if (!row.OverrideVisual)
                    {
                        if (row.Label != o.Label) { row.Label = o.Label; cambios++; }
                        if (row.Color != o.Color) { row.Color = o.Color; cambios++; }
                        if (row.Orden != o.Orden) { row.Orden = o.Orden; cambios++; }
                        if (!row.Activo) { row.Activo = true; cambios++; }   // reactivar base que A&D volvio a activar
                    }
                    // La META (Ola 4c) es un eje INDEPENDIENTE: se realinea salvo que la copropiedad la overrideo.
                    if (!row.OverrideMeta && !MetaIgual(row.Meta, o.Meta))
                    { row.Meta = o.Meta; cambios++; }
                    if (!row.EsBase) row.EsBase = true;
                }
                else
                {
                    _db.ListaOpciones.Add(new ListaOpcion
                    {
                        Lista = l,
                        Clave = o.Clave,
                        Label = o.Label,
                        Color = o.Color,
                        Orden = o.Orden,
                        Activo = true,
                        EsBase = true,
                        OverrideVisual = false,
                        Meta = o.Meta,
                        OverrideMeta = false,
                    });
                    cambios++;
                }
            }

            // Desactiva (no borra) las bases no-overrideadas que el catalogo global ya desactivo.
            foreach (var row in existentes)
                if (row.EsBase && !row.OverrideVisual && row.Activo && !clavesActivasCat.Contains(row.Clave))
                {
                    row.Activo = false;
                    cambios++;
                }
        }

        if (cambios > 0) await _db.SaveChangesAsync(ct);
        return cambios;
    }

    public async Task<int> GuardarOpcionesAsync(string lista, IReadOnlyList<OpcionGuardarDto> opciones, CancellationToken ct = default)
    {
        var tenantId = _tenant.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        try
        {
            await AsegurarListaAsync(lista, ct);
            var existentes = await _db.ListaOpciones.Where(x => x.Lista == lista).ToListAsync(ct);

            // Index por label normalizado (insensible a may/acentos no, solo trim+casing: el universo base de
            // una copia es pequeno y de labels distintos; el renombrado se trata como propia nueva).
            static string Norm(string? s) => (s ?? "").Trim();
            var porLabel = new Dictionary<string, ListaOpcion>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in existentes) porLabel[Norm(e.Label)] = e;

            var cambios = 0;
            var usados = new HashSet<Guid>();
            for (var i = 0; i < opciones.Count; i++)
            {
                var op = opciones[i];
                var lbl = Norm(op.Label);
                if (lbl.Length == 0) continue;
                var activo = !op.Oculta;

                if (porLabel.TryGetValue(lbl, out var row))
                {
                    usados.Add(row.Id);
                    var toco = false;
                    if (row.Orden != i) { row.Orden = i; toco = true; }
                    if (!string.Equals(row.Color, op.Color, StringComparison.Ordinal)) { row.Color = op.Color; toco = true; }
                    if (row.Activo != activo) { row.Activo = activo; toco = true; }
                    // Solo las bases que la copropiedad REALMENTE cambio quedan overrideadas (el re-sembrar ya no
                    // las pisa); las que vienen iguales NO se marcan, para que el re-sembrar siga propagandoles
                    // los cambios del catalogo global.
                    if (toco)
                    {
                        if (row.EsBase && !row.OverrideVisual) row.OverrideVisual = true;
                        cambios++;
                    }
                    // Meta (Ola 4c): null = el editor no la envia (listas libres), se deja intacta. Un valor nuevo
                    // sobre una base la marca OverrideMeta (el re-sembrar ya no la pisa); es un eje aparte del visual.
                    // Comparacion por valor (MetaIgual): jsonb normaliza el texto, comparar strings marcaria todo.
                    if (op.Meta is not null && !MetaIgual(row.Meta, op.Meta))
                    {
                        row.Meta = op.Meta;
                        if (row.EsBase) row.OverrideMeta = true;
                        cambios++;
                    }
                }
                else
                {
                    _db.ListaOpciones.Add(new ListaOpcion
                    {
                        TenantId = tenantId,
                        Lista = lista,
                        Clave = "c:" + Guid.NewGuid().ToString("N"),
                        Label = lbl,
                        Color = op.Color,
                        Orden = i,
                        Activo = activo,
                        EsBase = false,
                        OverrideVisual = false,
                        Meta = op.Meta,          // Ola 4c: la opcion propia acarrea su metadata de negocio.
                        OverrideMeta = false,
                    });
                    cambios++;
                }
            }

            // Las que el editor quito de la lista: desactivar (no borrar; una base no se puede perder y una
            // propia puede estar en uso).
            foreach (var e in existentes)
                if (!usados.Contains(e.Id) && e.Activo) { e.Activo = false; cambios++; }

            if (cambios > 0) await _db.SaveChangesAsync(ct);
            return cambios;
        }
        catch (Exception ex) when (EsTablaAusente(ex))
        {
            throw new InvalidOperationException("La tabla de listas por copropiedad aun no esta migrada en este entorno.");
        }
    }
}
