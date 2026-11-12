namespace Propia.Application.InfraestructuraIa;

/// <summary>
/// Modulo de Plantillas de WhatsApp (HSM) de la copropiedad (tenant-scoped, RLS). Portado de ECOREX
/// con nombre propio. El flujo principal de este corte es IMPORTAR las plantillas HSM que ya existen en
/// la cuenta YCloud de la linea; ademas se pueden listar, archivar/restaurar y hacer envios de prueba.
/// Crear/someter plantillas nuevas a Meta queda como trabajo posterior (no es parte de este corte).
/// </summary>
public interface IPlantillasWhatsAppService
{
    /// <summary>Lista las plantillas del tenant (por defecto solo las activas).</summary>
    Task<IReadOnlyList<PlantillaWhatsAppDto>> ListarAsync(bool incluirArchivadas = false, CancellationToken ct = default);

    Task<PlantillaWhatsAppDto?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Trae (importa) al tenant las plantillas HSM que ya existen en la cuenta YCloud de la linea indicada
    /// (debe ser una linea con Provider=YCloud y API key configurada). Hace UPSERT por (Name, Language): crea
    /// las nuevas, actualiza las existentes con lo que trae el proveedor. No borra nada.
    /// </summary>
    Task<ImportarPlantillasResultado> ImportarDeYCloudAsync(Guid whatsAppLineId, CancellationToken ct = default);

    /// <summary>Archiva (soft-delete) o restaura una plantilla. Devuelve false si no existe.</summary>
    Task<bool> ArchivarAsync(Guid id, bool activa, CancellationToken ct = default);

    /// <summary>
    /// Envio de PRUEBA de la plantilla a un numero. <paramref name="valores"/> = valores de las variables EN
    /// ORDEN (los que teclee el usuario); si es null/vacio se usan los ejemplos. Se manda por la linea de la
    /// plantilla (YCloud/Cloud requieren que este Aprobada; Evolution la renderiza a texto al vuelo).
    /// </summary>
    Task<EnvioPruebaResultado> ProbarEnvioAsync(Guid id, string telefono, IReadOnlyList<string>? valores = null, CancellationToken ct = default);
}
