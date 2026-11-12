namespace Propia.Application.Plantillas;

/// <summary>Categoria de plantilla: clave estable, nombre visible, icono fi-rr-*, color de chip y modulo donde se ofrece.</summary>
public sealed record PlantillaCategoriaDto(string Clave, string Nombre, string Icono, string Color, string UsadaEn);

/// <summary>Dato insertable {grupo.campo} con su descripcion y un valor de ejemplo (vista previa). Comun = de toda plantilla.</summary>
public sealed record PlantillaTokenDto(string Token, string Descripcion, string Ejemplo, bool Comun);

/// <summary>Plantilla del catalogo central. Modificada = UpdatedAt ?? CreatedAt. Encabezado/Pie = eleccion de
/// membrete ("pred" = predeterminado, "ninguno" o el Id de uno concreto).</summary>
public sealed record PlantillaDocumentoDto(Guid Id, string Nombre, string Categoria, string CuerpoHtml, DateTimeOffset Modificada, int Orden = 0,
    string Encabezado = MembreteEleccion.Predeterminado, string Pie = MembreteEleccion.Predeterminado);

/// <summary>Alta/edicion. En PUT es MERGE: un campo null no se toca (la tabla edita solo nombre o categoria).
/// Orden (solo alta): posicion; null = al final. "Duplicar" pasa el de la original para quedar a su lado.
/// Encabezado/Pie: "pred", "ninguno" o el Id de un membrete del tipo (null = no se toca).</summary>
public sealed record GuardarPlantillaDocumentoRequest(string? Nombre, string? Categoria, string? CuerpoHtml, int? Orden = null,
    string? Encabezado = null, string? Pie = null);

/// <summary>Valores de la eleccion de encabezado / pie de una plantilla (ademas del Id de un membrete fijo).</summary>
public static class MembreteEleccion
{
    public const string Predeterminado = "pred";
    public const string Ninguno = "ninguno";
}

/// <summary>Encabezado o pie de pagina de la biblioteca. Usos = plantillas que lo usan (fijo, o predeterminado y en
/// "Predeterminado"). Modificado = UpdatedAt ?? CreatedAt.</summary>
public sealed record MembreteDto(Guid Id, Propia.Domain.Enums.TipoMembrete Tipo, string Nombre, string Html, bool Predeterminado,
    int Usos, DateTimeOffset Modificado);

/// <summary>Biblioteca de membretes + lo que la Web necesita para pintarlos: el logo de la copropiedad ya como
/// imagen ({copropiedad.logo}) y un bloque de ejemplo de {documento.datos} (tipo, radicado y fecha del documento).</summary>
public sealed record MembretesDto(IReadOnlyList<MembreteDto> Items, string LogoHtml, string DocumentoDatosEjemplo);

/// <summary>Alta / edicion (PUT MERGE: null no se toca). Predeterminado=true lo vuelve el predeterminado de su tipo
/// (el anterior deja de serlo); false se ignora (para cambiarlo se marca otro).</summary>
public sealed record GuardarMembreteRequest(Propia.Domain.Enums.TipoMembrete? Tipo, string? Nombre, string? Html, bool? Predeterminado = null);

/// <summary>Campo propio de las plantillas (columna "+"). Misma forma que el resto de catalogos de campos
/// (CampoDinDef en la Web), asi lo consume el modal compartido CrearCampoModal.</summary>
public sealed record PlantillaCampoDefinicionDto(Guid Id, string Label, int Orden, Propia.Domain.Enums.TipoCampoTablero Tipo, string? Opciones, string? Descripcion);

/// <summary>Valor plano (plantilla + definicion) para pintar los campos propios como columnas sin N+1.</summary>
public sealed record PlantillaCampoValorFlatDto(Guid PlantillaId, Guid DefinicionId, string? Valor);
