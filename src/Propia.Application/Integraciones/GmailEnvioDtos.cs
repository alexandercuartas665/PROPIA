namespace Propia.Application.Integraciones;

/// <summary>Config del OAuth client de envio (Super Admin). No expone el secret en claro.</summary>
public record GmailEnvioAppConfigDto(string? ClientId, bool TieneSecret, bool IsEnabled);

/// <summary>Guarda el OAuth client de envio. ClientSecret vacio conserva el actual.</summary>
public record GuardarGmailEnvioAppConfigRequest(string? ClientId, string? ClientSecret, bool IsEnabled);

/// <summary>Estado de la conexion Gmail de la copropiedad actual.</summary>
public record GmailEnvioEstadoDto(bool AppConfigurada, bool Conectada, string? Email);
