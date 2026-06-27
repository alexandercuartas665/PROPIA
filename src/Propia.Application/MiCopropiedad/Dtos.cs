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
    string? TelefonoContacto = null, string? EmailContacto = null,
    string? Pais = null);

public record ActualizarIdentidadRequest(
    string Nombre, string? Nit, string? DigitoVerificacion,
    string? Direccion, string? Ciudad, string? Departamento,
    TipoCopropiedad? Tipo, Estrato? Estrato,
    string? FotoFachadaUrl, string? LogoUrl, string? Descripcion,
    string? NumeroReglamentoPh, string? NotariaRegistro,
    string? MatriculaInmobiliaria, string? LicenciaConstruccion,
    DateOnly? FechaConstitucion,
    string? LabelAgrupacion, string? LabelPiso,
    string? TelefonoContacto = null, string? EmailContacto = null,
    string? Pais = null);

// ----- Distribucion: Torres + Unidades (seccion 2) -----
public record TorreDto(Guid Id, string Nombre, int? CantidadPisos, string? Descripcion, int CantidadUnidades);
public record CrearTorreRequest(string Nombre, int? CantidadPisos, string? Descripcion);

public record UnidadDto(
    Guid Id, string Numero, TipoUnidad Tipo,
    Guid? TorreId, string? TorreNombre, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones,
    string? MatriculaInmobiliaria = null, bool PagaAdministracion = true,
    decimal? CuotaMensual = null);

public record CrearUnidadRequest(
    string Numero, TipoUnidad Tipo, Guid? TorreId, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones,
    string? MatriculaInmobiliaria = null, bool PagaAdministracion = true,
    decimal? CuotaMensual = null);

/// <summary>Actualiza la ficha completa de una unidad (todos los campos editables).</summary>
public record ActualizarUnidadRequest(
    string Numero, TipoUnidad Tipo, Guid? TorreId, int? Piso,
    decimal CoeficientePropiedad, decimal? AreaM2,
    int? Habitaciones, int? Banos, int? Parqueaderos,
    string? Estado, string? Observaciones,
    string? MatriculaInmobiliaria, bool PagaAdministracion,
    decimal? CuotaMensual = null);

// ----- Vinculos entre unidades (seccion 2 - RN-09) -----
public record UnidadVinculoDto(
    Guid Id, Guid UnidadAsociadaId, string AsociadaNumero, TipoUnidad AsociadaTipo,
    bool IncluyeEnFacturacion);

public record CrearVinculoUnidadRequest(Guid UnidadAsociadaId, bool IncluyeEnFacturacion);

// ----- Personas vinculadas a una unidad (Propietario/Residente/Familiar/Arrendatario/Apoderado) -----
public record UnidadPersonaDto(
    Guid Id, Guid PersonaId, string PersonaNombre, string PersonaDocumento,
    string? PersonaEmail, string? PersonaTelefono,
    RolUnidadPersona Rol, bool Habita, string? Parentesco,
    string PersonaNombres = "", string PersonaApellidos = "");

public record AgregarPersonaUnidadRequest(
    string Documento, string Nombres, string Apellidos,
    string? Email, string? Telefono,
    RolUnidadPersona Rol, bool Habita = true, string? Parentesco = null);

// ----- Campos personalizados de unidad: la DEFINICION es por copropiedad (catalogo compartido,
//       aplica a todas las unidades) y el VALOR es por unidad. -----
public record UnidadCampoDefinicionDto(Guid Id, string Label, int Orden);
public record CrearCampoDefinicionRequest(string Label);
public record UnidadCampoDto(Guid DefinicionId, string Label, int Orden, string? Valor);
public record SetCampoValorRequest(string? Valor);

// ----- Documentos / anexos de una unidad (archivo en blob + nombre) -----
public record UnidadDocumentoDto(Guid Id, string Nombre, string Url, long Tamano);

// ----- Prototipo v3, ficha de inmueble: bloques nuevos por unidad -----

// Placas habilitadas para ingreso (control de acceso de Porteria 2.12).
public record UnidadPlacaDto(Guid Id, string Placa, TipoVehiculo TipoVehiculo);
public record CrearUnidadPlacaRequest(string Placa, TipoVehiculo TipoVehiculo);

