namespace Propia.Domain.Enums;

/// <summary>Spec 2.11 v1.0 - El activo puede ser un equipo fisico (2.3) o una zona comun (2.3).</summary>
public enum TipoActivoMantenimiento
{
    Equipo = 1,
    ZonaComun = 2
}

/// <summary>Estado del equipo fisico (spec 2.11 tabla 3). Fuente unica de verdad - 2.11 lo escribe.</summary>
public enum EstadoEquipoActivo
{
    Operativo = 1,
    EnMantenimiento = 2,
    FueraDeServicio = 3
}

/// <summary>Estado de la zona comun. Si EnMantenimiento, bloquea reservas en 2.13 (RN-08).</summary>
public enum EstadoZonaComunMantenimiento
{
    Activa = 1,
    EnMantenimiento = 2,
    Inactiva = 3
}

/// <summary>Frecuencia del plan preventivo. Spec 2.11 seccion 5.</summary>
public enum FrecuenciaMantenimiento
{
    Semanal = 1,
    Quincenal = 2,
    Mensual = 3,
    Trimestral = 4,
    Semestral = 5,
    Anual = 6,
    Personalizada = 7
}

/// <summary>Modalidad de disparo del plan preventivo. Spec 2.11 seccion 5.</summary>
public enum DisparoPlanMantenimiento
{
    Automatico = 1,
    ConConfirmacion = 2
}

/// <summary>Naturaleza de la intervencion. Spec 2.11 seccion 4.</summary>
public enum TipoIntervencionMantenimiento
{
    Preventivo = 1,
    Correctivo = 2
}

/// <summary>Origen de la intervencion. Spec 2.11 seccion 6.</summary>
public enum OrigenIntervencion
{
    Manual = 1,
    Automatico = 2,
    Pqrsd = 3,
    WhatsAppIa = 4
}

/// <summary>Estado de la intervencion. Spec 2.11 seccion 7. Completada/Cancelada terminales.</summary>
public enum EstadoIntervencion
{
    Programada = 1,
    EnEjecucion = 2,
    EnRevision = 3,
    Completada = 4,
    Cancelada = 5
}

/// <summary>Prioridad de la intervencion. Spec 2.11 seccion 6.</summary>
public enum PrioridadIntervencion
{
    Urgente = 1,
    Alta = 2,
    Normal = 3,
    Baja = 4
}

/// <summary>Tipo de autor de una entrada de bitacora. Spec 2.11 seccion 9.</summary>
public enum TipoAutorBitacoraMantenimiento
{
    Administrador = 1,
    PersonalInterno = 2,
    Proveedor = 3,
    Sistema = 4
}

/// <summary>Color del semaforo de vencimiento. Spec 2.11 seccion 8. Calculado en backend (RN-10).</summary>
public enum SemaforoMantenimiento
{
    /// <summary>Sin plan preventivo configurado.</summary>
    Negro = 0,
    /// <summary>Al dia.</summary>
    Verde = 1,
    /// <summary>Proximo a vencer.</summary>
    Amarillo = 2,
    /// <summary>Vencido sin intervencion completada en el periodo.</summary>
    Rojo = 3
}
