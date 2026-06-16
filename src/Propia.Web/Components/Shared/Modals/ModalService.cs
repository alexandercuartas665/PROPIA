using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Implementacion scoped por circuit Blazor Server. Mantiene la lista de
/// modales activos y dispara OnChange cuando cambia para que ModalHost se
/// re-renderice.
/// </summary>
public class ModalService : IModalService
{
    private readonly List<ModalReference> _active = new();
    public IReadOnlyList<ModalReference> Active => _active;
    public event Action? OnChange;

    public Task<TResult?> OpenAsync<TModal, TResult>(ModalParameters? parameters = null, ModalOptions? options = null)
        where TModal : ComponentBase
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reference = new ModalReference
        {
            ComponentType = typeof(TModal),
            Parameters = parameters ?? new ModalParameters(),
            Tcs = tcs,
            Width = options?.Width ?? "540px",
            CloseOnBackdrop = options?.CloseOnBackdrop ?? true,
            Fullscreen = options?.Fullscreen ?? false
        };
        _active.Add(reference);
        OnChange?.Invoke();

        // Mapeamos el object? a TResult? para que el caller reciba el tipo correcto.
        return tcs.Task.ContinueWith(t =>
        {
            if (t.Result is TResult r) return r;
            return default(TResult);
        }, TaskScheduler.Default);
    }

    public void Close(Guid modalId, object? result = null)
    {
        var modal = _active.FirstOrDefault(m => m.Id == modalId);
        if (modal is null) return;
        _active.Remove(modal);
        modal.Tcs.TrySetResult(result);
        OnChange?.Invoke();
    }
}
