using Propia.Domain.Enums;

namespace Propia.Application.MiCopropiedad;

// ----- Identidad (seccion 1) -----
public record IdentidadDto(
    Guid Id, string Nombre, string? Nit, string? DigitoVerificacion,
    string? Direccion, string? Ciudad, string? Departamento,
    string? CodigoPropia, TipoCopropiedad? Tipo, Estrato? Estrato,
    string? FotoFachadaUrl, string? LogoUrl, string? Descripcion,
    // Identidad registral (spec v1.0)
    string? NumeroReglamentoPh, string? NotariaRegistro,
    string? MatriculaInmobiliaria, string? LicenciaConstruccion,
    DateOnly? FechaConstitucion,
    // Labels personalizables (spec v1.0)
    string? LabelAgrupacion, string? LabelPiso,
    // Contacto (spec v1.0)
    string? TelefonoContacto = null, string? EmailContacto = null);

public record ActualizarIdentidadRequest(
    string Nombre, string? Nit, string? DigitoVerificacion,
    string? Direccion, string? Ciudad, string? Departamento,
    TipoCopropiedad? Tipo, Estrato? Estrato,
    string? FotoFachadaUrl, string? LogoUrl, string? Descripcion,
    string? NumeroReglamentoPh, string? NotariaRegistro,
    string? MatriculaInmobiliaria, string? LicenciaConstruccion,
    DateOnly? FechaConstitucion,
    string? LabelAgrupacion, string? LabelPiso,
    string? TelefonoContacto = null, string? EmailContacto = null);

// ----- Distribucion: Torres + Unidades (seccion 2) -----
public record TorreDto(Guid Id, string Nombre, int? CantidadPisos, string? Descripcion, int CantidadUnidades);
public record CrearTorreRequest(string Nombre, int? CantidadPisos, string? Descripcion);

public record UnidadDto(
    Guid Id, string Numero, TipoUnidad Tipo,
    Guid? TorreId, string? TorreNombre, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones,
    string? MatriculaInmobiliaria = null, bool PagaAdministracion = true);

public record CrearUnidadRequest(
    string Numero, TipoUnidad Tipo, Guid? TorreId, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones,
    string? MatriculaInmobiliaria = null, bool PagaAdministracion = true);

// ----- Vinculos entre unidades (seccion 2 - RN-09) -----
public record UnidadVinculoDto(
    Guid Id, Guid UnidadAsociadaId, string AsociadaNumero, TipoUnidad AsociadaTipo,
    bool IncluyeEnFacturacion);

public record CrearVinculoUnidadRequest(Guid UnidadAsociadaId, bool IncluyeEnFacturacion);

// ----- Gobierno: Miembros del Consejo (seccion 4) -----
public record MiembroConsejoDto(
    Guid Id, Guid PersonaId, string PersonaNombre,
    CargoConsejo Cargo, DateOnly FechaInicio, DateOnly? FechaFin, bool Activo);

public record AgregarMiembroConsejoRequest(
    Guid PersonaId, CargoConsejo Cargo, DateOnly FechaInicio, DateOnly? FechaFin);

// ----- Comites (seccion 4) -----
public record ComiteDto(Guid Id, string Nombre, string? Descripcion, DateOnly FechaConformacion, bool Activo, int CantidadMiembros);
public record CrearComiteRequest(string Nombre, string? Descripcion, DateOnly FechaConformacion);
public record ComiteMiembroDto(Guid Id, Guid ComiteId, Guid PersonaId, string PersonaNombre, string? CargoEnComite, bool Activo);
public record AgregarComiteMiembroRequest(Guid ComiteId, Guid PersonaId, string? CargoEnComite);

// ----- Revisor Fiscal (seccion 4) -----
public record RevisorFiscalDto(Guid Id, Guid PersonaId, string PersonaNombre, string? NumeroTarjetaProfesional, DateOnly FechaPosesion, DateOnly? FechaFin, bool Activo);
public record DesignarRevisorFiscalRequest(Guid PersonaId, string? NumeroTarjetaProfesional, DateOnly FechaPosesion);

// ----- Equipo de trabajo (seccion 3) -----
public record MiembroEquipoDto(
    Guid Id, Guid PersonaId, string PersonaNombre,
    RolEquipo Rol, string? RolPersonalizado, TipoVinculacion Tipo,
    DateOnly FechaVinculacion, DateOnly? FechaFin, bool Activo, bool EsUsuarioSistema,
    string? Telefono, string? Email);

public record AgregarMiembroEquipoRequest(
    Guid PersonaId, RolEquipo Rol, string? RolPersonalizado, TipoVinculacion Tipo,
    DateOnly FechaVinculacion, string? Telefono, string? Email, string? Observaciones);

// Para crear o buscar Persona on-the-fly al vincular un miembro de equipo
public record VincularPersonaPorDocumentoRequest(string Documento, string Nombres, string Apellidos, string? Email, string? Telefono);

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
    string? HorariosUso, string? ReglasUso,
    EstadoZonaComunMantenimiento Estado);

