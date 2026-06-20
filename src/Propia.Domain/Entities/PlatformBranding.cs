using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Marca/branding de la plataforma (GLOBAL, configurable por el Super Admin).
/// Tabla de una sola fila: gobierna el logo y los textos de la pantalla de login del cliente.
/// Portado de CUBOT.travels con defaults de PROPIA.
/// </summary>
public class PlatformBranding : BaseEntity
{
    /// <summary>Nombre visible de la plataforma.</summary>
    public string PlatformName { get; set; } = "PROPIA";

    /// <summary>Bajada corta bajo el nombre.</summary>
    public string? Tagline { get; set; }

    /// <summary>URL del logo principal del login (y panel lateral). Null = logo por defecto.</summary>
    public string? LoginLogoUrl { get; set; }

    /// <summary>URL del icono cuadrado (rail del sidebar + favicon). Null = icono por defecto.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Titular grande del panel del login.</summary>
    public string? LoginHeadline { get; set; }

    /// <summary>Texto descriptivo del panel del login.</summary>
    public string? LoginSubtext { get; set; }
}
