using System.Net.Http.Json;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Helper compartido del menu de columna (accion "Escribir un titulo para el campo"): pide al
/// Auxiliar Administrativo de la copropiedad (endpoint reutilizable POST /api/ia/completar-campo)
/// un titulo corto para una columna de tabla. NO persiste nada: solo devuelve la sugerencia; cada
/// modulo decide si la aplica (renombrando la definicion del campo). Solo aplica a campos propios
/// (dinamicos): los campos del sistema no se renombran por aqui.
/// </summary>
public static class IaCampoTitulo
{
    public sealed record Resultado(bool Ok, string? Titulo, string? Error);

    private sealed record CampoResult(bool Ok, string? Texto, string? Error);

    /// <param name="http">Cliente ya autenticado del modulo (Auth()).</param>
    /// <param name="entidad">Nombre humano de la tabla, ej "personas del directorio", "unidades".</param>
    /// <param name="labelActual">Como se llama hoy la columna (contexto para la IA).</param>
    /// <param name="otras">Otras columnas de la tabla (contexto para no repetir nombres).</param>
    public static async Task<Resultado> SugerirAsync(
        HttpClient http, string entidad, string labelActual,
        IEnumerable<string>? otras = null, CancellationToken ct = default)
    {
        try
        {
            var contexto = $"Tabla de {entidad}. La columna se llama actualmente \"{labelActual}\".";
            if (otras is not null)
            {
                var o = string.Join(", ", otras.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(12));
                if (o.Length > 0) contexto += $" Otras columnas de la tabla: {o}.";
            }

            var req = new
            {
                Proposito = "Un titulo corto y claro (2 a 4 palabras, en espanol, sin comillas ni punto final) "
                          + "para una columna de una tabla del sistema de administracion de copropiedades.",
                Contexto = contexto,
                MaxPalabras = 6
            };

            var resp = await http.PostAsJsonAsync("/api/ia/completar-campo", req, ct);
            if (!resp.IsSuccessStatusCode) return new Resultado(false, null, "no_disponible");

            var dto = await resp.Content.ReadFromJsonAsync<CampoResult>(cancellationToken: ct);
            if (dto is null || !dto.Ok || string.IsNullOrWhiteSpace(dto.Texto))
                return new Resultado(false, null, dto?.Error ?? "sin_texto");

            var t = dto.Texto.Trim().Trim('"', '.', ' ');
            var nl = t.IndexOf('\n');
            if (nl >= 0) t = t[..nl].Trim();
            if (t.Length > 60) t = t[..60].Trim();
            if (t.Length == 0) return new Resultado(false, null, "sin_texto");

            return new Resultado(true, t, null);
        }
        catch
        {
            return new Resultado(false, null, "error");
        }
    }
}
