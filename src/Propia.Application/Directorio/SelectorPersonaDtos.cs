using Propia.Domain.Enums;

namespace Propia.Application.Directorio;

/// <summary>
/// Un candidato del selector de personas.
///
/// EnEstaCopropiedad decide el comportamiento del control:
///  true  -> se asigna directo.
///  false -> la persona existe en otra copropiedad de la organizacion; al elegirla se le
///           crea el vinculo con la actual (sin duplicar la persona, que es global).
///
/// Deliberadamente NO expone email ni telefono: el autocompletado puede mostrar gente de
/// otras copropiedades y no hace falta filtrar datos de contacto para elegir a alguien.
/// El documento va enmascarado por lo mismo.
/// </summary>
public record PersonaCandidatoDto(
    Guid PersonaId,
    string NombreCompleto,
    TipoDocumento TipoDocumento,
    string DocumentoEnmascarado,
    bool EnEstaCopropiedad,
    IReadOnlyList<string> Copropiedades);

/// <summary>Alta de persona desde el propio selector, sin salir del formulario.</summary>
public record CrearPersonaRapidaRequest(
    TipoDocumento TipoDocumento,
    string Documento,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono);
