namespace Propia.Application.Plantillas;

/// <summary>Categoria de plantilla: clave estable, nombre visible, icono fi-rr-*, color de chip y modulo donde se ofrece.</summary>
public sealed record PlantillaCategoriaDto(string Clave, string Nombre, string Icono, string Color, string UsadaEn);

/// <summary>Dato insertable {grupo.campo} con su descripcion y un valor de ejemplo (vista previa). Comun = de toda plantilla.</summary>
public sealed record PlantillaTokenDto(string Token, string Descripcion, string Ejemplo, bool Comun);

/// <summary>Plantilla del catalogo central. Modificada = UpdatedAt ?? CreatedAt.</summary>
public sealed record PlantillaDocumentoDto(Guid Id, string Nombre, string Categoria, string CuerpoHtml, DateTimeOffset Modificada, int Orden = 0);

/// <summary>Alta/edicion. En PUT es MERGE: un campo null no se toca (la tabla edita solo nombre o categoria).
/// Orden (solo alta): posicion; null = al final. "Duplicar" pasa el de la original para quedar a su lado.</summary>
public sealed record GuardarPlantillaDocumentoRequest(string? Nombre, string? Categoria, string? CuerpoHtml, int? Orden = null);
