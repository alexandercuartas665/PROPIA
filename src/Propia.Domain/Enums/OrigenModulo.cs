namespace Propia.Domain.Enums;

/// <summary>
/// Codigos de modulo que emiten tareas al tablero GENERAL (Tarea.ModuloOrigenCodigo). Es el valor
/// que muestra la columna "Origen" del tablero. Es INMUTABLE: lo fija el modulo emisor al crear la
/// tarea y no se edita desde la UI. Tareas creadas a mano quedan sin codigo (Origen = "Propia").
/// </summary>
public static class OrigenModulo
{
    public const string Pqrsd = "PQRSD";
    public const string Contrato = "CONTRATOS";
    public const string Seguro = "SEGUROS";
    public const string Mantenimiento = "MANTENIMIENTO";

    /// <summary>Etiqueta legible para la columna Origen. Codigo vacio/nulo = tarea propia (manual).</summary>
    public static string Etiqueta(string? codigo) => codigo switch
    {
        Pqrsd => "PQRSD",
        Contrato => "Contratos",
        Seguro => "Seguros",
        Mantenimiento or "2.11" => "Mantenimiento",  // "2.11" = codigo historico del modulo de mantenimiento
        null or "" => "Propia",
        _ => codigo!
    };
}
