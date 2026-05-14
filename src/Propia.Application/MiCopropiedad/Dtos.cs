using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

// ----- Identidad (seccion 1) -----
public record IdentidadDto(
    Guid Id, string Nombre, string? Nit, string? DigitoVerificacion,
    string? Direccion, string? Ciudad, string? Departamento,
    string? CodigoPropia, TipoCopropiedad? Tipo, Estrato? Estrato,
    string? FotoFachadaUrl, string? LogoUrl, string? Descripcion);

public record ActualizarIdentidadRequest(
    string Nombre, string? Nit, string? DigitoVerificacion,
    string? Direccion, string? Ciudad, string? Departamento,
    TipoCopropiedad? Tipo, Estrato? Estrato,
    string? FotoFachadaUrl, string? LogoUrl, string? Descripcion);

// ----- Distribucion: Torres + Unidades (seccion 2) -----
public record TorreDto(Guid Id, string Nombre, int? CantidadPisos, string? Descripcion, int CantidadUnidades);
public record CrearTorreRequest(string Nombre, int? CantidadPisos, string? Descripcion);

public record UnidadDto(
    Guid Id, string Numero, TipoUnidad Tipo,
    Guid? TorreId, string? TorreNombre, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones);

public record CrearUnidadRequest(
    string Numero, TipoUnidad Tipo, Guid? TorreId, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones);

// ----- Gobierno: Miembros del Consejo (seccion 4) -----
public record MiembroConsejoDto(
    Guid Id, Guid PersonaId, string PersonaNombre,
    CargoConsejo Cargo, DateOnly FechaInicio, DateOnly? FechaFin, bool Activo);

public record AgregarMiembroConsejoRequest(
    Guid PersonaId, CargoConsejo Cargo, DateOnly FechaInicio, DateOnly? FechaFin);

// ----- Servicios: Contratos (seccion 5) -----
public record ContratoServicioDto(
    Guid Id, TipoServicio Tipo, string Proveedor, string? NitProveedor, string? Contacto,
    DateOnly FechaInicio, DateOnly? FechaFin, decimal? ValorMensual, string? Observaciones);

public record CrearContratoServicioRequest(
    TipoServicio Tipo, string Proveedor, string? NitProveedor, string? Contacto,
    DateOnly FechaInicio, DateOnly? FechaFin, decimal? ValorMensual, string? Observaciones);

// ----- Zonas Comunes (seccion 6) -----
public record ZonaComunDto(
    Guid Id, string Nombre, CategoriaZonaComun Categoria, string? Descripcion,
    bool EsReservable, decimal? TarifaReserva, int? CapacidadPersonas,
    string? HorariosUso, string? ReglasUso);

public record CrearZonaComunRequest(
    string Nombre, CategoriaZonaComun Categoria, string? Descripcion,
    bool EsReservable, decimal? TarifaReserva, int? CapacidadPersonas,
    string? HorariosUso, string? ReglasUso);

// ----- Equipos / Activos (seccion 7) -----
public record EquipoActivoDto(
    Guid Id, string Nombre, CategoriaEquipo Categoria, string? Marca, string? Modelo,
    string? NumeroSerie, DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones);

public record CrearEquipoActivoRequest(
    string Nombre, CategoriaEquipo Categoria, string? Marca, string? Modelo,
    string? NumeroSerie, DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones);

// ----- Resumen / Completitud -----
public record ResumenMiCopropiedadDto(
    IdentidadDto Identidad,
    int CantidadTorres,
    int CantidadUnidades,
    decimal CoeficientesTotalPct,
    int CantidadZonasComunes,
    int CantidadEquipos,
    int CantidadContratos,
    int CantidadMiembrosConsejo,
    int CompletitudPct,
    IReadOnlyDictionary<string, bool> SeccionesCompletas);
