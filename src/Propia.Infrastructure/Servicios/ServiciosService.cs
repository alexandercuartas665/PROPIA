using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Servicios;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.Servicios;

/// <summary>
/// Implementacion de IServiciosService. Servicio + contactos + adjuntos (servicio y contrato).
/// Los adjuntos se guardan en IBlobStorage con key tenants/{tenant}/{tipo}/{id}/{guid}{ext}.
/// </summary>
public class ServiciosService : IServiciosService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBlobStorage _blob;

    private static readonly HashSet<string> ExtPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".doc", ".docx", ".xls", ".xlsx"
    };
    private const long MaxBytes = 20L * 1024 * 1024; // 20 MB
    private const string ModuloServicio = "servicio"; // ModuloOrigenCodigo de las alertas de servicio

    public ServiciosService(PropiaDbContext db, ITenantContext tenant, IBlobStorage blob)
    {
        _db = db;
        _tenant = tenant;
        _blob = blob;
    }

    // ----------------------------- Servicio CRUD -----------------------------

    public async Task<IReadOnlyList<ServicioDto>> ListarAsync(CancellationToken ct)
    {
        var servicios = await _db.Servicios
            .AsNoTracking()
            .Select(s => new
            {
                s.Id, s.Tipo, s.Nombre, s.Descripcion,
                s.EjecutorPersonaId, s.EjecutorEmpresaId, s.EjecutorNombre,
                s.CostoMensual, s.CostoAnual, s.Estado,
                Adjuntos = s.Adjuntos.Count,
                Contactos = s.Contactos.Count
            })
            .OrderBy(s => s.Nombre)
            .ToListAsync(ct);

        // Conteo de contratos por servicio en una sola consulta (evita subconsulta no traducible en la proyeccion).
        var contratoCounts = await _db.ContratosServicio
            .Where(c => c.ServicioId != null)
            .GroupBy(c => c.ServicioId!.Value)
            .Select(g => new { ServicioId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var map = contratoCounts.ToDictionary(x => x.ServicioId, x => x.Count);

        return servicios.Select(s => new ServicioDto(
            s.Id, s.Tipo, s.Nombre, s.Descripcion,
            s.EjecutorPersonaId, s.EjecutorEmpresaId, s.EjecutorNombre,
            s.CostoMensual, s.CostoAnual, s.Estado,
            map.TryGetValue(s.Id, out var cc) ? cc : 0,
            s.Adjuntos, s.Contactos)).ToList();
    }

    public async Task<ServicioDetalleDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Servicios
            .AsNoTracking()
            .Include(x => x.Contactos)
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;

        var dto = new ServicioDto(
            s.Id, s.Tipo, s.Nombre, s.Descripcion,
            s.EjecutorPersonaId, s.EjecutorEmpresaId, s.EjecutorNombre,
            s.CostoMensual, s.CostoAnual, s.Estado,
            await _db.ContratosServicio.CountAsync(c => c.ServicioId == s.Id, ct),
            s.Adjuntos.Count, s.Contactos.Count);

        var contactos = s.Contactos
            .Select(c => new ServicioContactoDto(c.Id, c.PersonaId, c.EmpresaId, c.NombreSnapshot, c.Rol, c.Telefono, c.Email))
            .ToList();

        var adjuntos = s.Adjuntos
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, _blob.GetPublicUrl(a.UrlStorage), a.CreatedAt))
            .ToList();

        var contratos = await _db.ContratosServicio
            .AsNoTracking()
            .Where(c => c.ServicioId == s.Id)
            .OrderByDescending(c => c.FechaInicio)
            .Select(c => new ContratoResumenDto(
                c.Id, c.Tipo, c.Proveedor, c.FechaInicio, c.FechaFin, c.ValorMensual,
                c.Estado, c.RenovacionAutomatica, c.Adjuntos.Count))
            .ToListAsync(ct);

        var alertas = await _db.AlertasCopropiedad
            .AsNoTracking()
            .Where(a => a.ModuloOrigenCodigo == ModuloServicio && a.EntidadId == s.Id)
            .OrderByDescending(a => a.Activa).ThenByDescending(a => a.CreatedAt)
            .Select(a => new AlertaServicioDto(a.Id, a.Titulo, a.Descripcion, a.Severidad, a.Activa, a.CreatedAt))
            .ToListAsync(ct);

        return new ServicioDetalleDto(dto, contactos, adjuntos, contratos, alertas);
    }

    public async Task<ServicioDto> CrearAsync(CrearServicioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del servicio obligatorio.");

        var s = new Servicio
        {
            Tipo = req.Tipo,
            Nombre = req.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            EjecutorPersonaId = req.EjecutorPersonaId,
            EjecutorEmpresaId = req.EjecutorEmpresaId,
            EjecutorNombre = string.IsNullOrWhiteSpace(req.EjecutorNombre) ? null : req.EjecutorNombre.Trim(),
            CostoMensual = req.CostoMensual,
            CostoAnual = req.CostoAnual,
            Estado = EstadoServicio.Activo
        };
        _db.Servicios.Add(s);
        await _db.SaveChangesAsync(ct);
        return new ServicioDto(s.Id, s.Tipo, s.Nombre, s.Descripcion,
            s.EjecutorPersonaId, s.EjecutorEmpresaId, s.EjecutorNombre,
            s.CostoMensual, s.CostoAnual, s.Estado, 0, 0, 0);
    }

    public async Task<bool> ActualizarAsync(Guid id, ActualizarServicioRequest req, CancellationToken ct)
    {
        var s = await _db.Servicios.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del servicio obligatorio.");

        s.Tipo = req.Tipo;
        s.Nombre = req.Nombre.Trim();
        s.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        s.EjecutorPersonaId = req.EjecutorPersonaId;
        s.EjecutorEmpresaId = req.EjecutorEmpresaId;
        s.EjecutorNombre = string.IsNullOrWhiteSpace(req.EjecutorNombre) ? null : req.EjecutorNombre.Trim();
        s.CostoMensual = req.CostoMensual;
        s.CostoAnual = req.CostoAnual;
        s.Estado = req.Estado;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct)
    {
        var s = await _db.Servicios
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;

        // Desvincular contratos (no se borran; el contrato es independiente).
        var contratos = await _db.ContratosServicio.Where(c => c.ServicioId == id).ToListAsync(ct);
        foreach (var c in contratos) c.ServicioId = null;

        // Resolver alertas activas del servicio para que no queden huerfanas en el dashboard.
        var alertas = await _db.AlertasCopropiedad
            .Where(a => a.ModuloOrigenCodigo == ModuloServicio && a.EntidadId == id && a.Activa).ToListAsync(ct);
        foreach (var al in alertas) { al.Activa = false; al.ResueltaAt = DateTimeOffset.UtcNow; }

        // Borrar blobs de adjuntos del servicio.
        foreach (var a in s.Adjuntos)
            await _blob.DeleteAsync(a.UrlStorage, ct);

        _db.Servicios.Remove(s); // cascade borra contactos y adjuntos
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Contactos -----------------------------

    public async Task<ServicioContactoDto> AgregarContactoAsync(Guid servicioId, AgregarServicioContactoRequest req, CancellationToken ct)
    {
        var existe = await _db.Servicios.AnyAsync(x => x.Id == servicioId, ct);
        if (!existe) throw new InvalidOperationException("Servicio no encontrado.");
        if (string.IsNullOrWhiteSpace(req.NombreSnapshot))
            throw new InvalidOperationException("Nombre del contacto obligatorio.");

        var c = new ServicioContacto
        {
            ServicioId = servicioId,
            PersonaId = req.PersonaId,
            EmpresaId = req.EmpresaId,
            NombreSnapshot = req.NombreSnapshot.Trim(),
            Rol = string.IsNullOrWhiteSpace(req.Rol) ? null : req.Rol.Trim(),
            Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim()
        };
        _db.ServicioContactos.Add(c);
        await _db.SaveChangesAsync(ct);
        return new ServicioContactoDto(c.Id, c.PersonaId, c.EmpresaId, c.NombreSnapshot, c.Rol, c.Telefono, c.Email);
    }

    public async Task<bool> EliminarContactoAsync(Guid contactoId, CancellationToken ct)
    {
        var c = await _db.ServicioContactos.FirstOrDefaultAsync(x => x.Id == contactoId, ct);
        if (c is null) return false;
        _db.ServicioContactos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Adjuntos de servicio -----------------------------

    public async Task<AdjuntoDto> SubirAdjuntoServicioAsync(Guid servicioId, SubirAdjuntoRequest req, Guid? usuarioId, CancellationToken ct)
    {
        var existe = await _db.Servicios.AnyAsync(x => x.Id == servicioId, ct);
        if (!existe) throw new InvalidOperationException("Servicio no encontrado.");

        var (bytes, ext, mime) = ValidarYDecodificar(req);
        var key = $"tenants/{TenantKey()}/servicios/{servicioId}/{Guid.NewGuid():N}{ext}";
        await using (var ms = new MemoryStream(bytes))
            await _blob.UploadAsync(key, ms, mime, ct);

        var a = new ServicioAdjunto
        {
            ServicioId = servicioId,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = mime,
            TamanoBytes = bytes.LongLength,
            UrlStorage = key,
            SubidoPorUsuarioId = usuarioId
        };
        _db.ServicioAdjuntos.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, _blob.GetPublicUrl(a.UrlStorage), a.CreatedAt);
    }

    public async Task<bool> EliminarAdjuntoServicioAsync(Guid adjuntoId, CancellationToken ct)
    {
        var a = await _db.ServicioAdjuntos.FirstOrDefaultAsync(x => x.Id == adjuntoId, ct);
        if (a is null) return false;
        await _blob.DeleteAsync(a.UrlStorage, ct);
        _db.ServicioAdjuntos.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Adjuntos de contrato -----------------------------

    public async Task<AdjuntoDto> SubirAdjuntoContratoAsync(Guid contratoId, SubirAdjuntoRequest req, Guid? usuarioId, CancellationToken ct)
    {
        var existe = await _db.ContratosServicio.AnyAsync(x => x.Id == contratoId, ct);
        if (!existe) throw new InvalidOperationException("Contrato no encontrado.");

        var (bytes, ext, mime) = ValidarYDecodificar(req);
        var key = $"tenants/{TenantKey()}/contratos/{contratoId}/{Guid.NewGuid():N}{ext}";
        await using (var ms = new MemoryStream(bytes))
            await _blob.UploadAsync(key, ms, mime, ct);

        var a = new ContratoAdjunto
        {
            ContratoId = contratoId,
            NombreArchivo = req.NombreArchivo.Trim(),
            TipoMime = mime,
            TamanoBytes = bytes.LongLength,
            UrlStorage = key,
            SubidoPorUsuarioId = usuarioId
        };
        _db.ContratoAdjuntos.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, _blob.GetPublicUrl(a.UrlStorage), a.CreatedAt);
    }

    public async Task<IReadOnlyList<AdjuntoDto>> ListarAdjuntosContratoAsync(Guid contratoId, CancellationToken ct)
    {
        var rows = await _db.ContratoAdjuntos
            .AsNoTracking()
            .Where(a => a.ContratoId == contratoId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(a => new AdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanoBytes, _blob.GetPublicUrl(a.UrlStorage), a.CreatedAt)).ToList();
    }

    public async Task<bool> EliminarAdjuntoContratoAsync(Guid adjuntoId, CancellationToken ct)
    {
        var a = await _db.ContratoAdjuntos.FirstOrDefaultAsync(x => x.Id == adjuntoId, ct);
        if (a is null) return false;
        await _blob.DeleteAsync(a.UrlStorage, ct);
        _db.ContratoAdjuntos.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Asociar contratos -----------------------------

    public async Task<bool> AsociarContratoAsync(Guid servicioId, Guid contratoId, CancellationToken ct)
    {
        var existeServicio = await _db.Servicios.AnyAsync(x => x.Id == servicioId, ct);
        if (!existeServicio) return false;
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        c.ServicioId = servicioId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesasociarContratoAsync(Guid contratoId, CancellationToken ct)
    {
        var c = await _db.ContratosServicio.FirstOrDefaultAsync(x => x.Id == contratoId, ct);
        if (c is null) return false;
        c.ServicioId = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Alertas del servicio -----------------------------

    public async Task<AlertaServicioDto> AgregarAlertaAsync(Guid servicioId, AgregarAlertaServicioRequest req, CancellationToken ct)
    {
        var s = await _db.Servicios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == servicioId, ct)
            ?? throw new InvalidOperationException("Servicio no encontrado.");
        if (string.IsNullOrWhiteSpace(req.Titulo))
            throw new InvalidOperationException("El titulo de la alerta es obligatorio.");

        var a = new AlertaCopropiedad
        {
            Tipo = TipoAlertaDashboard.Otro,
            Severidad = req.Severidad,
            Titulo = req.Titulo.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? $"Servicio: {s.Nombre}" : req.Descripcion.Trim(),
            UrlAccion = "/servicios",
            ModuloOrigenCodigo = ModuloServicio,
            EntidadId = servicioId,
            Activa = true
        };
        _db.AlertasCopropiedad.Add(a);
        await _db.SaveChangesAsync(ct);
        return new AlertaServicioDto(a.Id, a.Titulo, a.Descripcion, a.Severidad, a.Activa, a.CreatedAt);
    }

    public async Task<bool> ResolverAlertaAsync(Guid alertaId, CancellationToken ct)
    {
        var a = await _db.AlertasCopropiedad.FirstOrDefaultAsync(x => x.Id == alertaId && x.ModuloOrigenCodigo == ModuloServicio, ct);
        if (a is null) return false;
        a.Activa = false;
        a.ResueltaAt = DateTimeOffset.UtcNow;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Helpers -----------------------------

    private string TenantKey() => _tenant.CurrentTenantId?.ToString() ?? "global";

    private static (byte[] bytes, string ext, string mime) ValidarYDecodificar(SubirAdjuntoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NombreArchivo))
            throw new InvalidOperationException("Nombre de archivo obligatorio.");

        var ext = Path.GetExtension(req.NombreArchivo);
        if (string.IsNullOrWhiteSpace(ext) || !ExtPermitidas.Contains(ext))
            throw new InvalidOperationException("Formato no soportado. Usa PDF, imagen, Word o Excel.");

        var raw = req.ContenidoBase64 ?? string.Empty;
        var comma = raw.IndexOf(',');
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
            raw = raw[(comma + 1)..];

        byte[] bytes;
        try { bytes = Convert.FromBase64String(raw); }
        catch (FormatException) { throw new InvalidOperationException("Contenido base64 invalido."); }

        if (bytes.LongLength == 0) throw new InvalidOperationException("Archivo vacio.");
        if (bytes.LongLength > MaxBytes) throw new InvalidOperationException("Maximo 20 MB por archivo.");

        var mime = string.IsNullOrWhiteSpace(req.TipoMime) ? MimePorExt(ext) : req.TipoMime;
        return (bytes, ext, mime);
    }

    private static string MimePorExt(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };
}
