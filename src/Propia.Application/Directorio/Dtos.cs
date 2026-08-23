using Propia.Domain.Enums;

namespace Propia.Application.Directorio;

// ----- Etiqueta (chip liviano para tarjetas + filtro en el listado) -----
public record EtiquetaChipDto(Guid EtiquetaId, string Nombre, GrupoEtiqueta Grupo, string? Icono, string? Color);

// ----- Persona -----
public record PersonaResumenDto(
    Guid Id, TipoDocumento TipoDocumento, string Documento,
    string Nombres, string Apellidos, string? Email, string? Telefono,
    string? FotoUrl, bool PerfilIncompleto, EstadoDirectorio Estado,
    IReadOnlyList<EtiquetaChipDto>? Etiquetas = null);

public record PersonaDetalleDto(
    Guid Id, TipoDocumento TipoDocumento, string Documento,
    string Nombres, string Apellidos, string? Email, string? Telefono,
    string? FotoUrl, DateOnly? FechaNacimiento, GeneroPersona? Genero,
    bool AceptoTratamientoDatos, DateTimeOffset? FechaAceptacionDatos,
    bool PerfilIncompleto, EstadoDirectorio Estado);

public record BuscarPorDocumentoRequest(TipoDocumento TipoDocumento, string Documento);

public record CrearPersonaRequest(
    TipoDocumento TipoDocumento, string Documento,
    string Nombres, string Apellidos,
    string? Email, string? Telefono,
    DateOnly? FechaNacimiento, GeneroPersona? Genero,
    // Contactos ricos (correos/telefonos/direcciones) capturados en el alta. Opcional: si viene,
    // se persisten a directorio_contactos y el principal sincroniza Email/Telefono de la persona.
    IReadOnlyList<ContactoRapidoDto>? Contactos = null);

public record ActualizarPersonaRequest(
    string Nombres, string Apellidos,
    string? Email, string? Telefono,
    string? FotoUrl, DateOnly? FechaNacimiento, GeneroPersona? Genero);

// ----- Empresa -----
public record EmpresaResumenDto(
    Guid Id, string Nit, string? DigitoVerificacion,
    string RazonSocial, string? NombreComercial,
    string? Email, string? Telefono, string? LogoUrl,
    bool PerfilIncompleto, EstadoDirectorio Estado,
    IReadOnlyList<EtiquetaChipDto>? Etiquetas = null);

public record EmpresaDetalleDto(
    Guid Id, string Nit, string? DigitoVerificacion,
    string RazonSocial, string? NombreComercial,
    string? Email, string? Telefono, string? Direccion,
    string? TipoEmpresa, string? SectorEconomico, string? RegimenTributario,
    string? SitioWeb, string? LogoUrl,
    Guid? RepresentanteLegalPersonaId, string? RepresentanteLegalNombre,
    bool PerfilIncompleto, EstadoDirectorio Estado);

public record CrearEmpresaRequest(
    string Nit, string? DigitoVerificacion,
    string RazonSocial, string? NombreComercial,
    string? Email, string? Telefono, string? Direccion,
    string? TipoEmpresa, string? SectorEconomico, string? RegimenTributario,
    string? SitioWeb,
    // Contactos ricos capturados en el alta (mismo trato que en persona).
    IReadOnlyList<ContactoRapidoDto>? Contactos = null);

public record ActualizarEmpresaRequest(
    string RazonSocial, string? NombreComercial,
    string? Email, string? Telefono, string? Direccion,
    string? TipoEmpresa, string? SectorEconomico, string? RegimenTributario,
    string? SitioWeb, string? LogoUrl, Guid? RepresentanteLegalPersonaId);

// ----- Vinculo -----
public record VinculoDto(
    Guid Id, EntidadDirectorio EntidadTipo, Guid EntidadId,
    string EntidadNombre, string? EntidadDocumentoONit,
    DateOnly FechaDesde, DateOnly? FechaHasta,
    EstadoVinculo Estado, IReadOnlyList<EtiquetaAsignadaDto> Etiquetas);

public record EtiquetaAsignadaDto(Guid Id, Guid EtiquetaId, string Codigo, string Nombre, GrupoEtiqueta Grupo, string? Icono = null, string? Color = null);

public record CrearVinculoRequest(
    EntidadDirectorio EntidadTipo, Guid EntidadId,
    DateOnly FechaDesde, IReadOnlyList<Guid>? EtiquetaIds);

public record AsignarEtiquetaRequest(Guid VinculoId, Guid EtiquetaId);

// ----- Catalogo de etiquetas -----
public record EtiquetaCatalogoDto(
    Guid Id, string Codigo, string Nombre,
    GrupoEtiqueta Grupo, AplicaEtiqueta AplicaA,
    bool EsBase, bool TieneLogicaEspecial, bool Activo,
    string? Icono = null, string? Color = null, int Orden = 0);

public record CrearEtiquetaCustomRequest(
    string Nombre, GrupoEtiqueta Grupo, AplicaEtiqueta AplicaA,
    string? Icono = null, string? Color = null);

/// <summary>Edita una etiqueta custom (nombre, icono, color). Las base no se editan.</summary>
public record EditarEtiquetaRequest(string Nombre, string? Icono, string? Color);

// ----- Contactos -----
public record ContactoDto(
    Guid Id, EntidadDirectorio EntidadTipo, Guid EntidadId,
    TipoContacto Tipo, string? SubtipoLabel, string Valor,
    string? Ciudad, string? Departamento,
    bool EsPrincipal, VisibilidadContacto Visibilidad, bool Activo);

public record AgregarContactoRequest(
    EntidadDirectorio EntidadTipo, Guid EntidadId,
    TipoContacto Tipo, string? SubtipoLabel, string Valor,
    string? Ciudad, string? Departamento,
    bool EsPrincipal, VisibilidadContacto Visibilidad);

// ----- Vista 360 -----
public record Persona360Dto(
    PersonaDetalleDto Persona,
    IReadOnlyList<VinculoDto> VinculosEnCopropiedad,
    IReadOnlyList<ContactoDto> Contactos);

public record Empresa360Dto(
    EmpresaDetalleDto Empresa,
    IReadOnlyList<VinculoDto> VinculosEnCopropiedad,
    IReadOnlyList<ContactoDto> Contactos,
    IReadOnlyList<EquipoEmpresaDto> Equipo);

public record EquipoEmpresaDto(
    Guid Id, Guid PersonaId, string PersonaNombre, string PersonaDocumento,
    string? Cargo, bool EsRepresentanteLegal, bool EsContactoPrincipal, EstadoDirectorio Estado);

// ---------- Adjuntos (documentos de la identidad: RUT, camara de comercio, certificados) ----------
public record DirectorioAdjuntoDto(
    Guid Id, string Nombre, string Url, string? ContentType, long TamanoBytes, DateTimeOffset CreatedAt);
