namespace Propia.Domain.Enums;

/// <summary>Escenario de origen de la transferencia. Spec 1.5 seccion 3.</summary>
public enum EscenarioTransferencia
{
    EntregaVoluntaria = 1,
    ReclamacionEntrante = 2,
    GestionCopropiedad = 3
}

/// <summary>Estado del proceso de transferencia. Spec 1.5 seccion 8.</summary>
public enum EstadoTransferencia
{
    Iniciado = 1,
    PendienteAprobacion = 2,
    ActaEnValidacion = 3,
    AlertasActivas = 4,
    Ejecutado = 5,
    Cancelado = 6
}

/// <summary>Resultado de la validacion IA del acta de asamblea. Spec 1.5 seccion 4.2.</summary>
public enum ResultadoValidacionActa
{
    Pendiente = 1,
    Valida = 2,
    RequiereRevision = 3,
    Invalida = 4
}

/// <summary>Tipo de evento del historial de la transferencia. Spec 1.5 seccion 13.</summary>
public enum TipoEventoTransferencia
{
    SolicitudEnviada = 1,
    AprobacionSaliente = 2,
    RechazoSaliente = 3,
    ActaSubida = 4,
    ActaValidada = 5,
    ActaInvalida = 6,
    AlertaDia1 = 7,
    AlertaDia7 = 8,
    AlertaDia13 = 9,
    CorteEjecutado = 10,
    Cancelacion = 11,
    EscaladoAdmin = 12
}
// Estado de custodia: ya existe en Propia.Domain.Enums.EstadoCustodia (compartido con Tenant).
