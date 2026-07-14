using System;
using System.Threading.Tasks;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Handle a un modal abierto. Lo crea ModalService y lo recibe el modal via
/// CascadingValue para poder cerrarse a si mismo (Close / Cancel).
/// </summary>
public class ModalReference
{
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Tipo del componente Razor del modal.</summary>
    public required Type ComponentType { get; init; }

    /// <summary>Parametros pasados al modal.</summary>
    public required ModalParameters Parameters { get; init; }

    /// <summary>
    /// Completion source que resuelve cuando el modal se cierra.
    /// El valor es lo que el modal devuelve (TResult) o null si cancelado.
    /// </summary>
    public required TaskCompletionSource<object?> Tcs { get; init; }

    /// <summary>
    /// Si true, click en el backdrop cierra (con null). Default true.
    /// El modal puede ponerlo en false para forzar uso de Cancel.
    /// </summary>
    public bool CloseOnBackdrop { get; set; } = true;

    /// <summary>Ancho preferido del modal (CSS). Ej. "540px", "780px", "1640px".</summary>
    public string Width { get; set; } = "540px";

    /// <summary>Alto fijo del shell (CSS, ej. "88vh"). Si es null el modal crece con su contenido.</summary>
    public string? Height { get; set; }

    /// <summary>Layout fullscreen estilo prototipo (max 1640px x 95vh, padding reducido).</summary>
    public bool Fullscreen { get; set; }
}