// Arriendos y cobros mensuales de la unidad.
public record UnidadArriendoDto(Guid Id, string Concepto, decimal ValorMensual, string? Referencia);
public record CrearUnidadArriendoRequest(string Concepto, decimal ValorMensual, string? Referencia);

// Mascotas registradas en la unidad.
public record UnidadMascotaDto(Guid Id, string Nombre, TipoMascota Tipo, string? Raza);
public record CrearUnidadMascotaRequest(string Nombre, TipoMascota Tipo, string? Raza);

// Empleada(s) de servicio de la unidad.
public record UnidadEmpleadaDto(Guid Id, string Nombre, string? Documento, string? Celular, string? Horario);
public record CrearUnidadEmpleadaRequest(string Nombre, string? Documento, string? Celular, string? Horario);

/// <summary>
/// Cuota consolidada: coeficiente y cuota total de una unidad principal sumando
/// sus unidades asociadas con IncluyeEnFacturacion=true (RN-09).
/// </summary>
public record CuotaConsolidadaDto(
    Guid UnidadId, string Numero,
    decimal CoefPrincipal, decimal CoefAsociadasFactura, decimal CoefTotal,
    int CantidadAsociadas, int CantidadAsociadasFactura);

/// <summary>Resultado del rebalanceo de coeficientes (Ley 675 art. 26: suma = 100%).</summary>
public record RebalanceoCoeficientesDto(
    int UnidadesActualizadas, decimal SumaAnterior, decimal SumaNueva,
    decimal CoefPorApartamento, int Apartamentos, int NoPagan);

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
    DateOnly FechaInicio, DateOnly? FechaFin, decimal? ValorMensual, string? Observaciones,
    EstadoContrato Estado, int DiasAnticipacionAlerta,
    int? DiasParaVencer, bool AlertaVencimiento,
    bool RenovacionAutomatica = false, Guid? ServicioId = null, Guid? ExpedienteId = null,
    Guid? ProyectoTareaId = null, int CantidadAdjuntos = 0);

public record CrearContratoServicioRequest(
    TipoServicio Tipo, string Proveedor, string? NitProveedor, string? Contacto,
    DateOnly FechaInicio, DateOnly? FechaFin, decimal? ValorMensual, string? Observaciones,
    int DiasAnticipacionAlerta = 30,
    bool RenovacionAutomatica = false, Guid? ServicioId = null, Guid? ExpedienteId = null,
    Guid? ProyectoTareaId = null);

/// <summary>
/// Actualiza un contrato. Estado + dias siempre se aplican. Los demas campos son opcionales:
/// si vienen con valor se actualizan (MERGE, conserva los no provistos) - asi el editor de la
/// pagina edita todo y la tool MCP (que solo manda estado+dias) sigue funcionando igual.
/// Los vinculos (servicio/expediente/proyecto/renovacion) solo se aplican si
/// ActualizarVinculos=true, asi la tool MCP no los borra al actualizar estado+dias.
/// </summary>
public record ActualizarContratoRequest(
    EstadoContrato Estado, int DiasAnticipacionAlerta,
    TipoServicio? Tipo = null, string? Proveedor = null, string? NitProveedor = null,
    string? Contacto = null, DateOnly? FechaInicio = null, DateOnly? FechaFin = null,
    decimal? ValorMensual = null, string? Observaciones = null,
    bool ActualizarVinculos = false, bool RenovacionAutomatica = false,
    Guid? ServicioId = null, Guid? ExpedienteId = null, Guid? ProyectoTareaId = null);

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

// ----- Equipos / Activos (seccion 5) -----
public record EquipoActivoDto(
    Guid Id, string Nombre, CategoriaEquipo Categoria,
    TipoElemento Tipo, int Cantidad, bool EsReservable,
    string? Marca, string? Modelo,
    string? NumeroSerie, DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones,
    int? VidaUtilAnios, DateOnly? FechaAdquisicion, decimal? ValorAdquisicion,
    string? Proveedor, string? NumeroFactura,
    EstadoEquipoActivo Estado);

/// <summary>Creacion basica (lo minimo): nombre + cantidad + tipo + reservable + categoria.</summary>
public record CrearEquipoActivoRequest(
    string Nombre, CategoriaEquipo Categoria,
    TipoElemento Tipo, int Cantidad, bool EsReservable);

