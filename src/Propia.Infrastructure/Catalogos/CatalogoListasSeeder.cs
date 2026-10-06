using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Catalogos;

/// <summary>
/// Siembra el catalogo global de listas (<c>catalogo_opcion</c>) a partir de las fuentes de "fabrica"
/// que hoy viven en codigo (enums/semillas). IDEMPOTENTE: inserta solo las opciones semilla que
/// FALTAN (por Lista+Clave); NUNCA pisa lo que A&D ya edito. Asi la primera version del catalogo
/// refleja exactamente lo que el producto muestra hoy (paridad), y a partir de ahi A&D manda.
///
/// Ola 1: registra el PILOTO (unidad.tipo, unidad.estado). Las demas listas del inventario se
/// agregan a <see cref="Registro"/> cuando se enganche cada modulo (Ola 4).
/// </summary>
public static class CatalogoListasSeeder
{
    /// <summary>Una opcion semilla de una lista (clave estable + label de fabrica + orden + metadata opcional).</summary>
    public sealed record SemillaOpcion(string Clave, string Label, int Orden, string? Meta = null);

    /// <summary>Registro de las listas del catalogo con sus semillas de fabrica (fuente de la paridad).</summary>
    public static IReadOnlyList<(string Lista, IReadOnlyList<SemillaOpcion> Opciones)> Registro()
    {
        var reg = new List<(string, IReadOnlyList<SemillaOpcion>)>();

        // unidad.tipo: clave "e:<Enum>" (misma convencion que el override por tenant), label del
        // catalogo compartido TiposUnidadCatalogo (paridad exacta con lo que muestra la UI hoy).
        reg.Add(("unidad.tipo",
            TiposUnidadCatalogo.Todos
                .Select((x, i) => new SemillaOpcion("e:" + x.Tipo, x.Etiqueta, i))
                .ToList()));

        // unidad.estado: lista "libre"; la clave ES el texto (misma convencion que hoy).
        reg.Add(("unidad.estado",
            UnidadCamposSistema.EstadosSemilla
                .Select((e, i) => new SemillaOpcion(e, e, i))
                .ToList()));

        return reg;
    }

    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var now = DateTimeOffset.UtcNow;
        var insertadas = 0;

        foreach (var (lista, opciones) in Registro())
        {
            var existentes = await db.CatalogoOpciones
                .Where(x => x.Lista == lista)
                .Select(x => x.Clave)
                .ToListAsync();
            var set = new HashSet<string>(existentes, StringComparer.OrdinalIgnoreCase);

            foreach (var op in opciones)
            {
                if (set.Contains(op.Clave)) continue;   // ya existe (o A&D ya la toco): no pisar
                db.CatalogoOpciones.Add(new CatalogoOpcion
                {
                    Lista = lista,
                    Clave = op.Clave,
                    Label = op.Label,
                    Orden = op.Orden,
                    Color = null,
                    Activo = true,
                    EsSemilla = true,
                    Meta = op.Meta,
                    CreatedAt = now,
                });
                insertadas++;
            }
        }

        if (insertadas > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("CatalogoListasSeeder: {N} opciones semilla insertadas.", insertadas);
        }
    }
}
