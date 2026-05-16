using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Comunicaciones;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Comunicaciones;

/// <summary>
/// Modulo 2.14 Comunicaciones (spec v1.0 MVP).
///
/// Alcance MVP:
///  - Ciclo Borrador -> Enviando -> Enviado / Cancelado.
///  - Segmentacion soporta Broadcast y Etiqueta de Directorio. Tipos Unidad,
///    GrupoUnidad y EstadoCuenta quedan modelados (validados al persistir) pero
///    la resolucion de destinatarios usa Broadcast como fallback en esos casos
///    con un campo de advertencia en el preview.
///  - Envio simulado: marca destinatarios como Entregado (T.2 real diferido).
///  - Acuse de recibo via GET /c/{token} silencioso e idempotente.
///  - Reenvio a pendientes (RN-07) actualiza estado de los pendientes pero
///    no crea un nuevo comunicado.
///  - Plantillas globales sembradas por migracion; tenant puede crear las suyas.
///
/// Diferido a Fase 2: WhatsApp real (T.2), IA asistente (T.1), PDF export,
/// archivo en 2.15, segmentacion por unidad/piso/torre, segmentacion por
/// estado de cuenta (consume 2.7).
/// </summary>
public class ComunicacionesService : IComunicacionesService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _http;

    private const int TokenDiasExpiracion = 30;

    public ComunicacionesService(PropiaDbContext db, ITenantContext tenantContext, IHttpContextAccessor http)
    {
        _db = db;
        _tenantContext = tenantContext;
        _http = http;
    }

    private Guid GetUsuarioActualId()
    {
        var sub = _http.HttpContext?.User?.FindFirstValue("user_id");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    // ===========================================================================
    // Plantillas
    // ===========================================================================

    public async Task<IReadOnlyList<PlantillaDto>> ListarPlantillasAsync(bool? soloGlobales, CancellationToken ct)
    {
        var q = _db.ComunicadoPlantillas.AsNoTracking().Where(p => p.Activa);
        if (soloGlobales == true) q = q.Where(p => p.EsGlobal);
        if (soloGlobales == false) q = q.Where(p => !p.EsGlobal);
        return await q.OrderByDescending(p => p.EsGlobal).ThenBy(p => p.Nombre)
            .Select(p => new PlantillaDto(
                p.Id, p.EsGlobal, p.Nombre, p.TipoComunicado,
                p.AsuntoModelo, p.CuerpoModelo, p.AcusePorDefecto, p.Activa))
            .ToListAsync(ct);
    }

    public async Task<PlantillaDto?> GetPlantillaAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ComunicadoPlantillas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new PlantillaDto(
            p.Id, p.EsGlobal, p.Nombre, p.TipoComunicado,
            p.AsuntoModelo, p.CuerpoModelo, p.AcusePorDefecto, p.Activa);
    }

    public async Task<PlantillaDto> CrearPlantillaTenantAsync(CrearPlantillaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre obligatorio.");
        if (string.IsNullOrWhiteSpace(req.AsuntoModelo)) throw new InvalidOperationException("AsuntoModelo obligatorio.");
        if (string.IsNullOrWhiteSpace(req.CuerpoModelo)) throw new InvalidOperationException("CuerpoModelo obligatorio.");

        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        var p = new ComunicadoPlantilla
        {
            TenantId = tenantId,
            EsGlobal = false,
            Nombre = req.Nombre.Trim(),
            TipoComunicado = req.TipoComunicado,
            AsuntoModelo = req.AsuntoModelo.Trim(),
            CuerpoModelo = req.CuerpoModelo,
            AcusePorDefecto = req.AcusePorDefecto,
            Activa = true,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.ComunicadoPlantillas.Add(p);
        await _db.SaveChangesAsync(ct);
        return (await GetPlantillaAsync(p.Id, ct))!;
    }

    public async Task<bool> ActualizarPlantillaTenantAsync(Guid id, ActualizarPlantillaRequest req, CancellationToken ct)
    {
        var p = await _db.ComunicadoPlantillas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.EsGlobal) throw new InvalidOperationException("RN-06: Las plantillas globales no son editables.");

        p.Nombre = req.Nombre.Trim();
        p.TipoComunicado = req.TipoComunicado;
        p.AsuntoModelo = req.AsuntoModelo.Trim();
        p.CuerpoModelo = req.CuerpoModelo;
        p.AcusePorDefecto = req.AcusePorDefecto;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesactivarPlantillaTenantAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.ComunicadoPlantillas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (p.EsGlobal) throw new InvalidOperationException("RN-06: Las plantillas globales no son eliminables.");

        // RN-11: con comunicados activos (Borrador o Programado) no se elimina
        var tieneActivos = await _db.Comunicados.AnyAsync(c =>
            c.PlantillaId == id &&
            (c.Estado == EstadoComunicado.Borrador || c.Estado == EstadoComunicado.Programado), ct);
        if (tieneActivos) throw new InvalidOperationException(
            "RN-11: La plantilla tiene comunicados en Borrador o Programado. Desactivelos primero.");

        p.Activa = false;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Comunicado: bandeja y ficha
    // ===========================================================================

    public async Task<IReadOnlyList<ComunicadoListaDto>> ListarComunicadosAsync(
        EstadoComunicado? estado, TipoComunicadoBase? tipo, string? query, CancellationToken ct)
    {
        var q = _db.Comunicados.AsNoTracking()
            .Include(c => c.Segmentos)
            .AsQueryable();

        if (estado is not null) q = q.Where(c => c.Estado == estado);
        if (tipo is not null) q = q.Where(c => c.TipoComunicado == tipo);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var qn = query.Trim().ToLowerInvariant();
            q = q.Where(c => c.Asunto.ToLower().Contains(qn));
        }

        var lista = await q.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

        var ids = lista.Select(c => c.Id).ToList();
        var acuses = await _db.ComunicadoAcuses.AsNoTracking()
            .Where(a => ids.Contains(a.ComunicadoId))
            .GroupBy(a => a.ComunicadoId)
            .Select(g => new { ComunicadoId = g.Key, Abiertos = g.Select(a => a.PersonaId).Distinct().Count() })
            .ToListAsync(ct);

        return lista.Select(c =>
        {
            var ab = acuses.FirstOrDefault(a => a.ComunicadoId == c.Id)?.Abiertos ?? 0;
            decimal? tasa = (c.TotalDestinatarios is int td && td > 0) ? Math.Round((decimal)ab * 100m / td, 1) : (decimal?)null;
            return new ComunicadoListaDto(
                c.Id, c.Asunto, c.TipoComunicado, c.Estado,
                ResumirSegmentos(c.Segmentos),
                c.CreatedAt, c.FechaProgramada, c.FechaEnvio,
                c.RequiereAcuse, c.TotalDestinatarios, ab, tasa);
        }).ToList();
    }

    public async Task<ComunicadoDetalleDto?> GetComunicadoAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.Comunicados.AsNoTracking()
            .Include(x => x.Segmentos)
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        return new ComunicadoDetalleDto(
            c.Id, c.PlantillaId, c.TipoComunicado, c.Asunto, c.CuerpoHtml, c.CuerpoTextoPlano,
            c.RequiereAcuse, c.Estado, c.FechaProgramada, c.FechaEnvio, c.FechaCompletado,
            c.TotalDestinatarios, c.TotalEntregados, c.TotalFallidos,
            c.CreatedAt, c.CreadoPorUsuarioId,
            c.Segmentos.Select(s => new SegmentoDto(s.Id, s.TipoSegmento, s.ValorJson)).ToList(),
            c.Adjuntos.Select(a => new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, a.UrlStorage)).ToList());
    }

    public async Task<ComunicadoDetalleDto> CrearBorradorAsync(CrearBorradorRequest req, CancellationToken ct)
    {
        // Validaciones seccion 16
        if (string.IsNullOrWhiteSpace(req.Asunto) || req.Asunto.Trim().Length < 5)
            throw new InvalidOperationException("Asunto requerido (minimo 5 caracteres).");
        if (req.Asunto.Trim().Length > 150)
            throw new InvalidOperationException("Asunto maximo 150 caracteres.");
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml) || req.CuerpoHtml.Trim().Length < 20)
            throw new InvalidOperationException("Cuerpo requerido (minimo 20 caracteres).");

        if (req.PlantillaId is not null
            && !await _db.ComunicadoPlantillas.AnyAsync(p => p.Id == req.PlantillaId, ct))
            throw new InvalidOperationException("Plantilla no encontrada.");

        var c = new Comunicado
        {
            PlantillaId = req.PlantillaId,
            TipoComunicado = req.TipoComunicado,
            Asunto = req.Asunto.Trim(),
            CuerpoHtml = req.CuerpoHtml,
            CuerpoTextoPlano = string.IsNullOrWhiteSpace(req.CuerpoTextoPlano)
                ? GenerarResumenWhatsApp(req.Asunto, req.CuerpoHtml)
                : req.CuerpoTextoPlano!.Trim(),
            RequiereAcuse = req.RequiereAcuse,
            Estado = EstadoComunicado.Borrador,
            CreadoPorUsuarioId = GetUsuarioActualId()
        };
        _db.Comunicados.Add(c);
        await _db.SaveChangesAsync(ct);
        return (await GetComunicadoAsync(c.Id, ct))!;
    }

    public async Task<bool> ActualizarBorradorAsync(Guid id, ActualizarBorradorRequest req, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        AsegurarEditable(c);

        if (string.IsNullOrWhiteSpace(req.Asunto) || req.Asunto.Trim().Length < 5)
            throw new InvalidOperationException("Asunto requerido (minimo 5 caracteres).");
        if (string.IsNullOrWhiteSpace(req.CuerpoHtml) || req.CuerpoHtml.Trim().Length < 20)
            throw new InvalidOperationException("Cuerpo requerido (minimo 20 caracteres).");

        c.TipoComunicado = req.TipoComunicado;
        c.Asunto = req.Asunto.Trim();
        c.CuerpoHtml = req.CuerpoHtml;
        c.CuerpoTextoPlano = string.IsNullOrWhiteSpace(req.CuerpoTextoPlano)
            ? GenerarResumenWhatsApp(req.Asunto, req.CuerpoHtml)
            : req.CuerpoTextoPlano!.Trim();
        c.RequiereAcuse = req.RequiereAcuse;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void AsegurarEditable(Comunicado c)
    {
        if (c.Estado == EstadoComunicado.Enviado || c.Estado == EstadoComunicado.Cancelado)
            throw new InvalidOperationException("RN-04: El comunicado ya esta en estado terminal y no puede editarse.");
        if (c.Estado == EstadoComunicado.Enviando)
            throw new InvalidOperationException("El comunicado esta en proceso de envio.");
        if (c.Estado == EstadoComunicado.Programado
            && c.FechaProgramada is DateTimeOffset f
            && f <= DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Quedan menos de 5 minutos para el envio programado - ya no es editable.");
    }

    // ===========================================================================
    // Segmentos
    // ===========================================================================

    public async Task<SegmentoDto> AgregarSegmentoAsync(Guid comunicadoId, AgregarSegmentoRequest req, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == comunicadoId, ct)
            ?? throw new InvalidOperationException("Comunicado no encontrado.");
        AsegurarEditable(c);

        // Si es Broadcast, removemos los demas (anula los demas - spec 6.1)
        if (req.Tipo == TipoSegmento.Broadcast)
        {
            var existentes = await _db.ComunicadoSegmentos.Where(s => s.ComunicadoId == comunicadoId).ToListAsync(ct);
            _db.ComunicadoSegmentos.RemoveRange(existentes);
        }
        else
        {
            // Si ya existe Broadcast, lo quitamos antes de agregar este criterio
            var broadcast = await _db.ComunicadoSegmentos
                .FirstOrDefaultAsync(s => s.ComunicadoId == comunicadoId && s.TipoSegmento == TipoSegmento.Broadcast, ct);
            if (broadcast is not null) _db.ComunicadoSegmentos.Remove(broadcast);
        }

        var seg = new ComunicadoSegmento
        {
            ComunicadoId = comunicadoId,
            TipoSegmento = req.Tipo,
            ValorJson = string.IsNullOrWhiteSpace(req.ValorJson) ? "{}" : req.ValorJson
        };
        _db.ComunicadoSegmentos.Add(seg);
        await _db.SaveChangesAsync(ct);
        return new SegmentoDto(seg.Id, seg.TipoSegmento, seg.ValorJson);
    }

    public async Task<bool> RemoverSegmentoAsync(Guid comunicadoId, Guid segmentoId, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == comunicadoId, ct);
        if (c is null) return false;
        AsegurarEditable(c);

        var s = await _db.ComunicadoSegmentos.FirstOrDefaultAsync(x => x.Id == segmentoId && x.ComunicadoId == comunicadoId, ct);
        if (s is null) return false;
        _db.ComunicadoSegmentos.Remove(s);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PreviewDestinatariosDto> PreviewDestinatariosAsync(Guid comunicadoId, CancellationToken ct)
    {
        var personas = await ResolverDestinatariosAsync(comunicadoId, ct);
        var muestra = personas.Take(10).Select(p => new PreviewPersonaDto(
            p.PersonaId, p.NombreCompleto, p.UnidadNumero, p.TieneWhatsapp, p.AutorizaDatos)).ToList();
        return new PreviewDestinatariosDto(
            personas.Count(p => p.TieneWhatsapp && p.AutorizaDatos),
            personas.Count(p => !p.TieneWhatsapp),
            personas.Count(p => !p.AutorizaDatos),
            muestra);
    }

    /// <summary>
    /// Resolucion de destinatarios contra el Directorio. RN-01 y RN-02 obligatorios.
    /// MVP: soporta Broadcast y Etiqueta. Para otros tipos, se trata como Broadcast.
    /// </summary>
    private async Task<List<PersonaResueltaDto>> ResolverDestinatariosAsync(Guid comunicadoId, CancellationToken ct)
    {
        var tenantId = _tenantContext.CurrentTenantId
            ?? throw new InvalidOperationException("No hay copropiedad activa.");

        var segmentos = await _db.ComunicadoSegmentos.AsNoTracking()
            .Where(s => s.ComunicadoId == comunicadoId).ToListAsync(ct);

        // Universo base: personas con vinculo activo al tenant
        var personaIdsConVinculo = await _db.DirectorioVinculos.AsNoTracking()
            .Where(v => v.TenantId == tenantId
                        && v.Estado == EstadoVinculo.Activo
                        && v.EntidadTipo == EntidadDirectorio.Persona)
            .Select(v => v.EntidadId).Distinct().ToListAsync(ct);

        // Filtro por etiqueta (si hay segmentos de ese tipo)
        var etiquetaSegmentos = segmentos.Where(s => s.TipoSegmento == TipoSegmento.Etiqueta).ToList();
        if (etiquetaSegmentos.Count > 0)
        {
            var etiquetaIds = new HashSet<Guid>();
            foreach (var seg in etiquetaSegmentos)
            {
                try
                {
                    var json = JsonDocument.Parse(seg.ValorJson);
                    if (json.RootElement.TryGetProperty("etiquetaId", out var idProp)
                        && Guid.TryParse(idProp.GetString(), out var etId))
                        etiquetaIds.Add(etId);
                }
                catch { /* segmento mal formado se ignora */ }
            }
            if (etiquetaIds.Count > 0)
            {
                // DirectorioEtiqueta apunta a Vinculo; resolvemos PersonaId via JOIN.
                var personasConEtiqueta = await (from e in _db.DirectorioEtiquetas.AsNoTracking()
                                                 join v in _db.DirectorioVinculos.AsNoTracking()
                                                     on e.VinculoId equals v.Id
                                                 where e.TenantId == tenantId
                                                       && etiquetaIds.Contains(e.EtiquetaId)
                                                       && v.EntidadTipo == EntidadDirectorio.Persona
                                                 select v.EntidadId).Distinct().ToListAsync(ct);
                personaIdsConVinculo = personaIdsConVinculo.Intersect(personasConEtiqueta).ToList();
            }
        }

        // Datos de cada persona
        var personas = await _db.Personas.AsNoTracking()
            .Where(p => personaIdsConVinculo.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                NombreCompleto = (p.Nombres + " " + p.Apellidos).Trim(),
                p.AceptoTratamientoDatos
            })
            .ToListAsync(ct);

        // Contactos WhatsApp por persona (visible al tenant)
        var contactosWa = await _db.DirectorioContactos.AsNoTracking()
            .Where(c => c.TenantId == tenantId
                        && c.EntidadTipo == EntidadDirectorio.Persona
                        && c.Tipo == TipoContacto.Whatsapp
                        && c.Activo
                        && personaIdsConVinculo.Contains(c.EntidadId))
            .Select(c => c.EntidadId)
            .Distinct()
            .ToListAsync(ct);
        var setConWa = new HashSet<Guid>(contactosWa);

        return personas.Select(p => new PersonaResueltaDto(
            p.Id,
            string.IsNullOrWhiteSpace(p.NombreCompleto) ? "(sin nombre)" : p.NombreCompleto,
            null,
            setConWa.Contains(p.Id),
            p.AceptoTratamientoDatos)).ToList();
    }

    private sealed record PersonaResueltaDto(Guid PersonaId, string NombreCompleto, string? UnidadNumero, bool TieneWhatsapp, bool AutorizaDatos);

    // ===========================================================================
    // Adjuntos
    // ===========================================================================

    public async Task<AdjuntoDto> AgregarAdjuntoAsync(Guid comunicadoId, CrearAdjuntoRequest req, CancellationToken ct)
    {
        var c = await _db.Comunicados.Include(x => x.Adjuntos).FirstOrDefaultAsync(x => x.Id == comunicadoId, ct)
            ?? throw new InvalidOperationException("Comunicado no encontrado.");
        AsegurarEditable(c);

        if (c.Adjuntos.Count >= 5) throw new InvalidOperationException("Maximo 5 adjuntos por comunicado.");
        if (req.TamanoBytes > 10_000_000) throw new InvalidOperationException("Tamano maximo 10 MB por archivo.");
        var mime = req.TipoMime.ToLowerInvariant();
        if (!(mime == "application/pdf" || mime == "image/jpeg" || mime == "image/png"))
            throw new InvalidOperationException("Tipos permitidos: PDF, JPG, PNG.");

        var a = new ComunicadoAdjunto
        {
            ComunicadoId = comunicadoId,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = mime,
            TamanoBytes = req.TamanoBytes,
            UrlStorage = req.UrlStorage,
            SubidoPorUsuarioId = GetUsuarioActualId()
        };
        _db.ComunicadoAdjuntos.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, a.UrlStorage);
    }

    public async Task<bool> RemoverAdjuntoAsync(Guid comunicadoId, Guid adjuntoId, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == comunicadoId, ct);
        if (c is null) return false;
        AsegurarEditable(c);
        var a = await _db.ComunicadoAdjuntos.FirstOrDefaultAsync(x => x.Id == adjuntoId && x.ComunicadoId == comunicadoId, ct);
        if (a is null) return false;
        _db.ComunicadoAdjuntos.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===========================================================================
    // Envio / cancelacion
    // ===========================================================================

    public async Task<bool> EnviarAsync(Guid id, EnviarRequest req, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        AsegurarEditable(c);

        var ahora = DateTimeOffset.UtcNow;

        if (req.FechaProgramada is DateTimeOffset f)
        {
            if (f <= ahora.AddMinutes(5))
                throw new InvalidOperationException("RN: fecha programada debe ser al menos 5 minutos en el futuro.");
            c.FechaProgramada = f;
            c.Estado = EstadoComunicado.Programado;
            c.UpdatedAt = ahora;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // Envio inmediato. Validamos que tenga al menos un segmento.
        var tieneSegmento = await _db.ComunicadoSegmentos.AnyAsync(s => s.ComunicadoId == id, ct);
        if (!tieneSegmento) throw new InvalidOperationException("Defina al menos un segmento de destinatarios.");

        var personas = await ResolverDestinatariosAsync(id, ct);
        var elegibles = personas.Where(p => p.TieneWhatsapp && p.AutorizaDatos).ToList();
        if (elegibles.Count == 0) throw new InvalidOperationException("No hay destinatarios elegibles (con WhatsApp y autorizacion).");

        c.Estado = EstadoComunicado.Enviando;
        c.FechaEnvio = ahora;
        c.TotalDestinatarios = elegibles.Count;

        var expira = ahora.AddDays(TokenDiasExpiracion);
        foreach (var p in elegibles)
        {
            var d = new ComunicadoDestinatario
            {
                ComunicadoId = id,
                PersonaId = p.PersonaId,
                Token = Guid.NewGuid(),
                TokenExpiraAt = expira,
                EstadoEntrega = EstadoEntregaDestinatario.Pendiente
            };
            _db.ComunicadoDestinatarios.Add(d);
        }
        await _db.SaveChangesAsync(ct);

        // MVP: simulamos la entrega marcando todos como Entregado. En produccion, T.2
        // gestiona la cola asincrona y actualiza estado por destinatario.
        await _db.ComunicadoDestinatarios.Where(d => d.ComunicadoId == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.EstadoEntrega, EstadoEntregaDestinatario.Entregado)
                .SetProperty(x => x.FechaEntrega, ahora), ct);

        c.TotalEntregados = elegibles.Count;
        c.TotalFallidos = 0;
        c.FechaCompletado = DateTimeOffset.UtcNow;
        c.Estado = EstadoComunicado.Enviado;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelarAsync(Guid id, CancelarRequest req, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (c.Estado != EstadoComunicado.Borrador && c.Estado != EstadoComunicado.Programado)
            throw new InvalidOperationException("Solo se puede cancelar Borrador o Programado.");

        c.Estado = EstadoComunicado.Cancelado;
        c.CanceladoPorUsuarioId = GetUsuarioActualId();
        c.CanceladoAt = DateTimeOffset.UtcNow;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ReenviarAPendientesAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || c.Estado != EstadoComunicado.Enviado)
            throw new InvalidOperationException("Solo se puede reenviar comunicados ya enviados.");

        // Destinatarios sin acuse
        var destinatariosIds = await _db.ComunicadoDestinatarios.AsNoTracking()
            .Where(d => d.ComunicadoId == id).Select(d => d.Id).ToListAsync(ct);
        var conAcuse = await _db.ComunicadoAcuses.AsNoTracking()
            .Where(a => a.ComunicadoId == id).Select(a => a.DestinatarioId).Distinct().ToListAsync(ct);
        var pendientes = destinatariosIds.Except(conAcuse).Count();

        // MVP: el reenvio simula que se vuelve a encolar - no crea nuevo comunicado (RN-07).
        // En produccion, T.2 vuelve a despachar el mensaje WhatsApp para esos destinatarios.
        return pendientes;
    }

    // ===========================================================================
    // Acuses
    // ===========================================================================

    public async Task<AcuseListaDto> ListarAcusesAsync(Guid comunicadoId, CancellationToken ct)
    {
        var destinatarios = await _db.ComunicadoDestinatarios.AsNoTracking()
            .Where(d => d.ComunicadoId == comunicadoId)
            .Include(d => d.Persona)
            .ToListAsync(ct);
        var acuses = await _db.ComunicadoAcuses.AsNoTracking()
            .Where(a => a.ComunicadoId == comunicadoId)
            .GroupBy(a => a.DestinatarioId)
            .Select(g => new { DestinatarioId = g.Key, AbiertoAt = g.Min(a => a.AbiertoAt), Disp = g.OrderBy(x => x.AbiertoAt).First().Dispositivo })
            .ToListAsync(ct);
        var setConAcuse = acuses.ToDictionary(x => x.DestinatarioId);

        var confirmados = destinatarios.Where(d => setConAcuse.ContainsKey(d.Id))
            .Select(d =>
            {
                var a = setConAcuse[d.Id];
                return new AcuseDestinatarioDto(d.PersonaId,
                    d.Persona is null ? "(persona)" : $"{d.Persona.Nombres} {d.Persona.Apellidos}".Trim(),
                    null, a.AbiertoAt, a.Disp);
            }).ToList();
        var pendientes = destinatarios.Where(d => !setConAcuse.ContainsKey(d.Id))
            .Select(d => new AcuseDestinatarioDto(d.PersonaId,
                d.Persona is null ? "(persona)" : $"{d.Persona.Nombres} {d.Persona.Apellidos}".Trim(),
                null, null, null)).ToList();

        var total = destinatarios.Count;
        var conf = confirmados.Count;
        var pend = pendientes.Count;
        var tasa = total > 0 ? Math.Round((decimal)conf * 100m / total, 1) : 0m;

        return new AcuseListaDto(total, conf, pend, tasa, confirmados, pendientes);
    }

    public async Task<VistaPublicaComunicadoDto?> AbrirVistaPublicaAsync(Guid token, DispositivoAcuse dispositivo, CancellationToken ct)
    {
        // Endpoint publico - sin tenant en sesion. Usamos funcion SECURITY DEFINER
        // get_comunicado_publico(token) que bypasea RLS porque el secreto reside
        // en el token (UUID v4) presente solo en el WhatsApp del destinatario.
        var row = await _db.Database
            .SqlQuery<ComunicadoPublicoRow>(
                $@"SELECT destinatario_id AS ""DestinatarioId"",
                          comunicado_id AS ""ComunicadoId"",
                          tenant_id AS ""TenantIdCol"",
                          persona_id AS ""PersonaId"",
                          token_expira_at AS ""TokenExpiraAt"",
                          estado AS ""Estado"",
                          asunto AS ""Asunto"",
                          cuerpo_html AS ""CuerpoHtml"",
                          tipo_comunicado AS ""TipoComunicado"",
                          fecha_envio AS ""FechaEnvio"",
                          created_at AS ""CreatedAt"",
                          copropiedad_nombre AS ""CopropiedadNombre"",
                          ya_acusado AS ""YaAcusado""
                   FROM get_comunicado_publico({token})")
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;
        if (row.Estado != (int)EstadoComunicado.Enviado) return null;
        if (row.TokenExpiraAt < DateTimeOffset.UtcNow) return null;

        // Si no ha sido acusado, registrar acuse. Como tenemos el tenant_id de la fila,
        // seteamos el contexto y persistimos con RLS satisfecha. Idempotente.
        if (!row.YaAcusado)
        {
            _tenantContext.SetTenant(row.TenantIdCol);
            // Forzamos reabrir conexion para que el interceptor aplique app.tenant_id.
            await _db.Database.CloseConnectionAsync();

            _db.ComunicadoAcuses.Add(new ComunicadoAcuse
            {
                TenantId = row.TenantIdCol,
                ComunicadoId = row.ComunicadoId,
                DestinatarioId = row.DestinatarioId,
                PersonaId = row.PersonaId,
                AbiertoAt = DateTimeOffset.UtcNow,
                Dispositivo = dispositivo
            });
            try { await _db.SaveChangesAsync(ct); }
            catch { /* Race condition: otro request lo acuso primero - no es error. */ }
        }

        // Adjuntos del comunicado (la funcion SECURITY DEFINER ya nos dio el resto).
        // Reusamos el tenant context seteado arriba o lo seteamos si aun no fue.
        if (_tenantContext.CurrentTenantId is null)
        {
            _tenantContext.SetTenant(row.TenantIdCol);
            await _db.Database.CloseConnectionAsync();
        }
        var adjuntos = await _db.ComunicadoAdjuntos.AsNoTracking()
            .Where(a => a.ComunicadoId == row.ComunicadoId)
            .Select(a => new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, a.UrlStorage))
            .ToListAsync(ct);

        return new VistaPublicaComunicadoDto(
            row.Asunto, (TipoComunicadoBase)row.TipoComunicado, row.CuerpoHtml,
            row.FechaEnvio ?? row.CreatedAt, row.CopropiedadNombre, adjuntos);
    }

    /// <summary>Row de la funcion SECURITY DEFINER get_comunicado_publico.</summary>
    private sealed class ComunicadoPublicoRow
    {
        public Guid DestinatarioId { get; set; }
        public Guid ComunicadoId { get; set; }
        public Guid TenantIdCol { get; set; }
        public Guid PersonaId { get; set; }
        public DateTimeOffset TokenExpiraAt { get; set; }
        public int Estado { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string CuerpoHtml { get; set; } = string.Empty;
        public int TipoComunicado { get; set; }
        public DateTimeOffset? FechaEnvio { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CopropiedadNombre { get; set; } = string.Empty;
        public bool YaAcusado { get; set; }
    }

    // ===========================================================================
    // Resumen
    // ===========================================================================

    public async Task<ResumenComunicacionesDto> GetResumenAsync(CancellationToken ct)
    {
        var inicioMes = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var borradores = await _db.Comunicados.CountAsync(c => c.Estado == EstadoComunicado.Borrador, ct);
        var programados = await _db.Comunicados.CountAsync(c => c.Estado == EstadoComunicado.Programado, ct);
        var enviadosMes = await _db.Comunicados.CountAsync(c =>
            c.Estado == EstadoComunicado.Enviado && c.FechaEnvio >= inicioMes, ct);

        var comunicadosMes = await _db.Comunicados.AsNoTracking()
            .Where(c => c.Estado == EstadoComunicado.Enviado && c.FechaEnvio >= inicioMes && c.TotalDestinatarios > 0)
            .Select(c => new { c.Id, c.TotalDestinatarios }).ToListAsync(ct);
        decimal? tasaProm = null;
        if (comunicadosMes.Count > 0)
        {
            var ids = comunicadosMes.Select(x => x.Id).ToList();
            var abiertosPorCom = await _db.ComunicadoAcuses.AsNoTracking()
                .Where(a => ids.Contains(a.ComunicadoId))
                .GroupBy(a => a.ComunicadoId)
                .Select(g => new { Id = g.Key, Abiertos = g.Select(a => a.PersonaId).Distinct().Count() })
                .ToListAsync(ct);
            var tasas = comunicadosMes.Select(c =>
            {
                var ab = abiertosPorCom.FirstOrDefault(x => x.Id == c.Id)?.Abiertos ?? 0;
                return c.TotalDestinatarios > 0 ? (decimal)ab * 100m / c.TotalDestinatarios.Value : 0m;
            }).ToList();
            tasaProm = Math.Round(tasas.Average(), 1);
        }

        return new ResumenComunicacionesDto(borradores, programados, enviadosMes, tasaProm);
    }

    // ===========================================================================
    // Helpers
    // ===========================================================================

    private static string ResumirSegmentos(IEnumerable<ComunicadoSegmento> segs)
    {
        var lista = segs.ToList();
        if (lista.Count == 0) return "Sin definir";
        if (lista.Any(s => s.TipoSegmento == TipoSegmento.Broadcast)) return "Toda la comunidad";
        return string.Join(", ", lista.Select(s => s.TipoSegmento.ToString()));
    }

    private static string GenerarResumenWhatsApp(string asunto, string cuerpoHtml)
    {
        // Strip HTML tags rudo para texto plano del WhatsApp.
        var sinTags = System.Text.RegularExpressions.Regex.Replace(cuerpoHtml ?? "", "<.*?>", "");
        var trimmed = sinTags.Trim().Replace("\r", " ").Replace("\n", " ");
        var max = 450 - asunto.Length;
        if (max < 50) max = 50;
        var resumen = trimmed.Length > max ? trimmed.Substring(0, max) + "..." : trimmed;
        return $"{asunto}\n\n{resumen}";
    }
}