/// <summary>Actualiza la ficha tecnica completa de un equipo/activo existente.</summary>
public record ActualizarEquipoActivoRequest(
    string Nombre, CategoriaEquipo Categoria,
    TipoElemento Tipo, int Cantidad, bool EsReservable,
    string? Modelo, string? NumeroSerie,
    DateOnly? FechaInstalacion, DateOnly? GarantiaHasta,
    string? Ubicacion, string? Observaciones,
    int? VidaUtilAnios, DateOnly? FechaAdquisicion, decimal? ValorAdquisicion,
    string? Proveedor, string? NumeroFactura);

// ----- Ventanas de disponibilidad (reservas: zonas comunes y equipos reservables) -----
public record VentanaDisponibilidadDto(
    Guid Id, TipoEntidadDisponibilidad TipoEntidad, Guid EntidadId,
    DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activa);

public record GuardarVentanasRequest(
    TipoEntidadDisponibilidad TipoEntidad, Guid EntidadId,
    IReadOnlyList<NuevaVentanaItem> Ventanas);

public record NuevaVentanaItem(DiaSemana DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activa);

/// <summary>Cambia el estado operativo de un equipo (seccion 7).</summary>
public record CambiarEstadoEquipoRequest(EstadoEquipoActivo Estado);

// ----- Ficha tecnica completa del equipo/activo (modal grande estilo prototipo) -----

/// <summary>Valor en libros y depreciacion lineal calculada del activo.</summary>
public record DepreciacionDto(
    decimal CostoAdquisicion, decimal MejorasCapitalizadas, decimal Base,
    int VidaUtilAnios, int AniosUso, decimal DepreciacionAnual,
    decimal DepreciacionAcumulada, decimal ValorEnLibros, int PctDepreciado);

public record EquipoFotoDto(Guid Id, string Url);
public record EquipoMejoraDto(Guid Id, string Descripcion, decimal Valor, DateOnly Fecha, string? DocumentoUrl);
public record EquipoCampoDto(Guid Id, string Label, string? Valor);
public record EquipoRefDto(Guid Id, string Nombre, string? Meta);
public record MantenimientoRefDto(Guid Id, string Tipo, string Titulo, string? Responsable, DateOnly? Fecha, string Estado, Guid? TareaId);
public record TareaRefDto(Guid Id, string Titulo, string Tablero, string Estado);

/// <summary>Agregado que alimenta el modal de ficha tecnica del activo.</summary>
public record EquipoFichaDto(
    EquipoActivoDto Equipo,
    DepreciacionDto Depreciacion,
    IReadOnlyList<EquipoFotoDto> Fotos,
    IReadOnlyList<EquipoMejoraDto> Mejoras,
    IReadOnlyList<EquipoCampoDto> CamposPersonalizados,
    IReadOnlyList<EquipoRefDto> ContratosVinculados,
    IReadOnlyList<EquipoRefDto> ContratosDisponibles,
    IReadOnlyList<EquipoRefDto> ActivosVinculados,
    IReadOnlyList<EquipoRefDto> ActivosDisponibles,
    IReadOnlyList<MantenimientoRefDto> Mantenimientos,
    IReadOnlyList<TareaRefDto> Tareas);

public record AgregarMejoraRequest(string Descripcion, decimal Valor, DateOnly Fecha);
public record ToggleVinculoRequest(Guid Id, bool Vincular);
public record AgregarCampoRequest(string Label, string? Valor);

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

// ----- Bitacora de cambios (RN-06) -----
public record BitacoraEntradaDto(Guid Id, string Categoria, string Descripcion, string? Autor, DateTimeOffset Fecha);

public record ActualizarFinanzasRequest(
    string Moneda,
    int DiaCorte,
    bool TasaMoraEsLegal,
    decimal? TasaMoraValor,
    int PeriodoGraciaDias);

// ===== Configuracion avanzada de Finanzas (modulo 2.3 seccion 2 "Mas informacion") =====

public record CuentaBancariaDto(
    Guid Id,
    string NumeroCuenta,
    Propia.Domain.Enums.TipoCuentaBancaria TipoCuenta,
    string Banco,
    bool VerEnFactura,
    bool Cancelada,
    DateTimeOffset? FechaCancelacion);

public record CrearCuentaBancariaRequest(
    string NumeroCuenta,
    Propia.Domain.Enums.TipoCuentaBancaria TipoCuenta,
    string Banco,
    bool VerEnFactura);

