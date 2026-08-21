using Propia.Domain.Enums;

namespace Propia.Application.Informes;

// ---------- Plantillas ----------

public sealed record InformePlantillaSeccionDto(Guid Id, string Titulo, int Orden, string? Prompt);

public sealed record InformePlantillaDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    int NumSecciones,
    IReadOnlyList<InformePlantillaSeccionDto> Secciones);

/// <summary>Seccion a guardar dentro de una plantilla. Id null = seccion nueva.</summary>
public sealed record GuardarPlantillaSeccionRequest(Guid? Id, string Titulo, int Orden, string? Prompt);

public sealed record GuardarPlantillaRequest(
    string Nombre,
    string? Descripcion,
    List<GuardarPlantillaSeccionRequest> Secciones);

// ---------- Informes (instancias) ----------

public sealed record InformeSeccionDto(Guid Id, string Titulo, int Orden, string? Prompt, string? Contenido);

public sealed record InformeListItemDto(
    Guid Id,
    string Titulo,
    string? Periodo,
    EstadoInforme Estado,
    DateTimeOffset? GeneradoEn,
    int NumSecciones);

public sealed record InformeDetalleDto(
    Guid Id,
    Guid? PlantillaId,
    string Titulo,
    string? Periodo,
    EstadoInforme Estado,
    DateTimeOffset? GeneradoEn,
    IReadOnlyList<InformeSeccionDto> Secciones);

/// <summary>Crea un informe a partir de una plantilla (copia sus secciones) para un periodo.</summary>
public sealed record CrearInformeRequest(Guid? PlantillaId, string Titulo, string? Periodo);

/// <summary>Guarda la edicion en pantalla del contenido (y opcionalmente el prompt) de una seccion.</summary>
public sealed record GuardarInformeSeccionRequest(string? Contenido, string? Prompt);
