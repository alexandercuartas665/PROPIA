namespace Propia.Application.Asambleas;

/// <summary>Datos que viajan firmados en el enlace self-service de una sesion (Ola 1).</summary>
public record AsambleaEnlaceToken(Guid TenantId, Guid SesionId, Guid PersonaId);

/// <summary>
/// Firma y valida el token del enlace con el que el copropietario entra a una sesion de asamblea
/// sin login (registra su asistencia y vota). Sin estado: no hay tabla; el "un solo uso" lo
/// garantizan las reglas del voto (RN-09 + indice unico por votacion/unidad) y el check-in
/// idempotente. La version revocable/auditada con tabla queda para la Ola 3 (WhatsApp).
/// </summary>
public interface IAsambleaTokenService
{
    /// <summary>Emite un token opaco (base64url, apto para ruta) que vence en <paramref name="expira"/>.</summary>
    string Emitir(AsambleaEnlaceToken datos, DateTimeOffset expira);

    /// <summary>Devuelve los datos si el token es autentico y no ha vencido; null en cualquier otro caso.</summary>
    AsambleaEnlaceToken? Validar(string? token);
}
