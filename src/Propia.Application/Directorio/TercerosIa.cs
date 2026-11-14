using Propia.Domain.Enums;

namespace Propia.Application.Directorio;

/// <summary>
/// Datos que la IA leyo de un documento de un tercero (perfil "tercero": RUT, Camara de Comercio, certificacion
/// bancaria). Todo opcional: el servicio decide si es empresa (NIT + razon social) o persona (documento + nombres).
/// </summary>
public sealed record TerceroLeidoRequest(
    string? TipoDocumentoFuente = null, string? TipoPersona = null,
    string? Nit = null, string? RazonSocial = null, string? NombreComercial = null,
    string? TipoIdentificacion = null, string? NumeroIdentificacion = null, string? Nombres = null, string? Apellidos = null,
    string? Direccion = null, string? Ciudad = null, string? Email = null, string? Telefono = null,
    string? ActividadEconomica = null, string? RegimenTributario = null,
    string? RepresentanteNombres = null, string? RepresentanteApellidos = null,
    string? RepresentanteTipoIdentificacion = null, string? RepresentanteIdentificacion = null,
    string? Banco = null, string? TipoCuenta = null, string? NumeroCuenta = null);

/// <summary>Que paso con el documento: el tercero (empresa o persona) creado o actualizado y el documento asociado.</summary>
public sealed record TerceroDesdeDocumentoResultado(
    EntidadDirectorio Tipo, Guid Id, string Nombre, bool Creado, string DocumentoAsociado, string? RepresentanteLegal);

/// <summary>
/// "Crear terceros con IA": con lo que leyo la IA de un documento crea el tercero en el Directorio (o actualiza el
/// que ya existe, completando solo los datos que le faltan), lo vincula a la copropiedad, vincula al representante
/// legal (Camara de Comercio) y deja el archivo en sus Documentos.
/// </summary>
public interface ITercerosIaService
{
    Task<TerceroDesdeDocumentoResultado> CrearDesdeDocumentoAsync(TerceroLeidoRequest datos, string nombreArchivo, string? contentType,
        long tamanoBytes, Stream contenido, CancellationToken ct);

    /// <summary>Agrega al tercero los contactos que anoto quien lo registro (personas de la empresa con telefono y
    /// correo, y correos de facturacion). Solo agrega: no repite un telefono o correo que el tercero ya tenga.
    /// Devuelve cuantos contactos quedaron guardados.</summary>
    Task<int> AgregarContactosAsync(EntidadDirectorio tipo, Guid id, IReadOnlyList<ContactoTerceroRequest> contactos,
        IReadOnlyList<string> correosFacturacion, CancellationToken ct);
}

/// <summary>Persona de contacto de un tercero (sin documento: queda como contacto, no como persona del Directorio).</summary>
public sealed record ContactoTerceroRequest(string? Nombre = null, string? Cargo = null, string? Telefono = null, string? Correo = null);
