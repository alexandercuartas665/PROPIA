namespace Propia.Application.Catalogos;

/// <summary>
/// Materializacion y mantenimiento de las copias POR TENANT de las listas del sistema (Ola 4, modelo PQRSD
/// generalizado). La copia nace perezosa desde el catalogo global y luego es independiente.
/// </summary>
public interface IListasTenant
{
    /// <summary>
    /// Asegura (idempotente) que la copropiedad activa tiene materializada su copia de <paramref name="lista"/>:
    /// si no tiene filas, las crea desde las opciones ACTIVAS del catalogo global (EsBase=true). No pisa nada si
    /// ya existe.
    /// </summary>
    Task AsegurarListaAsync(string lista, CancellationToken ct = default);

    /// <summary>
    /// "Re-sembrar listas base" POR TENANT: reaplica las bases del catalogo global sobre la copia del tenant
    /// (una lista, o todas las presentes si <paramref name="lista"/> es null). Agrega bases que falten; para
    /// bases con OverrideVisual=false realinea label/color/orden y desactiva las que el catalogo ya desactivo.
    /// NUNCA toca opciones propias (EsBase=false) ni bases overrideadas, ni borra filas. Devuelve cuantos
    /// cambios aplico.
    /// </summary>
    Task<int> ReaplicarBasesAsync(string? lista = null, CancellationToken ct = default);

    /// <summary>
    /// Guarda el estado EDITADO de una lista por-tenant desde el editor de columna (write-side de Ola 4). Recibe
    /// el conjunto de opciones tal como quedo en el editor (en orden). Reconcilia por LABEL contra la copia del
    /// tenant (insensible a may/acentos): realinea color/orden/visibilidad de las que coinciden (marcando
    /// OverrideVisual=true en las bases que la copropiedad tocó), inserta las nuevas como propias (EsBase=false,
    /// clave c:guid) y DESACTIVA (no borra) las que el editor quito. Idempotente. Devuelve cuantos cambios aplico.
    /// </summary>
    Task<int> GuardarOpcionesAsync(string lista, IReadOnlyList<OpcionGuardarDto> opciones, CancellationToken ct = default);
}

/// <summary>Una opcion tal como la deja el editor de columna: label visible, color opcional y si queda oculta.
/// El orden lo da la posicion en la lista. <paramref name="Meta"/> (Ola 4c, listas con-logica): JSON de negocio;
/// null = no tocar la Meta existente (el editor generico de listas libres no la envia).</summary>
public record OpcionGuardarDto(string Label, string? Color, bool Oculta, string? Meta = null);
