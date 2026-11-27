using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Propia.Application.UsuariosAccesos;
using Propia.Domain.Enums;

namespace Propia.Web.Services;

/// <summary>
/// Permisos del usuario actual (GET /api/mis-permisos), cacheados por circuito. La UI lo usa para no
/// ofrecer acciones que el usuario no tiene (p.ej. el boton Descargar exige Exportar en el modulo).
/// Se recarga si cambia el JWT (cambio de copropiedad, de rol activo o refresh) o pasado un minuto, para
/// que un cambio en la matriz de roles se note sin cerrar sesion. Ante cualquier error: NO puede (cerrado).
/// </summary>
public sealed class MisPermisos
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _http;
    private readonly IJSRuntime _js;
    private string? _jwt;
    private DateTimeOffset _cargado;
    private Task<MisPermisosDto?>? _carga;

    public MisPermisos(IHttpClientFactory http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task<bool> PuedeAsync(string modulo, AccionPermiso accion)
        => (await ObtenerAsync())?.Puede(modulo, accion) ?? false;

    /// <summary>Todos los permisos del usuario actual (null si no se pudieron leer: prerender, sin sesion o
    /// error). El menu lateral lo usa para ocultar los modulos sin permiso de Ver (Alex 2026-11-27).</summary>
    public async Task<MisPermisosDto?> ObtenerAsync()
    {
        string? jwt;
        try { jwt = await _js.InvokeAsync<string?>("propiaAuth.get", "propia_jwt"); }
        catch { return null; } // prerender / circuito caido
        if (string.IsNullOrWhiteSpace(jwt)) return null;

        if (_carga is null || jwt != _jwt || DateTimeOffset.UtcNow - _cargado > Vigencia)
        {
            _jwt = jwt;
            _cargado = DateTimeOffset.UtcNow;
            _carga = CargarAsync(jwt);
        }
        var p = await _carga;
        if (p is null) _carga = null; // no cachear el error: reintenta en la proxima consulta
        return p;
    }

    private async Task<MisPermisosDto?> CargarAsync(string jwt)
    {
        try
        {
            var c = _http.CreateClient("PropiaApi");
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            return await c.GetFromJsonAsync<MisPermisosDto>("/api/mis-permisos");
        }
        catch { return null; }
    }
}
