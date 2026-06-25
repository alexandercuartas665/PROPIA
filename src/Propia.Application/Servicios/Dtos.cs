using Propia.Domain.Enums;

namespace Propia.Application.Servicios;

// ----- Servicio -----
public record ServicioDto(
    Guid Id, TipoServicio Tipo, string Nombre, string? Descripcion,
    Guid? EjecutorPersonaId, Guid? EjecutorEmpresaId, string? EjecutorNombre,
    decimal? CostoMensual, decimal? CostoAnual, EstadoServicio Estado,
    int CantidadContratos, int CantidadAdjuntos, int CantidadContactos);

public record CrearServicioRequest(
    TipoServicio Tipo, string Nombre, string? Descripcion,
    Guid? EjecutorPersonaId, Guid? EjecutorEmpresaId, string? EjecutorNombre,
    decimal? CostoMensual, decimal? CostoAnual);

public record ActualizarServicioRequest(
    TipoServicio Tipo, string Nombre, string? Descripcion,
    Guid? EjecutorPersonaId, Guid? EjecutorEmpresaId, string? EjecutorNombre,
    decimal? CostoMensual, decimal? CostoAnual, EstadoServicio Estado);

// ----- Contactos del servicio (cada contacto es un tercero del Directorio) -----
public record ServicioContactoDto(
    Guid Id, Guid? PersonaId, Guid? EmpresaId, string NombreSnapshot,
    string? Rol, string? Telefono, string? Email);

public record AgregarServicioContactoRequest(
    Guid? PersonaId, Guid? EmpresaId, string NombreSnapshot,
    string? Rol, string? Telefono, string? Email);

// ----- Adjuntos (servicio y contrato comparten DTO) -----
public record AdjuntoDto(
    Guid Id, string NombreArchivo, string TipoMime, long TamanoBytes,
    string Url, DateTimeOffset CreatedAt);

/// <summary>Subida via base64 (mismo patron que Documentos 2.15). Admite prefijo data:...;base64,.</summary>
public record SubirAdjuntoRequest(string NombreArchivo, string TipoMime, string ContenidoBase64);

// ----- Resumen de contrato para el detalle del servicio -----
public record ContratoResumenDto(
    Guid Id, TipoServicio Tipo, string Proveedor, DateOnly FechaInicio, DateOnly? FechaFin,
    decimal? ValorMensual, EstadoContrato Estado, bool RenovacionAutomatica, int CantidadAdjuntos);

public record ServicioDetalleDto(
    ServicioDto Servicio,
    IReadOnlyList<ServicioContactoDto> Contactos,
    IReadOnlyList<AdjuntoDto> Adjuntos,
    IReadOnlyList<ContratoResumenDto> Contratos,
    IReadOnlyList<AlertaServicioDto> Alertas);

// ----- Alertas de un servicio (se guardan como AlertaCopropiedad y salen en el dashboard) -----
public record AlertaServicioDto(
    Guid Id, string Titulo, string? Descripcion, SeveridadAlerta Severidad, bool Activa, DateTimeOffset CreatedAt);

public record AgregarAlertaServicioRequest(string Titulo, string? Descripcion, SeveridadAlerta Severidad);
