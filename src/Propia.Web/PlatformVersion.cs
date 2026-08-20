using System.Reflection;

namespace Propia.Web;

/// <summary>
/// Version desplegada de la plataforma, para mostrar en el login y el footer y saber que build esta
/// arriba tras un deploy. El SHA de git sale de la env var de Railway (RAILWAY_GIT_COMMIT_SHA) o, si no
/// esta, del AssemblyInformationalVersion (que el SDK anexa el SHA cuando hay git en el build). La fecha
/// se sella en el csproj (AssemblyMetadata BuildTimestampUtc) en cada build.
/// </summary>
public static class PlatformVersion
{
    /// <summary>SHA corto del commit desplegado (7 chars) o "dev" si no se pudo resolver.</summary>
    public static string Sha { get; }

    /// <summary>Fecha/hora del build en UTC (yyyy-MM-dd HH:mm).</summary>
    public static string BuildDate { get; }

    /// <summary>Etiqueta compacta para mostrar: "a1b076f - 2026-08-19 14:58 UTC".</summary>
    public static string Label =>
        string.IsNullOrEmpty(BuildDate) ? $"v {Sha}" : $"v {Sha} - {BuildDate} UTC";

    static PlatformVersion()
    {
        var asm = typeof(PlatformVersion).Assembly;

        // 1) SHA: primero la env de Railway (deploy por git), luego el informational version del build.
        var envSha = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA");
        if (!string.IsNullOrWhiteSpace(envSha))
        {
            Sha = Trunc(envSha.Trim(), 7);
        }
        else
        {
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var plus = info.IndexOf('+');
            Sha = (plus >= 0 && plus + 1 < info.Length) ? Trunc(info[(plus + 1)..], 7) : "dev";
        }

        // 2) Fecha del build, sellada por el csproj.
        BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestampUtc")?.Value ?? "";
    }

    private static string Trunc(string s, int n) => s.Length > n ? s[..n] : s;
}
