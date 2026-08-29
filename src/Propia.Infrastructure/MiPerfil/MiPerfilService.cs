using Microsoft.EntityFrameworkCore;
using Propia.Application.MiPerfil;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Storage;

namespace Propia.Infrastructure.MiPerfil;

/// <summary>
/// Self-service de "Mi Perfil": foto, firma y contactos de notificacion del usuario autenticado.
/// Resuelve la Persona a partir del userId del JWT. Los contactos son globales por persona (sin tenant).
/// </summary>
public class MiPerfilService : IMiPerfilService
{
    private readonly PropiaDbContext _db;
    private readonly IBlobStorage _blob;

    public MiPerfilService(PropiaDbContext db, IBlobStorage blob)
    {
        _db = db;
        _blob = blob;
    }

    private async Task<Persona?> PersonaDeUsuarioAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.Persona;
    }

    public Task<string?> SubirFotoAsync(Guid userId, Stream content, string contentType, string ext, CancellationToken ct)
        => SubirMediaAsync(userId, content, contentType, ext, "avatar", esFirma: false, ct);

    public Task<string?> SubirFirmaAsync(Guid userId, Stream content, string contentType, string ext, CancellationToken ct)
        => SubirMediaAsync(userId, content, contentType, ext, "firma", esFirma: true, ct);

    private async Task<string?> SubirMediaAsync(Guid userId, Stream content, string contentType, string ext, string nombre, bool esFirma, CancellationToken ct)
    {
        var persona = await PersonaDeUsuarioAsync(userId, ct);
        if (persona is null) return null;

        var key = $"personas/{persona.Id}/{nombre}{ext}";
        var url = await _blob.UploadAsync(key, content, contentType, ct);
        if (esFirma) persona.FirmaUrl = url; else persona.FotoUrl = url;
        await _db.SaveChangesAsync(ct);
        return _blob.ResolveUrl(url);
    }

    public async Task<IReadOnlyList<ContactoNotificacionDto>> ListarContactosAsync(Guid userId, CancellationToken ct)
    {
        var persona = await PersonaDeUsuarioAsync(userId, ct);
        if (persona is null) return Array.Empty<ContactoNotificacionDto>();

        return await _db.UsuarioContactosNotificacion
            .Where(c => c.PersonaId == persona.Id)
            .OrderBy(c => c.Canal).ThenBy(c => c.CreatedAt)
            .Select(c => new ContactoNotificacionDto(c.Id, c.Canal, c.Valor, c.Activo))
            .ToListAsync(ct);
    }

    public async Task<ContactoNotificacionDto?> AgregarContactoAsync(Guid userId, CrearContactoNotificacionRequest req, CancellationToken ct)
    {
        var persona = await PersonaDeUsuarioAsync(userId, ct);
        if (persona is null) return null;

        var valor = (req.Valor ?? "").Trim();
        if (valor.Length == 0) throw new InvalidOperationException("El correo o telefono es obligatorio.");
        if (req.Canal != CanalNotificacion.Email && req.Canal != CanalNotificacion.WhatsApp)
            throw new InvalidOperationException("Canal no soportado. Usa correo o WhatsApp.");
        if (req.Canal == CanalNotificacion.Email && (!valor.Contains('@') || !valor.Contains('.')))
            throw new InvalidOperationException("Correo invalido.");

        var ya = await _db.UsuarioContactosNotificacion
            .AnyAsync(c => c.PersonaId == persona.Id && c.Canal == req.Canal && c.Valor == valor, ct);
        if (ya) throw new InvalidOperationException("Ese contacto ya esta registrado.");

        var entidad = new UsuarioContactoNotificacion
        {
            PersonaId = persona.Id,
            Canal = req.Canal,
            Valor = valor,
            Activo = true
        };
        _db.UsuarioContactosNotificacion.Add(entidad);
        await _db.SaveChangesAsync(ct);
        return new ContactoNotificacionDto(entidad.Id, entidad.Canal, entidad.Valor, entidad.Activo);
    }

    public async Task<bool> ActualizarContactoAsync(Guid userId, Guid contactoId, ActualizarContactoNotificacionRequest req, CancellationToken ct)
    {
        var persona = await PersonaDeUsuarioAsync(userId, ct);
        if (persona is null) return false;

        var c = await _db.UsuarioContactosNotificacion
            .FirstOrDefaultAsync(x => x.Id == contactoId && x.PersonaId == persona.Id, ct);
        if (c is null) return false;

        if (req.Valor is { } v && v.Trim().Length > 0) c.Valor = v.Trim();
        if (req.Activo is { } a) c.Activo = a;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarContactoAsync(Guid userId, Guid contactoId, CancellationToken ct)
    {
        var persona = await PersonaDeUsuarioAsync(userId, ct);
        if (persona is null) return false;

        var c = await _db.UsuarioContactosNotificacion
            .FirstOrDefaultAsync(x => x.Id == contactoId && x.PersonaId == persona.Id, ct);
        if (c is null) return false;

        _db.UsuarioContactosNotificacion.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
