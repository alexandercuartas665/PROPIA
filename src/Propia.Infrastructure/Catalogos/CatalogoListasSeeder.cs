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
    public sealed record SemillaOpcion(string Clave, string Label, int Orden, string? Meta = null, string? Color = null);

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

        // Contratos · Forma de pago (metodo): lista libre nueva (antes "Forma de pago" era el N de cuotas).
        // La clave ES el texto; A&D edita/agrega. Semilla = metodos comunes en Colombia.
        reg.Add(("contrato.formapago", new List<SemillaOpcion>
        {
            new("Efectivo", "Efectivo", 0),
            new("Transferencia", "Transferencia", 1),
            new("PSE", "PSE", 2),
            new("Cheque", "Cheque", 3),
            new("Tarjeta", "Tarjeta", 4),
            new("Debito automatico", "Debito automatico", 5),
            new("Otro", "Otro", 6),
        }));

        // Contratos · Tipo de servicio: paridad con el enum TipoServicio (labels que muestra la UI). Lista
        // libre (clave = texto): el contrato guarda el texto (TipoServicioTexto); A&D edita/agrega sin tocar
        // el enum, que sigue vivo para el modulo Servicios.
        reg.Add(("contrato.tiposervicio", new List<SemillaOpcion>
        {
            new("Aseo", "Aseo", 0),
            new("Seguridad", "Seguridad", 1),
            new("Mantenimiento", "Mantenimiento", 2),
            new("Jardineria", "Jardineria", 3),
            new("Mantenimiento piscina", "Mantenimiento piscina", 4),
            new("Ascensores", "Ascensores", 5),
            new("Plagas", "Plagas", 6),
            new("Seguro PH", "Seguro PH", 7),
            new("Internet / WiFi", "Internet / WiFi", 8),
            new("Energia", "Energia", 9),
            new("Agua", "Agua", 10),
            new("Gas", "Gas", 11),
            new("Otros", "Otros", 12),
        }));

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

        // Directorio (2.4): Etiqueta. Lista "libre": la clave ES la etiqueta (label). Semilla = las 8
        // etiquetas que antes vivian hardcoded en el catalogo rico (DirectorioService.EtiquetasBase).
        // Editable por A&D (activar/ocultar/agregar) sin deploy; los custom por copropiedad viven en la
        // config del campo (unidades-config, entidad=personas, campo=etiquetas).
        reg.Add(("directorio.etiqueta", new List<SemillaOpcion>
        {
            new("Propietario", "Propietario", 0),
            new("Residente", "Residente", 1),
            new("Arrendatario", "Arrendatario", 2),
            new("Familiar", "Familiar", 3),
            new("Apoderado", "Apoderado", 4),
            new("Personal de apoyo", "Personal de apoyo", 5),
            new("Contratista", "Contratista", 6),
            new("Proveedor", "Proveedor", 7),
        }));

        // Terceros (2.4 v2): "Servicio que presta" por el proveedor/contratista externo. Lista "libre"
        // (la clave ES el servicio/label), seleccion multiple, editable por A&D sin deploy. Se almacena
        // como etiquetas del vinculo del tercero con la copropiedad (reusa el mecanismo de etiquetas).
        reg.Add(("tercero.servicio", new List<SemillaOpcion>
        {
            new("Mantenimiento", "Mantenimiento", 0),
            new("Vigilancia", "Vigilancia", 1),
            new("Aseo", "Aseo", 2),
            new("Jardinería", "Jardinería", 3),
            new("Jurídico", "Jurídico", 4),
            new("Contable", "Contable", 5),
            new("Servicios públicos", "Servicios públicos", 6),
            new("Otros", "Otros", 7),
        }));

        // Usuarios (2.5 v2.0): Cargo (descriptivo, seleccion multiple, NO otorga permisos). Lista "libre":
        // la clave ES la etiqueta. Editable por A&D; el usuario guarda las etiquetas elegidas.
        reg.Add(("usuario.cargo", new List<SemillaOpcion>
        {
            new("Administrador", "Administrador", 0),
            new("Portero", "Portero", 1),
            new("Aseador", "Aseador", 2),
            new("Coordinador de zonas", "Coordinador de zonas", 3),
            new("Recepcionista", "Recepcionista", 4),
        }));

        // PQRSD · Tipo (con-logica, Ley 1755): clave estable "e:<Enum>", Meta con el plazo legal. La clase
        // legal (TipoPqrsd) es la clave; A&D edita el plazo base (global) y la copropiedad lo puede overridear
        // (pqrsd_tipos.override_plazo). Paridad con PqrsdCatalogo.PlazosBase + TipoNombreBase.
        reg.Add(("pqrsd.tipo",
            Propia.Domain.Enums.PqrsdCatalogo.PlazosBase
                .Select((p, i) => new SemillaOpcion(
                    "e:" + p.Tipo,
                    TipoNombrePqrsd(p.Tipo),
                    i,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        plazoDias = p.DiasHabiles,
                        diasInconformidad = p.DiasInconformidad,
                        nivelUrgencia = p.Urgencia.ToString(),
                        legal = p.Tipo.ToString()
                    })))
                .ToList()));

        // PQRSD · Etapa (columnas base del tablero): clave estable "e:<EstadoPqrsd>", color de fabrica, y
        // Meta con la semantica legal + terminal + orden. A&D edita label/color (global); la copropiedad
        // overridea (pqrsd_estados.override_visual). La SEMANTICA/TERMINAL son backbone legal (no se editan).
        reg.Add(("pqrsd.estado", new List<(string, string, bool, int)>
        {
            ("Recibida", "#94A3B8", false, (int)Propia.Domain.Enums.EstadoPqrsd.Recibida),
            ("En gestión", "#6D4FE3", false, (int)Propia.Domain.Enums.EstadoPqrsd.EnGestion),
            ("Respondida", "#0EA5E9", false, (int)Propia.Domain.Enums.EstadoPqrsd.Respondida),
            ("Cerrada", "#16A34A", true, (int)Propia.Domain.Enums.EstadoPqrsd.Cerrada),
            ("Via interna agotada", "#DC2626", true, (int)Propia.Domain.Enums.EstadoPqrsd.ViaInternaAgotada),
        }.Select((e, i) =>
        {
            var sem = (Propia.Domain.Enums.EstadoPqrsd)e.Item4;
            var meta = System.Text.Json.JsonSerializer.Serialize(new
            {
                semanticaLegal = sem.ToString(),
                terminal = e.Item3,
                orden = i + 1
            });
            return new SemillaOpcion("e:" + sem, e.Item1, i, meta, e.Item2);
        }).ToList()));

        // ===== Mantenimiento (enums puros; el scheduler/workflow lee el VALOR enum, no el label). A&D
        // renombra el label; el valor estable es "e:<Enum>". Meta guarda la semantica (informativa). =====
        reg.Add(("mantenimiento.periodicidad",
            Enum.GetValues<Propia.Domain.Enums.PeriodicidadProgramacion>()
                .Select((p, i) => new SemillaOpcion("e:" + p, p.ToString(), i,
                    System.Text.Json.JsonSerializer.Serialize(new { semantica = p.ToString() })))
                .ToList()));
        reg.Add(("mantenimiento.prioridad",
            Enum.GetValues<Propia.Domain.Enums.PrioridadTarea>()
                .Select((p, i) => new SemillaOpcion("e:" + p, p.ToString(), i)).ToList()));
        reg.Add(("mantenimiento.intervtipo",
            Enum.GetValues<Propia.Domain.Enums.TipoIntervencionMantenimiento>()
                .Select((t, i) => new SemillaOpcion("e:" + t, t.ToString(), i)).ToList()));
        reg.Add(("mantenimiento.intervestado",
            Enum.GetValues<Propia.Domain.Enums.EstadoIntervencion>()
                .Select((s, i) => new SemillaOpcion("e:" + s, s.ToString(), i,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        semantica = s.ToString(),
                        terminal = s is Propia.Domain.Enums.EstadoIntervencion.Completada or Propia.Domain.Enums.EstadoIntervencion.Cancelada
                    })))
                .ToList()));
        reg.Add(("mantenimiento.intervprioridad",
            Enum.GetValues<Propia.Domain.Enums.PrioridadIntervencion>()
                .Select((p, i) => new SemillaOpcion("e:" + p, p.ToString(), i)).ToList()));

        return reg;
    }

    // Nombre de fabrica de un tipo PQRSD (paridad con PqrsdService.TipoNombreBase).
    private static string TipoNombrePqrsd(Propia.Domain.Enums.TipoPqrsd t) => t switch
    {
        Propia.Domain.Enums.TipoPqrsd.Peticion => "Petición",
        Propia.Domain.Enums.TipoPqrsd.SolicitudDocumentos => "Solicitud de documentos",
        Propia.Domain.Enums.TipoPqrsd.Consulta => "Consulta",
        Propia.Domain.Enums.TipoPqrsd.Queja => "Queja",
        Propia.Domain.Enums.TipoPqrsd.Reclamo => "Reclamo",
        Propia.Domain.Enums.TipoPqrsd.Sugerencia => "Sugerencia",
        Propia.Domain.Enums.TipoPqrsd.Denuncia => "Denuncia",
        Propia.Domain.Enums.TipoPqrsd.Felicitacion => "Felicitación",
        _ => t.ToString()
    };

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
                    Color = op.Color,
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
