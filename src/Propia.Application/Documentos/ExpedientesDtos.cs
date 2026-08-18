namespace Propia.Application.Documentos;

// ===========================================================================
// Vista "Expedientes" (TRD) del modulo 2.15. DTOs.
// ===========================================================================

// ----- Configuracion documental (TRD: series/subseries/tipologias/campos) -----
public record TrdSerieDto(Guid Id, string Nombre, int Orden, IReadOnlyList<TrdSubserieDto> Subseries);
public record TrdSubserieDto(Guid Id, string Nombre, int Orden, IReadOnlyList<TrdTipologiaDto> Tipologias, IReadOnlyList<TrdCampoDto> Campos);
public record TrdTipologiaDto(Guid Id, string Nombre, int Orden);
public record TrdCampoDto(Guid Id, string Clave, string Label, int Orden);

public record CrearSerieRequest(string Nombre);
public record CrearSubserieRequest(Guid SerieId, string Nombre);
public record CrearTipologiaCfgRequest(Guid SubserieId, string Nombre);
public record CrearCampoCfgRequest(Guid SubserieId, string Label);

// ----- Expedientes (instancias) -----
public record ExpedienteListaDto(
    Guid Id, string Codigo, string Nombre, string Serie, string Subserie,
    int Total, int Cargadas, int PendientesObligatorias);

public record ExpedienteCampoDto(Guid Id, string Clave, string Label);

public record ExpedienteTipologiaDto(
    Guid Id, string Nombre, bool Obligatoria, bool Cargado,
    string? ArchivoNombre, string? ArchivoMime, long ArchivoTamano,
    IReadOnlyList<ExpedienteMetaPairDto> Meta,
    int NumeroVersiones = 0);

/// <summary>Version historica del archivo de una tipologia (para consultar versiones viejas).</summary>
public record TipologiaVersionDto(
    Guid Id, int Numero, string ArchivoNombre, string ArchivoMime, long ArchivoTamano,
    string? NotasCambio, DateTimeOffset CreadoAt);

public record ExpedienteMetaPairDto(string Clave, string Label, string Valor);

public record ExpedienteDetalleDto(
    Guid Id, string Codigo, string Nombre, string Serie, string Subserie,
    int Total, int Cargadas,
    IReadOnlyList<ExpedienteCampoDto> Campos,
    IReadOnlyList<ExpedienteTipologiaDto> Tipologias);

/// <summary>
/// Vista previa renderizable de una tipologia. Kind indica como pintarla en el visor:
/// "image"/"pdf" -> DataUri (data:mime;base64); "text" -> Texto plano; "html" -> Html (excel/word rendereados); "none" -> sin preview (solo descarga).
/// </summary>
public record TipologiaPreviewDto(
    string Kind, string Nombre, string Mime,
    string? DataUri = null, string? Html = null, string? Texto = null);

public record CrearExpedienteRequest(Guid SubserieId, string Nombre);
public record AgregarTipologiaExpRequest(string Nombre, bool Obligatoria);
public record AgregarCampoExpRequest(string Label);
public record CargarTipologiaRequest(
    string NombreArchivo, string TipoMime, long TamanoBytes, string ContenidoBase64,
    IReadOnlyList<ExpedienteMetaValorDto>? Meta, string? NotasCambio = null);
public record ExpedienteMetaValorDto(string Clave, string? Valor);
