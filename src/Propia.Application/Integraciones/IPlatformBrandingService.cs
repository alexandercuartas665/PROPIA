namespace Propia.Application.Integraciones;

/// <summary>Marca de la plataforma para pintar el login y superficies publicas.</summary>
public sealed record PlatformBrandingDto(
    string PlatformName,
    string? Tagline,
    string? LoginLogoUrl,
    string? LoginHeadline,
    string? LoginSubtext)
{
    /// <summary>Valores por defecto cuando aun no se ha configurado la marca.</summary>
    public static PlatformBrandingDto Default => new(
        "PROPIA",
        "Plataforma de gestion de copropiedades",
        null,
        "Administra tu copropiedad sin complicaciones",
        "PROPIA centraliza presupuesto, cartera, asambleas, PQRSD, mantenimiento y comunicaciones de tu propiedad horizontal.");
}

public sealed record SaveBrandingRequest(
    string PlatformName,
    string? Tagline,
    string? LoginLogoUrl,
    string? LoginHeadline,
    string? LoginSubtext);

/// <summary>Gestiona el branding global (tabla de una sola fila). El Super Admin lo edita; el login lo lee.</summary>
public interface IPlatformBrandingService
{
    Task<PlatformBrandingDto> GetAsync(CancellationToken ct = default);
    Task SaveAsync(SaveBrandingRequest request, Guid actorId, string actorEmail, string? ip, CancellationToken ct = default);
}
