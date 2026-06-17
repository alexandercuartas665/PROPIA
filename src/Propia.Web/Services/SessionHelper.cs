using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Propia.Web.Services;

/// <summary>
/// Helper centralizado para manejar 401 en llamadas a la API: intenta refresh sliding
/// (POST /connect/refresh) y, si falla, redirige a /login con returnUrl a la ruta actual.
/// Asi el usuario no ve "Sesion expirada" salvo que el sliding window ya este vencido.
/// </summary>
public static class SessionHelper
{
    /// <summary>
    /// Llama propiaAuth.refresh() en el browser. Devuelve el nuevo JWT o null si fallo.
    /// La pagina debe actualizar su variable _jwt y reintentar la llamada.
    /// </summary>
    public static async Task<string?> TryRefreshAsync(IJSRuntime js)
    {
        try
        {
            return await js.InvokeAsync<string?>("propiaAuth.refresh");
        }
        catch { return null; }
    }

    /// <summary>
    /// Maneja un 401: intenta refresh; si OK devuelve el nuevo JWT. Si falla, limpia la sesion
    /// y redirige a /login?returnUrl=&lt;ruta_actual&gt;. Devuelve null cuando redirige.
    /// </summary>
    public static async Task<string?> HandleUnauthorizedAsync(IJSRuntime js, NavigationManager nav)
    {
        var nuevo = await TryRefreshAsync(js);
        if (!string.IsNullOrEmpty(nuevo)) return nuevo;

        // Limpia stores y redirige preservando la ruta actual.
        try { await js.InvokeVoidAsync("propiaAuth.clear"); } catch { }
        var actual = nav.Uri.Replace(nav.BaseUri.TrimEnd('/'), string.Empty);
        if (string.IsNullOrWhiteSpace(actual)) actual = "/";
        var returnUrl = Uri.EscapeDataString(actual);
        nav.NavigateTo($"/login?returnUrl={returnUrl}", forceLoad: true);
        return null;
    }
}
