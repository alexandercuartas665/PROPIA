using Propia.Domain.Enums;

namespace Propia.Application.Siniestros;

// ----- Siniestro -----
/// <summary>Siniestro listo para la tabla y la ficha. ElementoNombre/PolizaNumero/EstadoNombre se resuelven
/// en lectura (el elemento es un vinculo logico por Id).</summary>
public record SiniestroDto(
    Guid Id, string Numero, string Titulo, string? Descripcion,
    string? TipoClave, string? TipoLabel,
    DateOnly FechaOcurrencia, DateOnly? FechaReporte,
    ElementoSiniestro ElementoTipo, Guid? ElementoId, string? ElementoOtro, string? ElementoNombre,
    Guid? PolizaId, string? PolizaNumero, string? Aseguradora, string? NumeroReclamacion,
    Guid? ResponsablePersonaId, string? ResponsableNombre,
    decimal? MontoEstimado, decimal? MontoReclamado, decimal? Deducible, decimal? MontoReconocido, DateOnly? FechaPago,
    Guid EstadoId, string EstadoNombre, string? EstadoColor, string? EstadoClave,
    DateTimeOffset? FechaCierre, Guid? TareaId,
    DateTimeOffset CreatedAt,
    int DocumentosCount = 0, int ComentariosCount = 0,
    string? TareaNumero = null, string? TareaEstado = null);

/// <summary>Alta de un siniestro. EstadoId null = estado base "Reportado".</summary>
public record CrearSiniestroRequest(
    string Titulo,
    DateOnly FechaOcurrencia,
    string? Descripcion = null,
    string? TipoClave = null,
    DateOnly? FechaReporte = null,
    ElementoSiniestro ElementoTipo = ElementoSiniestro.Ninguno,
    Guid? ElementoId = null,
    string? ElementoOtro = null,
    Guid? PolizaId = null,
    string? NumeroReclamacion = null,
    Guid? ResponsablePersonaId = null,
    decimal? MontoEstimado = null,
    decimal? MontoReclamado = null,
    decimal? Deducible = null,
    Guid? EstadoId = null);

/// <summary>
/// Actualiza un siniestro. Es un MERGE: null = "no enviado" y se conserva. Para BORRAR un opcional se nombra
/// explicito en <see cref="Limpiar"/> (claves: descripcion, tipo, fechareporte, poliza, numeroreclamacion,
/// responsable, montoestimado, montoreclamado, deducible, montoreconocido, fechapago). El elemento afectado
/// se reemplaza completo cuando se envia ElementoTipo.
/// </summary>
public record ActualizarSiniestroRequest(
    string? Titulo = null,
    string? Descripcion = null,
    string? TipoClave = null,
    DateOnly? FechaOcurrencia = null,
    DateOnly? FechaReporte = null,
    ElementoSiniestro? ElementoTipo = null,
    Guid? ElementoId = null,
    string? ElementoOtro = null,
    Guid? PolizaId = null,
    string? NumeroReclamacion = null,
    Guid? ResponsablePersonaId = null,
    decimal? MontoEstimado = null,
    decimal? MontoReclamado = null,
    decimal? Deducible = null,
    decimal? MontoReconocido = null,
    DateOnly? FechaPago = null,
    IReadOnlyList<string>? Limpiar = null);

/// <summary>Cambio de estado. Si el estado destino es Pagado/Cerrado y el siniestro no tiene monto
/// reconocido, se exige MontoReconocido. FechaPago opcional (default hoy al pasar a Pagado).</summary>
public record CambiarEstadoSiniestroRequest(Guid EstadoId, decimal? MontoReconocido = null, DateOnly? FechaPago = null);

// ----- Estados (columnas) -----
public record SiniestroEstadoDto(Guid Id, string Nombre, int Orden, string? Color, bool Activo, string? Clave, int SiniestrosCount = 0);
public record CrearSiniestroEstadoRequest(string Nombre, string? Color);
/// <summary>MERGE: null = no enviado.</summary>
public record ActualizarSiniestroEstadoRequest(string? Nombre = null, string? Color = null, bool? Activo = null);
public record ReordenarSiniestroEstadosRequest(List<Guid> Orden);

// ----- Campos personalizados (EAV) -----
public record SiniestroCampoDto(Guid Id, string Label, int Orden, TipoCampoTablero Tipo, string? Opciones, string? Descripcion, bool Activo);
public record CrearSiniestroCampoRequest(string Label, TipoCampoTablero Tipo, string? Opciones, string? Descripcion);
/// <summary>MERGE (forma estandar del editor de campos): Descripcion/Activo/Label null = se conservan.</summary>
public record ActualizarSiniestroCampoRequest(string? Label, TipoCampoTablero Tipo, string? Opciones, string? Descripcion, int Orden, bool? Activo);
public record GuardarSiniestroCampoValorRequest(string? Valor);
public record SiniestroCampoValorFlatDto(Guid SiniestroId, Guid DefinicionId, string? Valor);
public record SiniestroCampoDinDto(Guid DefinicionId, string Label, int Orden, string? Valor, TipoCampoTablero Tipo, string? Opciones);

// ----- Documentos y actividad -----
public record SiniestroDocumentoDto(Guid Id, string Nombre, string Url, DateTimeOffset CreatedAt);
public record SiniestroComentarioDto(Guid Id, string Texto, string? AutorNombre, DateTimeOffset CreatedAt, Guid? AutorUsuarioId, bool EsSistema);
public record AgregarComentarioSiniestroRequest(string Texto);
