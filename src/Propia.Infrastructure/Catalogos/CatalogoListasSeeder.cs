using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
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

        // Contratos (Ola 4): tipocontrato y categoria ya eran editables por tenant (patron Unidades);
        // la clave ES el texto de la semilla (etiquetas de los enums TipoContrato / CategoriaContrato).
        reg.Add(("contrato.tipocontrato",
            ContratoCamposSistema.TiposContratoSemilla
                .Select((s, i) => new SemillaOpcion(s, s, i))
                .ToList()));
        reg.Add(("contrato.categoria",
            ContratoCamposSistema.CategoriasSemilla
                .Select((s, i) => new SemillaOpcion(s, s, i))
                .ToList()));

        // Listas enum "patron B" (Ola 4 tranche A): clave estable "e:<Enum>", label = el que muestra hoy
        // el panel (switch bespoke). Se replica aqui SOLO para la semilla de paridad; luego manda el catalogo.
        reg.Add(("vehiculo.tipovehiculo", new List<SemillaOpcion>
        {
            new("e:Automovil", "Carro", 0),   // el panel mapea Automovil -> "Carro"
            new("e:Moto", "Moto", 1),
            new("e:Bicicleta", "Bicicleta", 2),
            new("e:Camioneta", "Camioneta", 3),
            new("e:Otro", "Otro", 4),
        }));
        reg.Add(("mascota.tipo",
            Enum.GetValues<TipoMascota>()
                .Select((t, i) => new SemillaOpcion("e:" + t, t.ToString(), i))
                .ToList()));

        // Equipos (enum): categoria/estado/tipo. Labels = los que muestra el panel (estado abrevia).
        reg.Add(("equipo.categoria",
            Enum.GetValues<CategoriaEquipo>()
                .Select((c, i) => new SemillaOpcion("e:" + c, c.ToString(), i))
                .ToList()));
        reg.Add(("equipo.estado", new List<SemillaOpcion>
        {
            new("e:Operativo", "Operativo", 0),
            new("e:EnMantenimiento", "Mantenimiento", 1),
            new("e:FueraDeServicio", "Fuera", 2),
        }));
        reg.Add(("equipo.tipo", new List<SemillaOpcion>
        {
            new("e:Equipo", "Equipo", 0),
            new("e:Activo", "Activo", 1),
        }));

        // Zonas (enum): categoria/estado. Labels = enum (ToString, como el panel).
        reg.Add(("zona.categoria",
            Enum.GetValues<CategoriaZonaComun>()
                .Select((c, i) => new SemillaOpcion("e:" + c, c.ToString(), i))
                .ToList()));
        reg.Add(("zona.estado",
            Enum.GetValues<EstadoZonaComunMantenimiento>()
                .Select((s, i) => new SemillaOpcion("e:" + s, s.ToString(), i))
                .ToList()));

        // Personas/Directorio: sexo (enum GeneroPersona). tipo de ID y tipo residente se enganchan aparte.
        reg.Add(("persona.sexo",
            Enum.GetValues<GeneroPersona>()
                .Select((g, i) => new SemillaOpcion("e:" + g, g.ToString(), i))
                .ToList()));

        return reg;
    }

    public static async Task EnsureAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropiaDbContext>>();

        var insertadas = await SembrarAsync(db);
        if (insertadas > 0)
            logger.LogInformation("CatalogoListasSeeder: {N} opciones semilla insertadas.", insertadas);
    }

    /// <summary>
    /// Siembra las opciones de fabrica que FALTAN (por Lista+Clave) sobre el <paramref name="db"/> dado y
    /// devuelve cuantas inserto. IDEMPOTENTE: nunca pisa lo que A&D edito; una opcion semilla que A&D
    /// desactivo o renombro ya existe (misma clave), asi que no se re-crea. Reutilizable desde el arranque
    /// (<see cref="EnsureAsync"/>) y desde la consola A&D ("Re-sembrar listas base"). NO invalida el cache
    /// del lector: eso lo hace quien la invoca (el admin service).
    /// </summary>
    public static async Task<int> SembrarAsync(PropiaDbContext db, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var insertadas = 0;

        foreach (var (lista, opciones) in Registro())
        {
            var existentes = await db.CatalogoOpciones
                .Where(x => x.Lista == lista)
                .Select(x => x.Clave)
                .ToListAsync(ct);
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

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
