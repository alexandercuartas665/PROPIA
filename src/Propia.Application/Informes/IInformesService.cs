namespace Propia.Application.Informes;

/// <summary>
/// Modulo Informes de gestion (Capa 2). Plantillas inteligentes: cada seccion trae su prompt y el
/// sistema genera el contenido con los agentes de IA existentes (Auxiliar Administrativo + proveedor
/// LLM global). MVP: generar + editar en pantalla (sin PDF).
/// </summary>
public interface IInformesService
{
    // ---------- Plantillas ----------
    Task<IReadOnlyList<InformePlantillaDto>> ListarPlantillasAsync(CancellationToken ct);
    Task<InformePlantillaDto?> GetPlantillaAsync(Guid id, CancellationToken ct);
    Task<InformePlantillaDto> CrearPlantillaAsync(GuardarPlantillaRequest req, CancellationToken ct);
    Task<bool> ActualizarPlantillaAsync(Guid id, GuardarPlantillaRequest req, CancellationToken ct);
    Task<bool> EliminarPlantillaAsync(Guid id, CancellationToken ct);
    /// <summary>Siembra una plantilla base de ejemplo la primera vez (si no hay ninguna). Devuelve cuantas creo.</summary>
    Task<int> SembrarPlantillasBaseAsync(CancellationToken ct);

    // ---------- Informes (instancias) ----------
    Task<IReadOnlyList<InformeListItemDto>> ListarInformesAsync(CancellationToken ct);
    Task<InformeDetalleDto?> GetInformeAsync(Guid id, CancellationToken ct);
    /// <summary>Crea un informe copiando las secciones (titulo+prompt) de la plantilla indicada.</summary>
    Task<InformeDetalleDto?> CrearInformeAsync(CrearInformeRequest req, CancellationToken ct);
    Task<bool> EliminarInformeAsync(Guid id, CancellationToken ct);
    /// <summary>Guarda la edicion en pantalla del contenido (y prompt) de una seccion del informe.</summary>
    Task<bool> GuardarSeccionAsync(Guid informeId, Guid seccionId, GuardarInformeSeccionRequest req, CancellationToken ct);

    // ---------- Generacion con IA ----------
    /// <summary>Genera con IA el contenido de UNA seccion (usando su prompt + contexto de la copropiedad).</summary>
    Task<InformeSeccionDto?> GenerarSeccionAsync(Guid informeId, Guid seccionId, CancellationToken ct);
    /// <summary>Genera con IA TODAS las secciones del informe, una por una.</summary>
    Task<InformeDetalleDto?> GenerarInformeAsync(Guid informeId, CancellationToken ct);
}
