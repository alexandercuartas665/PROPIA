using System.Security.Cryptography;
using System.Text;

namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Reglas compartidas de las plantillas de agente (Super Admin) y sus copias en las copropiedades:
/// claves de los agentes del sistema, reemplazo de placeholders y huella (hash) del prompt aplicado.
/// La huella se calcula igual aqui y en la migracion (sha256 hex en minusculas del texto UTF-8).
/// </summary>
public static class PlantillaAgente
{
    /// <summary>Agente que lee documentos (contratos, polizas, recibos de servicios publicos).</summary>
    public const string ClaveDocumental = "agente-documental";
    /// <summary>Agente que redacta/completa campos de texto del sistema.</summary>
    public const string ClaveAuxiliar = "auxiliar-administrativo";
    /// <summary>Redactor de actas de asamblea (modulo 2.8): redacta el acta con la transcripcion y los datos de la sesion.</summary>
    public const string ClaveActaAsamblea = "redactor-actas";
    /// <summary>Corrector de redaccion: mejora el texto de las cajas de texto (dictado o escrito) si el usuario lo pide.</summary>
    public const string ClaveCorrectorTexto = "corrector-redaccion";

    /// <summary>{COPROPIEDAD_NOMBRE} y {ORGANIZACION_NOMBRE} (sin organizacion, el de la copropiedad).</summary>
    public static string Render(string? texto, string copropiedadNombre, string? organizacionNombre)
        => (texto ?? "")
            .Replace("{COPROPIEDAD_NOMBRE}", copropiedadNombre, StringComparison.Ordinal)
            .Replace("{ORGANIZACION_NOMBRE}", organizacionNombre ?? copropiedadNombre, StringComparison.Ordinal);

    /// <summary>Huella del prompt para saber si la copropiedad lo edito despues de aplicarlo.</summary>
    public static string Hash(string? prompt)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt ?? ""))).ToLowerInvariant();
}
