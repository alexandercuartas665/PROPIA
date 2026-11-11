namespace Propia.Application.Asambleas;

/// <summary>Datos que viajan firmados en el enlace self-service de una sesion (Ola 1).</summary>
public record AsambleaEnlaceToken(Guid TenantId, Guid SesionId, Guid PersonaId);

/// <summary>Reglas compartidas del enlace: vigencia y ruta publica (endpoint del admin y envio por correo).</summary>
public static class AsambleaEnlace
{
    /// <summary>Vence 2 dias despues de la fecha de la sesion (o de hoy, si la sesion ya paso).</summary>
    public static DateTimeOffset Vencimiento(DateTimeOffset fechaSesion, DateTimeOffset ahora)
        => (fechaSesion > ahora ? fechaSesion : ahora).AddDays(2);

    /// <summary>Ruta relativa de la pagina publica (Web: Pages/Publico/VotarAsambleaPublico.razor).</summary>
    public static string Ruta(Guid tenantId, string token) => $"/publico/asambleas/{tenantId}/{token}";
}

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
