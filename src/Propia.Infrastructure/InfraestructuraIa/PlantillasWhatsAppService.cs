using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Implementacion del modulo Plantillas de WhatsApp (HSM). Tenant-scoped (query filter + RLS). Unica por
/// (Name, Language) por tenant. El flujo principal es IMPORTAR las plantillas que ya existen en la cuenta
/// YCloud de la linea (UPSERT por Nombre+Idioma). Portado de ECOREX (WhatsAppTemplateService) con el alcance
/// de este corte: importar, listar, archivar/restaurar y envio de prueba. Crear/someter a Meta queda pendiente.
/// </summary>
public sealed class PlantillasWhatsAppService : IPlantillasWhatsAppService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IYCloudApiClient _ycloud;
    private readonly ISecretProtector _secret;
    private readonly IWhatsAppConnectorService _connector;

    public PlantillasWhatsAppService(
        PropiaDbContext db,
        ITenantContext tenant,
        IYCloudApiClient ycloud,
        ISecretProtector secret,
        IWhatsAppConnectorService connector)
    {
        _db = db;
        _tenant = tenant;
        _ycloud = ycloud;
        _secret = secret;
        _connector = connector;
    }

    public async Task<IReadOnlyList<PlantillaWhatsAppDto>> ListarAsync(bool incluirArchivadas = false, CancellationToken ct = default)
    {
        var query = _db.PlantillasWhatsApp.AsNoTracking();
        if (!incluirArchivadas) { query = query.Where(t => t.Activa); }
        var rows = await query
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var lineNames = await LineNamesAsync(rows, ct);
        return rows.Select(t => Map(t, lineNames)).ToList();
    }

    public async Task<PlantillaWhatsAppDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.PlantillasWhatsApp.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) { return null; }
        var lineNames = await LineNamesAsync(new[] { t }, ct);
        return Map(t, lineNames);
    }

    public async Task<bool> ArchivarAsync(Guid id, bool activa, CancellationToken ct = default)
    {
        var t = await _db.PlantillasWhatsApp.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) { return false; }
        t.Activa = activa;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ImportarPlantillasResultado> ImportarDeYCloudAsync(Guid whatsAppLineId, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId)
        {
            return new ImportarPlantillasResultado(false, "No hay copropiedad activa.", null);
        }
        var line = await _db.WhatsAppLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == whatsAppLineId, ct);
        if (line is null)
        {
            return new ImportarPlantillasResultado(false, "La linea de WhatsApp no existe.", null);
        }
        if (line.Provider != WhatsAppProvider.YCloud)
        {
            return new ImportarPlantillasResultado(false, "La linea elegida no es de YCloud.", null);
        }
        if (string.IsNullOrWhiteSpace(line.YCloudApiKeyEncrypted))
        {
            return new ImportarPlantillasResultado(false, "La linea no tiene API key de YCloud configurada.", null);
        }

        string apiKey;
        try { apiKey = _secret.Unprotect(line.YCloudApiKeyEncrypted!); }
        catch { return new ImportarPlantillasResultado(false, "No se pudo leer la API key de la linea.", null); }

        var credentials = new YCloudCredentials(apiKey, line.YCloudFromNumber ?? string.Empty);
        var listed = await _ycloud.ListTemplatesAsync(credentials, ct);
        if (!listed.Ok)
        {
            return new ImportarPlantillasResultado(false, $"YCloud: {listed.Error ?? "no se pudieron listar las plantillas."}", null);
        }

        // Upsert por (Name, Language). Se traen TODAS las del tenant (incl. inactivas) para respetar la unicidad.
        var rows = await _db.PlantillasWhatsApp.ToListAsync(ct);
        var byKey = rows.ToDictionary(t => (t.Name.ToLowerInvariant(), (t.Language ?? "").ToLowerInvariant()));

        int importadas = 0, actualizadas = 0, sinCambios = 0, omitidas = 0;
        var mensajes = new List<string>();

        foreach (var it in listed.Items)
        {
            var name = NormalizeName(it.Name);
            var language = string.IsNullOrWhiteSpace(it.Language) ? "es" : it.Language!.Trim();
            var cuerpo = it.BodyText?.Trim() ?? string.Empty;
            if (cuerpo.Length == 0)
            {
                omitidas++;
                mensajes.Add($"'{name}' ({language}): sin cuerpo, se omitio.");
                continue;
            }
            var categoria = MapCategoria(it.Category);
            var estado = MapEstado(it.Status);
            var encabezado = MapEncabezado(it.HeaderFormat);
            var encabezadoTexto = string.IsNullOrWhiteSpace(it.HeaderText) ? null : it.HeaderText!.Trim();
            var pie = string.IsNullOrWhiteSpace(it.FooterText) ? null : it.FooterText!.Trim();
            var variablesJson = BuildPositionalVariablesJson(it.VariableExamples);
            var botonesJson = BuildButtonsJson(it.Buttons);
            var rechazo = estado == PlantillaWaEstado.Rejected ? it.RejectedReason : null;

            if (byKey.TryGetValue((name.ToLowerInvariant(), language.ToLowerInvariant()), out var existing))
            {
                var before = Fingerprint(existing);
                existing.Categoria = categoria;
                existing.Estado = estado;
                existing.Encabezado = encabezado;
                existing.EncabezadoTexto = encabezadoTexto;
                existing.Cuerpo = cuerpo;
                existing.PieTexto = pie;
                existing.VariablesJson = variablesJson;
                existing.BotonesJson = botonesJson;
                existing.Provider = WhatsAppProvider.YCloud;
                existing.WhatsAppLineId = line.Id;
                existing.WabaId = line.CloudBusinessAccountId;
                existing.ProviderTemplateId = it.Id;
                existing.MotivoRechazo = rechazo;
                if (Fingerprint(existing) == before) { sinCambios++; } else { actualizadas++; }
            }
            else
            {
                _db.PlantillasWhatsApp.Add(new PlantillaWhatsApp
                {
                    TenantId = tenantId,
                    Name = name,
                    Language = language,
                    Categoria = categoria,
                    Encabezado = encabezado,
                    EncabezadoTexto = encabezadoTexto,
                    Cuerpo = cuerpo,
                    PieTexto = pie,
                    VariablesJson = variablesJson,
                    BotonesJson = botonesJson,
                    Provider = WhatsAppProvider.YCloud,
                    WhatsAppLineId = line.Id,
                    WabaId = line.CloudBusinessAccountId,
                    Estado = estado,
                    ProviderTemplateId = it.Id,
                    MotivoRechazo = rechazo,
                    Activa = true
                });
                importadas++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportarPlantillasResultado(true, null,
            new ImportarPlantillasReporte(importadas, actualizadas, sinCambios, omitidas, mensajes));
    }

    public async Task<EnvioPruebaResultado> ProbarEnvioAsync(Guid id, string telefono, IReadOnlyList<string>? valores = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return new EnvioPruebaResultado(false, "Indica el numero de WhatsApp de prueba.");
        }
        var t = await _db.PlantillasWhatsApp.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) { return new EnvioPruebaResultado(false, "La plantilla no existe."); }
        if (!t.Activa) { return new EnvioPruebaResultado(false, "La plantilla esta archivada."); }

        var valoresEnvio = valores is { Count: > 0 } ? valores : ExampleValues(t.VariablesJson);
        var fallback = RenderBody(t.Cuerpo, valoresEnvio);
        var res = await _connector.SendTemplateAsync(t.WhatsAppLineId, telefono.Trim(), t.Name, t.Language, valoresEnvio, fallback, ct);
        return res.Ok
            ? new EnvioPruebaResultado(true, null)
            : new EnvioPruebaResultado(false, res.Error ?? "No se pudo enviar la prueba.");
    }

    // ===== Helpers ============================================================

    private static readonly JsonSerializerOptions JsonInsensitive = new() { PropertyNameCaseInsensitive = true };

    // Huella de los campos que el import puede cambiar (para distinguir "actualizada" de "sin cambios").
    private static string Fingerprint(PlantillaWhatsApp t)
        => string.Join("|", t.Categoria, t.Estado, t.Encabezado, t.EncabezadoTexto, t.Cuerpo, t.PieTexto,
            t.VariablesJson, t.BotonesJson, t.ProviderTemplateId, t.MotivoRechazo, t.WabaId);

    private static string? BuildButtonsJson(IReadOnlyList<YCloudTemplateButton>? buttons)
    {
        if (buttons is null || buttons.Count == 0) { return null; }
        var shaped = buttons.Select(b => new
        {
            type = b.Type,
            text = b.Text,
            url = b.Url,
            phoneNumber = b.PhoneNumber,
            hasUrlVariable = b.HasUrlVariable
        });
        return JsonSerializer.Serialize(shaped);
    }

    private static PlantillaWaCategoria MapCategoria(string? category) => category?.ToUpperInvariant() switch
    {
        "MARKETING" => PlantillaWaCategoria.Marketing,
        "AUTHENTICATION" => PlantillaWaCategoria.Authentication,
        _ => PlantillaWaCategoria.Utility
    };

    private static PlantillaWaEstado MapEstado(string? status) => status?.ToUpperInvariant() switch
    {
        "APPROVED" => PlantillaWaEstado.Approved,
        "REJECTED" => PlantillaWaEstado.Rejected,
        "PAUSED" => PlantillaWaEstado.Paused,
        "DISABLED" or "DELETED" => PlantillaWaEstado.Disabled,
        "PENDING" or "IN_APPEAL" or "PENDING_DELETION" => PlantillaWaEstado.Submitted,
        _ => PlantillaWaEstado.Draft
    };

    private static PlantillaWaEncabezado? MapEncabezado(string? format) => format?.ToUpperInvariant() switch
    {
        "TEXT" => PlantillaWaEncabezado.Text,
        "IMAGE" => PlantillaWaEncabezado.Image,
        "VIDEO" => PlantillaWaEncabezado.Video,
        "DOCUMENT" => PlantillaWaEncabezado.Document,
        _ => null
    };

    // Variables POSICIONALES: token = "1","2"... (como las devuelve Meta) + su ejemplo si vino.
    private static string BuildPositionalVariablesJson(IReadOnlyList<string>? examples)
    {
        if (examples is null || examples.Count == 0) { return "[]"; }
        var vars = examples.Select((ex, i) => new PlantillaWaVariable((i + 1).ToString(), ex ?? string.Empty)).ToList();
        return JsonSerializer.Serialize(vars);
    }

    // Valores de EJEMPLO de las variables, en orden (cae al token si no hay ejemplo).
    private static List<string> ExampleValues(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson)) { return new List<string>(); }
        try
        {
            var vars = JsonSerializer.Deserialize<List<PlantillaWaVariable>>(variablesJson, JsonInsensitive);
            return vars?.Select(v => !string.IsNullOrWhiteSpace(v.Example) ? v.Example : (v.Token ?? string.Empty)).ToList()
                ?? new List<string>();
        }
        catch (JsonException) { return new List<string>(); }
    }

    // Renderiza el cuerpo reemplazando {{1}},{{2}}... por los valores en orden. Sirve de texto fallback para
    // lineas Evolution (que no usan HSM de Meta y mandan texto libre).
    private static string RenderBody(string? body, IReadOnlyList<string> values)
    {
        var text = body ?? string.Empty;
        for (var i = 0; i < values.Count; i++)
        {
            text = text.Replace("{{" + (i + 1) + "}}", values[i] ?? string.Empty);
        }
        return text;
    }

    private async Task<Dictionary<Guid, string>> LineNamesAsync(IReadOnlyCollection<PlantillaWhatsApp> templates, CancellationToken ct)
    {
        var ids = templates.Select(t => t.WhatsAppLineId).Distinct().ToList();
        if (ids.Count == 0) { return new Dictionary<Guid, string>(); }
        return await _db.WhatsAppLines.AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.InstanceName, ct);
    }

    private static PlantillaWhatsAppDto Map(PlantillaWhatsApp t, IReadOnlyDictionary<Guid, string> lineNames)
    {
        IReadOnlyList<PlantillaWaVariable> vars;
        try { vars = JsonSerializer.Deserialize<List<PlantillaWaVariable>>(t.VariablesJson, JsonInsensitive) ?? new(); }
        catch { vars = new List<PlantillaWaVariable>(); }
        IReadOnlyList<PlantillaWaBotonDto> botones;
        try
        {
            botones = string.IsNullOrWhiteSpace(t.BotonesJson)
                ? new List<PlantillaWaBotonDto>()
                : JsonSerializer.Deserialize<List<PlantillaWaBotonDto>>(t.BotonesJson, JsonInsensitive) ?? new();
        }
        catch { botones = new List<PlantillaWaBotonDto>(); }
        return new PlantillaWhatsAppDto(
            t.Id, t.Name, t.Language, t.Categoria, t.Encabezado, t.EncabezadoTexto, t.EncabezadoMediaUrl,
            t.Cuerpo, t.PieTexto, vars, botones, t.Provider, t.WhatsAppLineId,
            lineNames.TryGetValue(t.WhatsAppLineId, out var lineName) ? lineName : null,
            t.WabaId, t.Estado, t.ProviderTemplateId, t.MotivoRechazo, t.SubmittedAt, t.ReviewedAt, t.Activa);
    }

    // Normaliza el nombre tecnico al formato de Meta: minusculas, guion_bajo, sin acentos ni simbolos.
    private static string NormalizeName(string? raw)
    {
        var lower = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new List<char>(lower.Length);
        foreach (var c in lower)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { chars.Add(c); }
            else if (c is ' ' or '-' or '_') { chars.Add('_'); }
        }
        var name = new string(chars.ToArray()).Trim('_');
        while (name.Contains("__")) { name = name.Replace("__", "_"); }
        return string.IsNullOrWhiteSpace(name) ? "plantilla" : name;
    }
}
