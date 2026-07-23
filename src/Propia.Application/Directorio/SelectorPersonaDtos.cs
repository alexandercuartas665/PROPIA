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
    IReadOnlyList<string> Copropiedades,
    // Persona natural o empresa (dueno/tercero juridico). PersonaId lleva el id de la entidad
    // segun este tipo. Se agrega al final para no romper las construcciones existentes.
    EntidadDirectorio EntidadTipo = EntidadDirectorio.Persona,
    // Contactos del candidato ELEGIDO (correos/telefonos/direcciones globales), para que el
    // formulario los precargue. En la lista de busqueda va null: solo se llena al seleccionar.
    IReadOnlyList<ContactoRapidoDto>? Contactos = null);

/// <summary>
/// Un dato de contacto que el selector/formulario transporta (correo, telefono o direccion).
/// Es la forma "ligera" de un DirectorioContacto, pensada para precargar y guardar desde las fichas.
/// </summary>
public record ContactoRapidoDto(
    TipoContacto Tipo,          // Email | Telefono | Direccion
    string Valor,               // correo / "+57 300 000 0000" / direccion (texto completo)
    string? Etiqueta = null,    // SubtipoLabel: Personal, Trabajo, Facturacion...
    string? Ciudad = null,      // solo direcciones
    bool Principal = false);

/// <summary>
/// Reemplaza en bloque los contactos (correos/telefonos/direcciones) de una entidad del directorio.
/// El backend borra los del mismo Tipo que ya tenia y deja exactamente los enviados; ademas sincroniza
/// el Email/Telefono/Direccion "principal" denormalizado de la Persona/Empresa.
/// </summary>
public record ReemplazarContactosRequest(
    EntidadDirectorio EntidadTipo,
    Guid EntidadId,
    IReadOnlyList<ContactoRapidoDto> Contactos);

/// <summary>Alta de persona desde el propio selector, sin salir del formulario.</summary>
public record CrearPersonaRapidaRequest(
    TipoDocumento TipoDocumento,
    string Documento,
    string Nombres,
    string Apellidos,
    string? Email,
    string? Telefono);