public record CrearZonaComunRequest(
    string Nombre, CategoriaZonaComun Categoria, string? Descripcion,
    bool EsReservable, decimal? TarifaReserva, int? CapacidadPersonas,
    string? HorariosUso, string? ReglasUso);

/// <summary>Cambia el estado operativo de una zona (RN-13/RN-14).</summary>
public record CambiarEstadoZonaRequest(EstadoZonaComunMantenimiento Estado);

// ----- Tipos de unidad personalizados (spec 2.3 - distribucion) -----
public record TipoUnidadCustomDto(Guid Id, string Nombre, bool PagaAdministracionPorDefecto, string? Descripcion, bool Activo);

public record CrearTipoUnidadCustomRequest(string Nombre, bool PagaAdministracionPorDefecto, string? Descripcion);

// ----- Generador inteligente de unidades (spec 2.3 - distribucion) -----
public enum PatronNumeracion
{
    /// <summary>Numeracion compuesta: piso + numero (ej. 101, 102, 1001, 1002).</summary>
    PisoNumero = 1,
    /// <summary>Numeracion corrida: 1, 2, 3... N.</summary>
    Corrido = 2
}

public record GenerarUnidadesRequest(
    IReadOnlyList<GeneradorTorreDto> Torres,
    PatronNumeracion Patron,
    TipoUnidad TipoUnidadDefault,
    decimal CoeficientePorUnidad);

public record GeneradorTorreDto(
    string Nombre,
    int CantidadPisos,
    int UnidadesPorPiso);

public record GenerarUnidadesResponse(
    int TorresCreadas,
    int UnidadesCreadas,
    IReadOnlyList<Guid> TorreIds,
    IReadOnlyList<Guid> UnidadIds);

// ----- Tipos de coeficiente PH (spec 2.3 - RN-02) -----
public record TipoCoeficienteDto(Guid Id, string Nombre, string? Descripcion, bool EsPrincipal, bool Activo, decimal SumaActual);

public record CrearTipoCoeficienteRequest(string Nombre, string? Descripcion);

public record UnidadCoeficienteDto(Guid TipoCoeficienteId, string TipoNombre, decimal Valor);

public record SetCoeficienteUnidadRequest(Guid TipoCoeficienteId, decimal Valor);

// ----- Importar unidades CSV (spec 2.3 - validador previo + transaccional) -----
public record ImportarUnidadesRequest(string CsvContent);

public record ImportarUnidadesResponse(
    bool Aceptado,
    int FilasLeidas,
    int UnidadesCreadas,
    decimal SumaCoeficientes,
    IReadOnlyList<ImportacionFilaError> Errores);

public record ImportacionFilaError(int Fila, string Campo, string Mensaje);

// ----- Equipos / Activos (seccion 7) -----
public record EquipoActivoDto(
    Guid Id, string Nombre, CategoriaEquipo Categoria, string? Marca, string? Modelo,
    string? NumeroSerie, DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones,
    EstadoEquipoActivo Estado);

public record CrearEquipoActivoRequest(
    string Nombre, CategoriaEquipo Categoria, string? Marca, string? Modelo,
    string? NumeroSerie, DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones);

/// <summary>Cambia el estado operativo de un equipo (seccion 7).</summary>
public record CambiarEstadoEquipoRequest(EstadoEquipoActivo Estado);

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

// ----- Finanzas (seccion 8) -----

/// <summary>Item del catalogo de monedas (ISO 4217).</summary>
public record MonedaDto(string Codigo, string Nombre, string Simbolo);

/// <summary>Parametros financieros de la copropiedad (consumidos por 2.6/2.7).</summary>
public record FinanzasParametrosDto(
    string Moneda,
    int DiaCorte,
    bool TasaMoraEsLegal,
    decimal? TasaMoraValor,
    int PeriodoGraciaDias,
    bool Configuradas,
    decimal TasaMoraMaximaLegal);  // maximo legal vigente para validar la tasa fija

/// <summary>Resumen financiero en tiempo real (se nutre de 2.6 Presupuesto y 2.7 Cartera).</summary>
public record ResumenFinancieroDto(
    decimal CuotaAdministracionVigente,
    decimal RecaudoMesPct,
    decimal PresupuestoAnualAprobado,
    decimal CarteraEnMora,
    bool HayPresupuestoVigente);

/// <summary>Vista completa de la seccion Finanzas: parametros + resumen.</summary>
public record FinanzasDto(
    FinanzasParametrosDto Parametros,
    ResumenFinancieroDto Resumen);

public record ActualizarFinanzasRequest(
    string Moneda,
    int DiaCorte,
    bool TasaMoraEsLegal,
    decimal? TasaMoraValor,
    int PeriodoGraciaDias);
