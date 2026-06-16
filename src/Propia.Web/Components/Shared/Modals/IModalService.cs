using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Servicio central para abrir modales globales desde cualquier pagina o
/// componente. Se inyecta via @inject IModalService Modals.
///
/// Uso:
/// <code>
/// var result = await Modals.OpenAsync&lt;ProgramarMantenimientoModal, MantenimientoProgramado&gt;(
///     new ModalParameters().Set("ActivoId", activo.Id));
/// if (result is null) return; // cancelado
/// await ServicioMantenimiento.ProgramarAsync(result);
/// </code>
///
/// Reglas (ver [[00.3. COMPONENTES GLOBALES]]):
/// - El modal NO accede a HttpContext, solo a servicios de Application
/// - Resultado tipado: T o null si cancelado
/// - Apertura async, no bloquea al caller
/// - Dry-run cuando aplique (creaciones de impacto)
/// </summary>
public interface IModalService
{
    /// <summary>Lista de modales actualmente abiertos. Se renderiza por ModalHost.</summary>
    IReadOnlyList<ModalReference> Active { get; }

    /// <summary>Se dispara cuando un modal se abre o cierra (notifica a ModalHost).</summary>
    event Action? OnChange;

    /// <summary>
    /// Abre un modal del tipo TModal y espera su resultado de tipo TResult.
    /// Devuelve null si el modal fue cancelado.
    /// </summary>
    Task<TResult?> OpenAsync<TModal, TResult>(ModalParameters? parameters = null, ModalOptions? options = null)
        where TModal : ComponentBase;

    /// <summary>Cierra un modal por su Id con un resultado (null = cancelado).</summary>
    void Close(Guid modalId, object? result = null);
}

/// <summary>Opciones cosmeticas / comportamiento del modal.</summary>
public class ModalOptions
{
    public string Width { get; set; } = "540px";
    public bool CloseOnBackdrop { get; set; } = true;
    /// <summary>Layout fullscreen tipo prototipo (1640px max, 95vh). Ignora Width.</summary>
    public bool Fullscreen { get; set; }
}
