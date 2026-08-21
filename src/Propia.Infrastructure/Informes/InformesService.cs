using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Common;
using Propia.Application.Informes;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Informes;

/// <summary>
/// Modulo Informes de gestion. Plantillas inteligentes (secciones + prompt por seccion) e informes
/// generados con IA. Reusa el agente "Auxiliar Administrativo" (proveedor/credenciales/cuota) via
/// IAiInferenceService, pero con un system prompt de "redactor de informes" para redaccion larga
/// y en espanol con acentos.
/// </summary>
public class InformesService : IInformesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAsistenteCamposService _asistente;
    private readonly IAiInferenceService _inference;
    private readonly ILogger<InformesService> _logger;

    public InformesService(
        PropiaDbContext db,
        ITenantContext tenant,
        IAsistenteCamposService asistente,
        IAiInferenceService inference,
        ILogger<InformesService> logger)
    {
        _db = db;
        _tenant = tenant;
        _asistente = asistente;
        _inference = inference;
        _logger = logger;
    }

    private const string SystemPromptInforme = """
Eres un redactor profesional de informes de gestion para una copropiedad (propiedad
horizontal) en Colombia. Tu tarea es redactar UNA seccion del informe a partir de la
instruccion que se te da.

Reglas:
- Devuelve UNICAMENTE el contenido de la seccion, sin repetir el titulo, sin encabezados
  Markdown, sin saludos ni frases como "Aqui tienes". El titulo lo pone el sistema.
- Escribe en espanol claro, formal y en tono institucional de administracion PH, con
  acentos y ortografia correctos.
- Se concreto y profesional. Puedes usar parrafos o vinetas con guion cuando ayuden a la
  lectura.
- NO inventes cifras, nombres, fechas ni valores que no te hayan entregado. Si falta un
  dato numerico, redacta de forma general o deja un marcador entre corchetes, por ejemplo
  [valor por confirmar], para que el administrador lo complete.
- Ajusta la extension a lo pedido; por defecto entre 1 y 3 parrafos.
""";

    // =====================================================================
    // Plantillas
    // =====================================================================

    public async Task<IReadOnlyList<InformePlantillaDto>> ListarPlantillasAsync(CancellationToken ct)
    {
        var plantillas = await _db.InformePlantillas
            .AsNoTracking()
            .Include(p => p.Secciones)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);
        return plantillas.Select(MapPlantilla).ToList();
    }

    public async Task<InformePlantillaDto?> GetPlantillaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.InformePlantillas
            .AsNoTracking()
            .Include(x => x.Secciones)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : MapPlantilla(p);
    }

    public async Task<InformePlantillaDto> CrearPlantillaAsync(GuardarPlantillaRequest req, CancellationToken ct)
    {
        var plantilla = new InformePlantilla
        {
            Id = Guid.NewGuid(),
            Nombre = (req.Nombre ?? string.Empty).Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var orden = 0;
        foreach (var s in (req.Secciones ?? new()).OrderBy(x => x.Orden))
        {
            plantilla.Secciones.Add(new InformePlantillaSeccion
            {
                Id = Guid.NewGuid(),
                Titulo = (s.Titulo ?? string.Empty).Trim(),
                Orden = orden++,
                Prompt = string.IsNullOrWhiteSpace(s.Prompt) ? null : s.Prompt.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        _db.InformePlantillas.Add(plantilla);
        await _db.SaveChangesAsync(ct);
        return MapPlantilla(plantilla);
    }

    public async Task<bool> ActualizarPlantillaAsync(Guid id, GuardarPlantillaRequest req, CancellationToken ct)
    {
        var plantilla = await _db.InformePlantillas
            .Include(x => x.Secciones)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (plantilla is null) return false;

        plantilla.Nombre = (req.Nombre ?? string.Empty).Trim();
        plantilla.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        plantilla.UpdatedAt = DateTimeOffset.UtcNow;

        var entrantes = req.Secciones ?? new();
        var idsEntrantes = entrantes.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToHashSet();

        // Borra las secciones que ya no vienen
        var aBorrar = plantilla.Secciones.Where(s => !idsEntrantes.Contains(s.Id)).ToList();
        foreach (var s in aBorrar) _db.InformePlantillaSecciones.Remove(s);

        var orden = 0;
        foreach (var s in entrantes.OrderBy(x => x.Orden))
        {
            var existente = s.Id.HasValue ? plantilla.Secciones.FirstOrDefault(x => x.Id == s.Id.Value) : null;
            if (existente is null)
            {
                plantilla.Secciones.Add(new InformePlantillaSeccion
                {
                    Id = Guid.NewGuid(),
                    Titulo = (s.Titulo ?? string.Empty).Trim(),
                    Orden = orden++,
                    Prompt = string.IsNullOrWhiteSpace(s.Prompt) ? null : s.Prompt.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existente.Titulo = (s.Titulo ?? string.Empty).Trim();
                existente.Orden = orden++;
                existente.Prompt = string.IsNullOrWhiteSpace(s.Prompt) ? null : s.Prompt.Trim();
                existente.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPlantillaAsync(Guid id, CancellationToken ct)
    {
        var plantilla = await _db.InformePlantillas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (plantilla is null) return false;
        _db.InformePlantillas.Remove(plantilla);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> SembrarPlantillasBaseAsync(CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is null) return 0;
        if (await _db.InformePlantillas.AnyAsync(ct)) return 0;

        var secciones = new (string Titulo, string Prompt)[]
        {
            ("Resumen ejecutivo",
                "Redacta un resumen ejecutivo del periodo para la asamblea de copropietarios: gestion general, avances mas relevantes y estado general de la copropiedad. Tono institucional y breve."),
            ("Gestion administrativa",
                "Describe la gestion administrativa del periodo: contratos y proveedores, tramites, correspondencia y atencion a residentes. No inventes cifras; usa marcadores entre corchetes si faltan datos."),
            ("Gestion financiera",
                "Redacta un panorama de la gestion financiera del periodo: ingresos por cuotas de administracion, gastos principales, estado de cartera y ejecucion presupuestal. Deja marcadores entre corchetes para las cifras que deba completar el administrador."),
            ("Mantenimiento y zonas comunes",
                "Resume las actividades de mantenimiento preventivo y correctivo, el estado de los equipos y las zonas comunes, y las mejoras realizadas en el periodo."),
            ("PQRSD y convivencia",
                "Resume la gestion de peticiones, quejas, reclamos, sugerencias y denuncias (PQRSD) del periodo, los casos de convivencia atendidos y su estado. No inventes numeros concretos."),
            ("Conclusiones y recomendaciones",
                "Cierra el informe con conclusiones del periodo y recomendaciones para el proximo, orientadas a la asamblea y al consejo de administracion.")
        };

        var plantilla = new InformePlantilla
        {
            Id = Guid.NewGuid(),
            Nombre = "Informe de gestion mensual",
            Descripcion = "Plantilla base para el informe de gestion periodico ante la asamblea y el consejo de administracion.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var orden = 0;
        foreach (var (titulo, prompt) in secciones)
        {
            plantilla.Secciones.Add(new InformePlantillaSeccion
            {
                Id = Guid.NewGuid(),
                Titulo = titulo,
                Orden = orden++,
                Prompt = prompt,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        _db.InformePlantillas.Add(plantilla);
        await _db.SaveChangesAsync(ct);
        return 1;
    }

    // =====================================================================
    // Informes (instancias)
    // =====================================================================

    public async Task<IReadOnlyList<InformeListItemDto>> ListarInformesAsync(CancellationToken ct)
    {
        return await _db.Informes
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InformeListItemDto(
                i.Id, i.Titulo, i.Periodo, i.Estado, i.GeneradoEn, i.Secciones.Count))
            .ToListAsync(ct);
    }

    public async Task<InformeDetalleDto?> GetInformeAsync(Guid id, CancellationToken ct)
    {
        var inf = await _db.Informes
            .AsNoTracking()
            .Include(i => i.Secciones)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return inf is null ? null : MapInforme(inf);
    }

    public async Task<InformeDetalleDto?> CrearInformeAsync(CrearInformeRequest req, CancellationToken ct)
    {
        if (_tenant.CurrentTenantId is null) return null;

        var inf = new Informe
        {
            Id = Guid.NewGuid(),
            PlantillaId = req.PlantillaId,
            Titulo = (req.Titulo ?? string.Empty).Trim(),
            Periodo = string.IsNullOrWhiteSpace(req.Periodo) ? null : req.Periodo.Trim(),
            Estado = EstadoInforme.Borrador,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (req.PlantillaId is Guid pid)
        {
            var plantilla = await _db.InformePlantillas
                .AsNoTracking()
                .Include(p => p.Secciones)
                .FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (plantilla is not null)
            {
                if (string.IsNullOrWhiteSpace(inf.Titulo)) inf.Titulo = plantilla.Nombre;
                foreach (var s in plantilla.Secciones.OrderBy(x => x.Orden))
                {
                    inf.Secciones.Add(new InformeSeccion
                    {
                        Id = Guid.NewGuid(),
                        Titulo = s.Titulo,
                        Orden = s.Orden,
                        Prompt = s.Prompt,
                        Contenido = null,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        if (string.IsNullOrWhiteSpace(inf.Titulo)) inf.Titulo = "Informe de gestion";

        _db.Informes.Add(inf);
        await _db.SaveChangesAsync(ct);
        return MapInforme(inf);
    }

    public async Task<bool> EliminarInformeAsync(Guid id, CancellationToken ct)
    {
        var inf = await _db.Informes.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inf is null) return false;
        _db.Informes.Remove(inf);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarSeccionAsync(Guid informeId, Guid seccionId, GuardarInformeSeccionRequest req, CancellationToken ct)
    {
        var seccion = await _db.InformeSecciones
            .FirstOrDefaultAsync(s => s.Id == seccionId && s.InformeId == informeId, ct);
        if (seccion is null) return false;

        seccion.Contenido = req.Contenido;
        if (req.Prompt is not null)
            seccion.Prompt = string.IsNullOrWhiteSpace(req.Prompt) ? null : req.Prompt.Trim();
        seccion.UpdatedAt = DateTimeOffset.UtcNow;

        // Editar contenido pasa el informe a Generado (ya tiene material)
        var inf = await _db.Informes.FirstOrDefaultAsync(i => i.Id == informeId, ct);
        if (inf is not null && inf.Estado == EstadoInforme.Borrador && !string.IsNullOrWhiteSpace(req.Contenido))
            inf.Estado = EstadoInforme.Generado;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // =====================================================================
    // Generacion con IA
    // =====================================================================

    public async Task<InformeSeccionDto?> GenerarSeccionAsync(Guid informeId, Guid seccionId, CancellationToken ct)
    {
        var inf = await _db.Informes
            .Include(i => i.Secciones)
            .FirstOrDefaultAsync(i => i.Id == informeId, ct);
        if (inf is null) return null;
        var seccion = inf.Secciones.FirstOrDefault(s => s.Id == seccionId);
        if (seccion is null) return null;

        var contexto = await ContextoCopropiedadAsync(ct);
        var ok = await GenerarContenidoSeccionAsync(inf, seccion, contexto, ct);

        if (ok && inf.Estado == EstadoInforme.Borrador) inf.Estado = EstadoInforme.Generado;
        inf.GeneradoEn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new InformeSeccionDto(seccion.Id, seccion.Titulo, seccion.Orden, seccion.Prompt, seccion.Contenido);
    }

    public async Task<InformeDetalleDto?> GenerarInformeAsync(Guid informeId, CancellationToken ct)
    {
        var inf = await _db.Informes
            .Include(i => i.Secciones)
            .FirstOrDefaultAsync(i => i.Id == informeId, ct);
        if (inf is null) return null;

        var contexto = await ContextoCopropiedadAsync(ct);
        foreach (var seccion in inf.Secciones.OrderBy(s => s.Orden))
            await GenerarContenidoSeccionAsync(inf, seccion, contexto, ct);

        inf.Estado = EstadoInforme.Generado;
        inf.GeneradoEn = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapInforme(inf);
    }

    /// <summary>Genera el texto de una seccion e lo asigna a seccion.Contenido. No guarda (lo hace el caller).</summary>
    private async Task<bool> GenerarContenidoSeccionAsync(Informe inf, InformeSeccion seccion, string contexto, CancellationToken ct)
    {
        var agentId = await _asistente.EnsureAgenteAsync(ct);
        if (agentId is null) return false;

        var prompt = new StringBuilder();
        prompt.Append(contexto);
        prompt.Append("\nInforme: ").Append(inf.Titulo);
        if (!string.IsNullOrWhiteSpace(inf.Periodo))
            prompt.Append("\nPeriodo del informe: ").Append(inf.Periodo);
        prompt.Append("\nSeccion a redactar: ").Append(seccion.Titulo);
        prompt.Append("\n\nInstruccion para esta seccion:\n");
        prompt.Append(string.IsNullOrWhiteSpace(seccion.Prompt)
            ? $"Redacta la seccion \"{seccion.Titulo}\" del informe de gestion."
            : seccion.Prompt!.Trim());

        try
        {
            var res = await _inference.TestChatAsync(
                agentId.Value,
                new[] { new AiChatTurn("user", prompt.ToString()) },
                systemPromptOverride: SystemPromptInforme,
                ct: ct);

            if (!res.Ok)
            {
                _logger.LogWarning("[Informes] IA no disponible al generar seccion {Seccion}: {Error}", seccion.Titulo, res.Error);
                return false;
            }
            seccion.Contenido = (res.Text ?? string.Empty).Trim();
            seccion.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Informes] Error generando seccion {Seccion}", seccion.Titulo);
            return false;
        }
    }

    /// <summary>Contexto de la copropiedad para inyectar en los prompts de generacion.</summary>
    private async Task<string> ContextoCopropiedadAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        if (_tenant.CurrentTenantId is Guid tid)
        {
            var t = await _db.Tenants.AsNoTracking()
                .Where(x => x.Id == tid)
                .Select(x => new { x.Nombre, x.Ciudad })
                .FirstOrDefaultAsync(ct);
            if (t is not null)
            {
                sb.Append("Copropiedad: ").Append(t.Nombre);
                if (!string.IsNullOrWhiteSpace(t.Ciudad)) sb.Append(" (").Append(t.Ciudad).Append(')');
            }
        }
        if (sb.Length == 0) sb.Append("Copropiedad en Colombia.");
        return sb.ToString();
    }

    // =====================================================================
    // Mapeos
    // =====================================================================

    private static InformePlantillaDto MapPlantilla(InformePlantilla p) => new(
        p.Id,
        p.Nombre,
        p.Descripcion,
        p.Secciones.Count,
        p.Secciones.OrderBy(s => s.Orden)
            .Select(s => new InformePlantillaSeccionDto(s.Id, s.Titulo, s.Orden, s.Prompt))
            .ToList());

    private static InformeDetalleDto MapInforme(Informe i) => new(
        i.Id,
        i.PlantillaId,
        i.Titulo,
        i.Periodo,
        i.Estado,
        i.GeneradoEn,
        i.Secciones.OrderBy(s => s.Orden)
            .Select(s => new InformeSeccionDto(s.Id, s.Titulo, s.Orden, s.Prompt, s.Contenido))
            .ToList());
}
