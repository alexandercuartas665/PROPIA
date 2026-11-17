namespace Propia.Domain.Enums;

/// <summary>
/// Elemento de la copropiedad afectado por un siniestro. Un siniestro puede no tener elemento
/// (Ninguno), ligarse a un equipo, zona comun o unidad (ElementoId) o describirse libremente (Otro).
/// </summary>
public enum ElementoSiniestro
{
    Ninguno = 0,
    Equipo = 1,
    Zona = 2,
    Unidad = 3,
    Otro = 4
}

/// <summary>
/// Claves de los estados BASE de un siniestro. El nombre visible lo edita cada copropiedad; la clave
/// es estable y lleva la regla de negocio (pagado/cerrado piden monto reconocido). Los estados propios
/// de la copropiedad no tienen clave.
/// </summary>
public static class EstadoSiniestroClave
{
    public const string Reportado = "reportado";
    public const string EnTramite = "tramite";
    public const string Aprobado = "aprobado";
    public const string Objetado = "objetado";
    public const string Pagado = "pagado";
    public const string Cerrado = "cerrado";

    /// <summary>Estados que exigen el monto reconocido por la aseguradora.</summary>
    public static bool PideMontoReconocido(string? clave) => clave is Pagado or Cerrado;
}
