using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Auth;
using Propia.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Propia.Integration.Tests;

/// <summary>
/// BANCO del Agente Documental con PDFs REALES (no corre en la suite normal: se salta si no hay
/// PROPIA_BENCH_DIR). Levanta el API en memoria contra la BD de desarrollo (PROPIA_BENCH_CONN, la
/// copropiedad PROPIA_BENCH_TENANT con su agente y su llave de IA), firma un token en proceso para el
/// usuario PROPIA_BENCH_USER y llama POST /api/ocr/extraer-agente como lo hace el cliente:
///  - modo "antes": texto si pdf.js saco algo; si no, las paginas como imagen (regla del cliente viejo);
///  - modo "despues": ademas, si el texto es pobre (pocas palabras por pagina) manda tambien las imagenes.
/// Cada .json del directorio lo arma bench_prep.py: { archivo, perfil, paginas, palabras, texto, imagenes[] }.
/// Reporta por documento cuantos campos llegaron con valor (total y sobre las claves del perfil viejo).
/// </summary>
public class AgenteDocumentalBenchTests
{
    private readonly ITestOutputHelper _out;
    public AgenteDocumentalBenchTests(ITestOutputHelper output) => _out = output;

    // Claves del perfil anterior (antes de ampliar): la comparacion justa antes/despues se hace sobre estas.
    private static readonly Dictionary<string, string[]> ClavesViejas = new()
    {
        ["contrato"] = new[] { "contratista", "nit", "numero_contrato", "objeto", "valor_total", "valor_mensual", "contacto", "fecha_inicio", "fecha_fin" },
        ["poliza"] = new[] { "aseguradora", "corredor", "numero_poliza", "ramo", "valor_poliza", "fecha_inicio", "fecha_fin" },
    };
    // Claves viejas que el perfil nuevo reparte en otras: cuentan como presentes si llega cualquiera.
    private static readonly Dictionary<string, string[]> Alias = new()
    {
        ["valor_poliza"] = new[] { "prima", "valor_asegurado" },
        ["contacto"] = new[] { "telefono_contratista", "correo_contratista" },
    };

    [Fact]
    public async Task Banco_pdfs_reales()
    {
        // Banco MANUAL: sin PROPIA_BENCH_DIR no hace nada (la suite normal no depende de una IA externa).
        var dir = Environment.GetEnvironmentVariable("PROPIA_BENCH_DIR");
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        var conn = Environment.GetEnvironmentVariable("PROPIA_BENCH_CONN")!;
        var tenantId = Guid.Parse(Environment.GetEnvironmentVariable("PROPIA_BENCH_TENANT")!);
        var userId = Guid.Parse(Environment.GetEnvironmentVariable("PROPIA_BENCH_USER")!);
        var modo = Environment.GetEnvironmentVariable("PROPIA_BENCH_MODO") ?? "despues";
        var salida = Environment.GetEnvironmentVariable("PROPIA_BENCH_OUT");

        var connPrevia = Environment.GetEnvironmentVariable("ConnectionStrings__Propia");
        Environment.SetEnvironmentVariable("ConnectionStrings__Propia", conn);
        Environment.SetEnvironmentVariable("Jobs__Enabled", "false");
        await using var factory = new AuthFlowTests.ApiFactory(conn);
        try
        {
            string jwt;
            using (var scope = factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("usuario no existe");
                jwt = scope.ServiceProvider.GetRequiredService<ITokenService>().IssueAccessToken(user, tenantId).Token;
            }
            var http = factory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(6);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var rep = new StringBuilder();
            rep.AppendLine($"modo={modo}");
            rep.AppendLine("archivo\tperfil\truta\tcampos_con_valor\tde_claves_viejas\tdetalle");
            int total = 0, totalViejas = 0;
            foreach (var f in Directory.GetFiles(dir!, "*.json").OrderBy(x => x))
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(f));
                var r = doc.RootElement;
                var perfil = r.GetProperty("perfil").GetString()!;
                var texto = r.GetProperty("texto").GetString() ?? "";
                var paginas = Math.Max(1, Math.Min(r.GetProperty("paginas").GetInt32(), 15));
                var imgs = r.GetProperty("imagenes").EnumerateArray().Select(x => x.GetString()!).ToList();
                var palabrasPorPag = texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / (double)paginas;

                object body; string ruta;
                var totalPag = r.GetProperty("paginas").GetInt32();
                if (string.IsNullOrWhiteSpace(texto)) { body = new { Perfil = perfil, Imagenes = imgs, Paginas = totalPag }; ruta = "vision"; }
                else if (modo == "despues" && palabrasPorPag < 40) { body = new { Perfil = perfil, Texto = texto, Imagenes = imgs, Paginas = totalPag }; ruta = "texto+vision"; }
                else { body = new { Perfil = perfil, Texto = texto, Paginas = totalPag }; ruta = "texto"; }

                var resp = await http.PostAsJsonAsync("/api/ocr/extraer-agente", body);
                var raw = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode) { rep.AppendLine($"{r.GetProperty("archivo").GetString()}\t{perfil}\t{ruta}\tERROR {(int)resp.StatusCode}\t\t{raw}"); continue; }
                using var rj = JsonDocument.Parse(raw);
                var campos = rj.RootElement.GetProperty("campos").EnumerateArray()
                    .Select(c => (Nombre: c.GetProperty("nombre").GetString()!, Valor: c.GetProperty("valor").GetString()))
                    .Where(c => !string.IsNullOrWhiteSpace(c.Valor)).ToList();
                var nombres = campos.Select(c => c.Nombre).ToHashSet();
                var viejas = ClavesViejas[perfil].Count(k => nombres.Contains(k) || (Alias.TryGetValue(k, out var al) && al.Any(nombres.Contains)));
                total += campos.Count; totalViejas += viejas;
                var det = string.Join(" | ", campos.Select(c => $"{c.Nombre}={Corto(c.Valor!)}"));
                var extra = rj.RootElement.TryGetProperty("pasadas", out var pas) ? $" pasadas={pas.GetInt32()} via={rj.RootElement.GetProperty("ruta").GetString()}" : "";
                rep.AppendLine($"{r.GetProperty("archivo").GetString()}\t{perfil}\t{ruta}{extra}\t{campos.Count}\t{viejas}/{ClavesViejas[perfil].Length}\t{det}");
            }
            rep.AppendLine($"TOTAL\t\t\t{total}\t{totalViejas}");
            _out.WriteLine(rep.ToString());
            if (!string.IsNullOrWhiteSpace(salida)) await File.WriteAllTextAsync(salida, rep.ToString());
        }
        finally { Environment.SetEnvironmentVariable("ConnectionStrings__Propia", connPrevia); }
    }

    private static string Corto(string s) { s = s.Replace('\n', ' ').Replace('\t', ' '); return s.Length > 60 ? s[..60] + "..." : s; }
}
