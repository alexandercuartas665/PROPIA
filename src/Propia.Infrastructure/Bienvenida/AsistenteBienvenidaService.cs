using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propia.Application.Bienvenida;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Application.Integraciones;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Bienvenida;

/// <summary>
/// Asistente del onboarding /bienvenida. Agente de PLATAFORMA: resuelve el proveedor desde la
/// config global del Super Admin (AiProviderConfigs, sin tenant) y llama al cliente de inferencia
/// con el prompt de BienvenidaPrompts. No usa AiAgent de tenant ni ejecuta tools.
/// </summary>
public class AsistenteBienvenidaService : IAsistenteBienvenidaService
{
    private static readonly string[] NombresPasos =
        { "Bienvenida", "Tu perfil", "Co-propiedad", "Estructura", "Personas" };

    private readonly PropiaDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _client;
    private readonly ILogger<AsistenteBienvenidaService> _logger;

    public AsistenteBienvenidaService(
        PropiaDbContext db,
        ISecretProtector secret,
        IAiProviderClient client,
        ILogger<AsistenteBienvenidaService> logger)
    {
        _db = db;
        _secret = secret;
        _client = client;
        _logger = logger;
    }

    public async Task<BienvenidaChatDto> ResponderAsync(BienvenidaChatRequest req, CancellationToken ct)
    {
        var proveedor = await ResolverProveedorAsync(ct);
        if (proveedor is null)
            return new BienvenidaChatDto(false, null, "ia_no_configurada");

        var (provider, apiKey, baseUrl, model) = proveedor.Value;

        var paso = Math.Clamp(req.Paso, 0, NombresPasos.Length - 1);
        var system = BienvenidaPrompts.Sistema +
            $"\n\nContexto actual: el usuario esta en el paso {paso + 1} de {NombresPasos.Length} ({NombresPasos[paso]})." +
            (string.IsNullOrWhiteSpace(req.NombreUsuario) ? "" : $" Se llama {req.NombreUsuario.Trim()}.") +
            (string.IsNullOrWhiteSpace(req.ContextoPaso) ? "" : $"\nDatos que lleva diligenciados: {req.ContextoPaso.Trim()}");

        // Ultimos 16 turnos: suficiente memoria para el recorrido sin inflar el prompt.
        var turns = (req.Conversacion ?? new List<BienvenidaTurno>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Texto))
            .TakeLast(16)
            .Select(t => new AiChatTurn(t.Rol == "model" ? "model" : "user", t.Texto.Trim()))
            .ToList();
        if (turns.Count == 0)
            return new BienvenidaChatDto(false, null, "conversacion_vacia");

        try
        {
            var res = await _client.CompleteAsync(provider, apiKey, baseUrl, model, system, turns, ct);
            return new BienvenidaChatDto(res.Ok, res.Text, res.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo el asistente de bienvenida (proveedor {Provider})", provider);
            return new BienvenidaChatDto(false, null, "error_inferencia");
        }
    }

    public async Task<BienvenidaChatDto> GenerarDescripcionAsync(BienvenidaDescripcionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            return new BienvenidaChatDto(false, null, "nombre_requerido");

        var proveedor = await ResolverProveedorAsync(ct);
        if (proveedor is null)
            return new BienvenidaChatDto(false, null, "ia_no_configurada");

        var (provider, apiKey, baseUrl, model) = proveedor.Value;

        var datos = $"Nombre: {req.Nombre.Trim()}"
            + (string.IsNullOrWhiteSpace(req.Tipo) ? "" : $". Tipo: {req.Tipo}")
            + (string.IsNullOrWhiteSpace(req.Ciudad) ? "" : $". Ciudad: {req.Ciudad}")
            + (string.IsNullOrWhiteSpace(req.Departamento) ? "" : $". Departamento: {req.Departamento}")
            + (string.IsNullOrWhiteSpace(req.Estrato) ? "" : $". Estrato: {req.Estrato}")
            + (string.IsNullOrWhiteSpace(req.TextoActual) ? "" : $". Borrador del usuario a mejorar: {req.TextoActual.Trim()}");

        try
        {
            var res = await _client.CompleteAsync(provider, apiKey, baseUrl, model,
                BienvenidaPrompts.Descripcion,
                new List<AiChatTurn> { new("user", datos) }, ct);
            return new BienvenidaChatDto(res.Ok, res.Text?.Trim(), res.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo generando la descripcion de bienvenida (proveedor {Provider})", provider);
            return new BienvenidaChatDto(false, null, "error_inferencia");
        }
    }

    /// <summary>
    /// Primer proveedor global habilitado cuya API key se pueda descifrar. Modelo: el de la config
    /// o el default del catalogo (mismo criterio de AiInferenceService, sin agente de tenant).
    /// </summary>
    private async Task<(Domain.Enums.AiProvider Provider, string ApiKey, string? BaseUrl, string Model)?> ResolverProveedorAsync(CancellationToken ct)
    {
        var configs = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
            .OrderBy(c => c.Provider)
            .ToListAsync(ct);

        foreach (var cfg in configs)
        {
            string apiKey;
            try { apiKey = _secret.Unprotect(cfg.ApiKeyEncrypted!); }
            catch { continue; }

            var meta = AiProviderCatalog.For(cfg.Provider);
            var model = !string.IsNullOrWhiteSpace(cfg.Model) ? cfg.Model! : meta.DefaultModel;
            return (cfg.Provider, apiKey, cfg.BaseUrl, model);
        }
        return null;
    }
}
