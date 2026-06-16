using Microsoft.AspNetCore.Components;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Base recomendada para modales globales. Provee acceso al ModalReference
/// (via CascadingValue desde ModalHost) y helpers Close/Cancel tipados.
///
/// Uso:
/// <code>
/// @inherits ModalBase&lt;MantenimientoProgramado&gt;
/// ...
/// &lt;button @onclick="() => Close(_draft)"&gt;Confirmar&lt;/button&gt;
/// </code>
/// </summary>
public abstract class ModalBase<TResult> : ComponentBase
{
    [CascadingParameter]
    protected ModalReference ModalRef { get; set; } = default!;

    [Inject]
    protected IModalService Modals { get; set; } = default!;

    /// <summary>Cierra el modal devolviendo un resultado al caller.</summary>
    protected void Close(TResult? result) => Modals.Close(ModalRef.Id, result);

    /// <summary>Cierra el modal devolviendo null (cancelado).</summary>
    protected void Cancel() => Modals.Close(ModalRef.Id, default(TResult));
}
