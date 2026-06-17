using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Numero de telefono en la lista negra global del tenant: ningun agente de IA debe responderle.
/// Comparte la lista todos los agentes del tenant; el dispatcher la consulta antes de despachar.
/// Comparacion por digitos (ignora prefijo +, espacios y codigo de pais sobrante).
/// Portado desde CUBOT.travels TenantBlockedNumber.
/// </summary>
public class NumeroEnListaNegra : TenantEntity
{
    /// <summary>Telefono normalizado a solo digitos (sin +, espacios ni separadores).</summary>
    public string Telefono { get; set; } = null!;

    /// <summary>Nota opcional: motivo, contexto, o como se agrego (ej. comando del residente).</summary>
    public string? Nota { get; set; }
}
