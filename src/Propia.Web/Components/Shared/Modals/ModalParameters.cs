using System.Collections.Generic;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Bolsa de parametros tipados que se pasa a un modal global al abrirlo.
/// Convencion: el caller usa Set("Key", value) y el modal los recibe via
/// [Parameter] public T Key { get; set; } gracias a DynamicComponent.
/// </summary>
public class ModalParameters
{
    private readonly Dictionary<string, object?> _params = new();

    public ModalParameters Set(string key, object? value)
    {
        _params[key] = value;
        return this;
    }

    public T? Get<T>(string key)
    {
        return _params.TryGetValue(key, out var v) && v is T t ? t : default;
    }

    public IReadOnlyDictionary<string, object?> All => _params;
}
