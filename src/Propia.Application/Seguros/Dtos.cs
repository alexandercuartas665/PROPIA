using Propia.Domain.Enums;

namespace Propia.Application.Seguros;

// ----- Poliza -----
/// <summary>PDF origen de una poliza para descarga gateada (bytes + tipo + nombre sugerido).</summary>
public record PdfOrigenDescarga(byte[] Contenido, string ContentType, string NombreArchivo);

public record PolizaDto(
    Guid Id,
    string? NumeroPoliza,
    string Aseguradora, Guid? AseguradoraPersonaId, Guid? AseguradoraEmpresaId,
    string? Corredor, Guid? CorredorPersonaId, Guid? CorredorEmpresaId,
    DateOnly? FechaInicio, DateOnly? FechaFin,
    decimal? ValorPoliza, int? FormaPagoCuotas, bool PagoMensual,
    string? Cobertura, bool IncluyeZonasUnidades, string? ValoresAgregados, string? Observaciones,
    Guid? ExpedienteId,
    int? DiasParaVencer, SemaforoContrato Semaforo,
    int ReclamacionesCount = 0,
    IReadOnlyList<PolizaCampoValorDto>? CamposValores = null,
    bool TienePdfOrigen = false);

public record CrearPolizaRequest(
    string Aseguradora,
    string? NumeroPoliza = null,
    Guid? AseguradoraPersonaId = null, Guid? AseguradoraEmpresaId = null,
    string? Corredor = null, Guid? CorredorPersonaId = null, Guid? CorredorEmpresaId = null,
    DateOnly? FechaInicio = null, DateOnly? FechaFin = null,
    decimal? ValorPoliza = null, int? FormaPagoCuotas = null, bool PagoMensual = false,
    string? Cobertura = null, bool IncluyeZonasUnidades = false,
    string? ValoresAgregados = null, string? Observaciones = null, Guid? ExpedienteId = null,
    string? PdfOrigenKey = null);

/// <summary>Actualiza la poliza (MERGE: se aplica lo provisto). LimpiarExpediente desconecta el expediente.</summary>
public record ActualizarPolizaRequest(
    string Aseguradora,
    string? NumeroPoliza = null,
    Guid? AseguradoraPersonaId = null, Guid? AseguradoraEmpresaId = null,
    string? Corredor = null, Guid? CorredorPersonaId = null, Guid? CorredorEmpresaId = null,
    DateOnly? FechaInicio = null, DateOnly? FechaFin = null,
    decimal? ValorPoliza = null, int? FormaPagoCuotas = null, bool PagoMensual = false,
    string? Cobertura = null, bool IncluyeZonasUnidades = false,
    string? ValoresAgregados = null, string? Observaciones = null,
    Guid? ExpedienteId = null, bool LimpiarExpediente = false,
    string? PdfOrigenKey = null);

// ----- Campos personalizados (EAV) de polizas -----
public record PolizaCampoDto(Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones, string? Descripcion, bool Activo);
public record PolizaCampoValorDto(Guid CampoId, string? Valor);
public record CrearPolizaCampoRequest(string Label, TipoCampoTablero Tipo, string? Opciones, string? Descripcion);
public record ActualizarPolizaCampoRequest(string Label, TipoCampoTablero Tipo, string? Opciones, string? Descripcion, int Orden, bool Activo);
public record GuardarPolizaCampoValorRequest(string? Valor);

// ----- Reclamaciones (Ola 5) -----
public record ReclamacionDto(
    Guid Id, DateOnly Fecha, decimal MontoReclamado, string Descripcion,
    EstadoReclamacion Estado, decimal? MontoReconocido, DateTimeOffset? FechaCierre, Guid? ExpedienteId);
public record CrearReclamacionRequest(DateOnly Fecha, decimal MontoReclamado, string Descripcion, Guid? ExpedienteId = null);
public record CerrarReclamacionRequest(decimal MontoReconocido);
