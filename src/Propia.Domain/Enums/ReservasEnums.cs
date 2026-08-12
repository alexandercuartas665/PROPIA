namespace Propia.Domain.Enums;

/// <summary>Modalidad de cobro de la zona. Spec 2.13 seccion 4.3.</summary>
public enum ModalidadCobroReserva
{
    PorHora = 1,
    PorFranja = 2,
    PorEvento = 3
}

/// <summary>Politica de reembolso al cancelar. Spec 2.13 seccion 4.3.</summary>
public enum PoliticaReembolso
{
    SinReembolso = 1,
    Total = 2,
    Parcial = 3
}

/// <summary>Comportamiento cuando el residente cancela despues del limite. Spec 2.13 seccion 4.4.</summary>
public enum ComportamientoCancelacionTardia
{
    Bloqueada = 1,
    SinPenalidad = 2,
    ConPenalidad = 3
}

/// <summary>Dias de la semana habilitados para reservas. Spec 2.13 seccion 4.2.</summary>
public enum DiaSemanaReserva
{
    Lunes = 1,
    Martes = 2,
    Miercoles = 3,
    Jueves = 4,
    Viernes = 5,
    Sabado = 6,
    Domingo = 7
}

/// <summary>Estado de la reserva. Spec 2.13 seccion 15.</summary>
public enum EstadoReserva
{
    Confirmada = 1,
    PendienteAprobacion = 2,
    PendientePago = 3,
    Completada = 4,
    CanceladaResidente = 5,
    CanceladaAdmin = 6,
    CanceladaSistema = 7,
    Expirada = 8
}

/// <summary>Frecuencia de reserva recurrente. Spec 2.13 seccion 6.1. (Recurrencia diferida a Fase 2.)</summary>
public enum FrecuenciaRecurrencia
{
    Semanal = 1,
    Quincenal = 2,
    Mensual = 3
}

/// <summary>Estado del cobro Wompi. Spec 2.13 seccion 15. (Wompi real en Fase 2.)</summary>
public enum EstadoPagoReserva
{
    Pendiente = 1,
    Pagado = 2,
    Reembolsado = 3,
    Fallido = 4
}

/// <summary>Tipo de bloqueo manual del admin. Spec 2.13 seccion 15.</summary>
public enum TipoBloqueoZona
{
    Franja = 1,
    DiaCompleto = 2,
    Periodo = 3
}

/// <summary>Etiqueta libre del bloqueo. Spec 2.13 seccion 15.</summary>
public enum EtiquetaBloqueo
{
    Mantenimiento = 1,
    EventoCopropiedad = 2,
    Cerrado = 3,
    Otro = 4
}

/// <summary>Origen del bloqueo. Spec 2.13 seccion 13 (auto desde 2.3 cuando zona pasa a mantenimiento).</summary>
public enum OrigenBloqueoZona
{
    ManualAdmin = 1,
    AutomaticoSistema = 2
}

/// <summary>Momento en que se toma una foto de trazabilidad de un prestamo/reserva.</summary>
public enum MomentoEntrega
{
    Entrega = 1,
    Devolucion = 2
}

/// <summary>Estado del prestamo de un equipo/activo reservable.</summary>
public enum EstadoPrestamoEquipo
{
    Reservado = 1,
    Entregado = 2,
    Devuelto = 3,
    Cancelado = 4
}