public record ActualizarCuentaBancariaRequest(
    string NumeroCuenta,
    Propia.Domain.Enums.TipoCuentaBancaria TipoCuenta,
    string Banco,
    bool VerEnFactura,
    bool Cancelada);

public record ConfiguracionFinanzasDto(
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeo,
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeoCuotaExtra,
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeoProntoPago,
    string? ConsecutivoFactura,
    string? ConsecutivoRC,
    string? ConsecutivoNotaCredito,
    string? ConsecutivoPazYSalvo,
    string? ConsecutivoActaConsejo,
    string? ConsecutivoActaAsamblea,
    string? ConvenioRecaudo,
    string? Chartld,
    string? ComunicacionFactura,
    string? WenjoyCodigoRecaudo,
    Propia.Domain.Enums.TiposPagoPermitidos TiposPagoPermitidos,
    string? FormasDePago,
    decimal MinimoSaldoProntoPago,
    decimal MinimoSaldoCartera,
    string? CuentaContable,
    string? ZonaFacturacion,
    int? EstratoFacturacion);

public record ActualizarConfiguracionFinanzasRequest(
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeo,
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeoCuotaExtra,
    Propia.Domain.Enums.MultiploRedondeo MultiploRedondeoProntoPago,
    string? ConsecutivoFactura,
    string? ConsecutivoRC,
    string? ConsecutivoNotaCredito,
    string? ConsecutivoPazYSalvo,
    string? ConsecutivoActaConsejo,
    string? ConsecutivoActaAsamblea,
    string? ConvenioRecaudo,
    string? Chartld,
    string? ComunicacionFactura,
    string? WenjoyCodigoRecaudo,
    Propia.Domain.Enums.TiposPagoPermitidos TiposPagoPermitidos,
    string? FormasDePago,
    decimal MinimoSaldoProntoPago,
    decimal MinimoSaldoCartera,
    string? CuentaContable,
    string? ZonaFacturacion,
    int? EstratoFacturacion);

// ===========================================================================
// Ficha completa de zona comun (2.3 seccion 4, prototipo zonaDet)
// ===========================================================================

public record ZonaFacturaDto(Guid Id, string Concepto, decimal? Valor, DateOnly? Fecha);
public record ZonaDocumentoDto(Guid Id, string Nombre, string Url);
public record ZonaCampoDto(Guid Id, string Label, string? Valor);
public record ZonaContratoRefDto(Guid Id, string Nombre);

public record ZonaComentarioDto(Guid Id, string AutorNombre, string AutorIniciales, string Texto, string FechaTexto);

public record ZonaNovedadDto(
    Guid Id, string Titulo, string? Texto, string? ImagenUrl,
    string AutorNombre, string AutorIniciales, string FechaTexto,
    int Likes, bool YoDiLike,
    IReadOnlyList<ZonaComentarioDto> Comentarios);

/// <summary>Agregado que alimenta el modal de ficha completa de la zona comun.</summary>
public record ZonaFichaDto(
    ZonaComunDto Zona,
    string? ImagenUrl,
    string MantenimientoTipo,
    string? MantenimientoContrato,
    string MantenimientoFrecuencia,
    int? MantenimientoDiaMes,
    IReadOnlyList<ZonaFacturaDto> Facturas,
    IReadOnlyList<ZonaDocumentoDto> Documentos,
    IReadOnlyList<ZonaCampoDto> Campos,
    IReadOnlyList<VentanaDisponibilidadDto> Horarios,
    IReadOnlyList<ZonaContratoRefDto> ContratosDisponibles,
    IReadOnlyList<ZonaNovedadDto> Novedades);

/// <summary>Guarda la config de la ficha (imagen + mantenimiento + reservable + aforo).</summary>
public record GuardarZonaFichaRequest(
    string? ImagenUrl,
    string MantenimientoTipo,
    string? MantenimientoContrato,
    string MantenimientoFrecuencia,
    int? MantenimientoDiaMes,
    bool EsReservable,
    int? CapacidadPersonas);

public record AgregarZonaFacturaRequest(string Concepto, decimal? Valor, DateOnly? Fecha);
public record AgregarZonaCampoRequest(string Label, string? Valor);
public record PublicarZonaNovedadRequest(string Titulo, string? Texto, string? ImagenUrl);
public record ComentarZonaNovedadRequest(string Texto);
