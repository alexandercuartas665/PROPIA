using System.Reflection;

namespace Propia.Web;

/// <summary>
/// Version desplegada de la plataforma, para mostrar en el login y el sidebar y saber que version
/// esta arriba tras un deploy. El indicador VISIBLE es un semver manual (v0.0.1, v0.0.2, ...) que se
/// bumpea en el csproj (&lt;Version&gt;) en cada deploy. Como respaldo de trazabilidad exacta se conserva
/// el SHA de git (env RAILWAY_GIT_COMMIT_SHA o AssemblyInformationalVersion tras '+') y la fecha del
/// build (AssemblyMetadata BuildTimestampUtc), que van en el tooltip (propiedad Build).
/// </summary>
public static class PlatformVersion
{
    /// <summary>Version semantica manual (Major.Minor.Patch), del &lt;Version&gt; del csproj. Ej. "0.0.1".</summary>
    public static string SemVer { get; }

    /// <summary>SHA corto del commit desplegado (7 chars) o "dev" si no se pudo resolver.</summary>
    public static string Sha { get; }

    /// <summary>Fecha/hora del build en UTC (yyyy-MM-dd HH:mm).</summary>
    public static string BuildDate { get; }

    /// <summary>Etiqueta VISIBLE compacta: "v0.0.1".</summary>
    public static string Label => $"v{SemVer}";

    /// <summary>Detalle para el tooltip: SHA + fecha del build (trazabilidad exacta). Ej. "764f233 - 2026-08-20 08:52 UTC".</summary>
    public static string Build =>
        string.IsNullOrEmpty(BuildDate) ? Sha : $"{Sha} - {BuildDate} UTC";

    static PlatformVersion()
    {
        var asm = typeof(PlatformVersion).Assembly;

        // 1) Semver manual: sale del <Version> del csproj (AssemblyVersion = Major.Minor.Patch.0).
        var v = asm.GetName().Version;
        SemVer = v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";

        // 2) SHA: primero la env de Railway (deploy por git), luego el informational version del build.
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

        // 3) Fecha del build, sellada por el csproj.
        BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimestampUtc")?.Value ?? "";
    }

    private static string Trunc(string s, int n) => s.Length > n ? s[..n] : s;
}
